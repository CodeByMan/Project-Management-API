using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Auth.Handlers;
using ProjectManager.Auth.Requirements;
using ProjectManager.Configuration;
using ProjectManager.Data;
using ProjectManager.Filters;
using ProjectManager.Interfaces;
using ProjectManager.Middleware;
using ProjectManager.Models;
using ProjectManager.Repositories;
using ProjectManager.Services;
using Serilog;
using Microsoft.OpenApi.Models;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// --- 1. Service Registration ---

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .Enrich.FromLogContext() // Important for Correlation ID!
    .CreateLogger();

builder.Host.UseSerilog();


// 1a. Core Services
builder.Services.AddControllers(options =>
{
    options.Filters.Add<AuditLogActionFilter>(); // Add global filter for Audit Logging
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Project Manager Backend API",
        Version = "v1",
        Description = "Project management API maintained by Muhammad Ali Nawaz."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<IAuthorizationHandler, TaskAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, ProjectAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CommentAuthorizationHandler>();
builder.Services.AddSingleton<IProjectMappingService, ProjectMappingService>();
builder.Services.AddSingleton<ITaskMappingService, TaskMappingService>();

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

// 1b. Database & Identity
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null)
    ));

// IMPORTANT: Configure Identity to NOT add default authentication scheme
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Disable automatic cookie-based authentication redirect
    options.SignIn.RequireConfirmedAccount = true;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var identityTokenLifetimeHours = builder.Configuration.GetValue<int?>("IdentityToken:LifetimeHours") ?? 2;
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
{
    options.TokenLifespan = TimeSpan.FromHours(Math.Clamp(identityTokenLifetimeHours, 1, 24));
});

// Configure Identity to not use cookies for API
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
});

// 1c. Custom Application Services
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<JwtSecurityStampValidator>();
builder.Services.AddTransient<IEmailSender, EmailSender>();

// 1d. JWT Authentication Configuration
var jwtSettings = JwtSettings.Load(builder.Configuration);

// Set JWT Bearer as the DEFAULT authentication scheme
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = jwtSettings.CreateValidationParameters();
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (context.Principal == null)
            {
                context.Fail("The access token principal is unavailable.");
                return;
            }

            var validator = context.HttpContext.RequestServices
                .GetRequiredService<JwtSecurityStampValidator>();

            if (!await validator.IsCurrentAsync(context.Principal))
            {
                context.Fail("The access token is no longer valid.");
            }
        }
    };
});

// 1e. Authorization Policies
builder.Services.AddAuthorization(options =>
{
    // Task Policies

    options.AddPolicy("CanManageTask", policy =>
        policy.Requirements.Add(TaskOperations.Manage));

    options.AddPolicy("CanAssignTask", policy =>
        policy.Requirements.Add(TaskOperations.Assign));

    options.AddPolicy("CanUpdateTaskStatus", policy =>
        policy.Requirements.Add(TaskOperations.UpdateStatus));

    options.AddPolicy("CanDeleteTask", policy =>
        policy.Requirements.Add(TaskOperations.Delete));

    options.AddPolicy("CanViewTask", policy =>
        policy.Requirements.Add(TaskOperations.View));

    // Project Policies

    options.AddPolicy("CanManageProject", policy =>
        policy.Requirements.Add(ProjectOperations.ManageMembers)); // Create is a project operation for task creation, Project Creation is handled by role based authorization

    options.AddPolicy("CanViewProject", policy =>
        policy.Requirements.Add(ProjectOperations.View));

    options.AddPolicy("CanModifyProject", policy =>
        policy.Requirements.Add(ProjectOperations.Update));

    options.AddPolicy("CanDeleteProject", policy =>
        policy.Requirements.Add(ProjectOperations.Delete));

    options.AddPolicy("CanManageMembers", policy =>
        policy.Requirements.Add(ProjectOperations.ManageMembers));

    // Comment Policies

    options.AddPolicy("CanEditComment", policy =>
        policy.Requirements.Add(CommentOperations.Edit));
    options.AddPolicy("CanDeleteComment", policy =>
        policy.Requirements.Add(CommentOperations.Delete));
});


var app = builder.Build();

// --- 2. Middleware Configuration ---

// Execute Data Seeder (Role/Admin User Creation)
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");

    await DataSeeder.InitializeAsync(userManager, roleManager, configuration, logger);
}

// 2a. Development Middleware
if (app.Environment.IsDevelopment())
{
    // Swagger UI: Necessary for API testing via browser/Postman
    app.UseSwagger();
    app.UseSwaggerUI();
}


// 2b. Standard Middleware
// Correlation ID wraps the complete request pipeline so every safe response can expose it.
app.UseMiddleware<CorrelationIdMiddleware>();

// Error handling runs before the remaining application middleware.
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();

// Endpoint-specific rate limiting and authorization require routing metadata.
app.UseRouting();

// CORS policy for configured frontend clients.
app.UseCors("FrontendClient");

// Apply endpoint-specific rate limits before authentication work.
app.UseRateLimiter();

// 2d. Security Middleware (Order is CRITICAL: Authentication BEFORE Authorization)
app.UseAuthentication();
app.UseAuthorization();

// 2e. Routing
app.MapControllers();

app.Run();

public partial class Program { }
