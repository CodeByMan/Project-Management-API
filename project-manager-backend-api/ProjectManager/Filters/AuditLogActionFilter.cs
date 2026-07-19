using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using ProjectManager.Data;
using ProjectManager.Services;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Filters
{
    public class AuditLogActionFilter : IAsyncActionFilter
    {
        private readonly IAuditService _auditService;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditLogActionFilter> _logger;

        public AuditLogActionFilter(
            IAuditService auditService,
            ApplicationDbContext context,
            ILogger<AuditLogActionFilter> logger)
        {
            _auditService = auditService;
            _context = context;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpMethod = context.HttpContext.Request.Method;
            var controllerName = context.ActionDescriptor.RouteValues["controller"] ?? string.Empty;
            var actionName = context.ActionDescriptor.RouteValues["action"] ?? string.Empty;
            var target = DetermineAuditTarget(httpMethod, controllerName, actionName);

            object? oldEntitySnapshot = null;
            var entityId = ExtractEntityId(context);

            if (target != null && entityId > 0 && target.Value.Action is "Updated" or "Deleted")
            {
                oldEntitySnapshot = await CaptureState(target.Value.EntityName, entityId);
            }

            var executedContext = await next();

            if (target == null || executedContext.Exception != null || !IsSuccessStatusCode(executedContext))
            {
                return;
            }

            if (target.Value.Action == "Created")
            {
                var createdEntityId = ExtractCreatedEntityId(executedContext, target.Value.EntityName);
                if (createdEntityId > 0)
                {
                    entityId = createdEntityId;
                }
            }

            if (entityId <= 0)
            {
                return;
            }

            object? newEntitySnapshot = null;
            if (target.Value.Action is "Created" or "Updated")
            {
                newEntitySnapshot = await CaptureState(target.Value.EntityName, entityId);
            }

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "System";

            try
            {
                await _auditService.LogAsync(
                    target.Value.EntityName,
                    entityId,
                    target.Value.Action,
                    oldEntitySnapshot,
                    newEntitySnapshot,
                    userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Audit logging failed for {EntityName} {EntityId}. The application response was preserved.",
                    target.Value.EntityName,
                    entityId);
            }
        }

        private async Task<object?> CaptureState(string entityName, int entityId)
        {
            return entityName switch
            {
                "Project" => await _context.Projects
                    .Where(project => project.Id == entityId)
                    .Select(project => new
                    {
                        project.Name,
                        project.Description,
                        project.Status,
                        project.StartDate,
                        project.EndDate
                    })
                    .FirstOrDefaultAsync(),

                "Task" => await _context.Tasks
                    .Where(task => task.Id == entityId)
                    .Select(task => new
                    {
                        task.Title,
                        task.Description,
                        task.Status,
                        task.Priority,
                        task.DueDate,
                        task.AssignedUserId
                    })
                    .FirstOrDefaultAsync(),

                "Comment" => await _context.Comments
                    .Where(comment => comment.Id == entityId)
                    .Select(comment => new { comment.Content, comment.TaskId })
                    .FirstOrDefaultAsync(),

                "ProjectUser" => await _context.ProjectUsers
                    .Where(projectUser => projectUser.ProjectId == entityId)
                    .OrderBy(projectUser => projectUser.UserId)
                    .Select(projectUser => projectUser.UserId)
                    .ToListAsync(),

                _ => null
            };
        }

        private static int ExtractEntityId(ActionExecutingContext context)
        {
            foreach (var argumentName in new[] { "id", "projectId", "taskId" })
            {
                if (context.ActionArguments.TryGetValue(argumentName, out var value) && value is int id)
                {
                    return id;
                }
            }

            return 0;
        }

        private int ExtractCreatedEntityId(ActionExecutedContext executedContext, string entityName)
        {
            if (executedContext.Result is ObjectResult objectResult && objectResult.Value != null)
            {
                var resultType = objectResult.Value.GetType();
                var idProperty = resultType.GetProperty("Id") ?? resultType.GetProperty("id");
                if (idProperty?.GetValue(objectResult.Value) is int id)
                {
                    return id;
                }
            }

            return entityName switch
            {
                "Project" => _context.ChangeTracker.Entries<Project>()
                    .Select(entry => entry.Entity.Id)
                    .Where(id => id > 0)
                    .DefaultIfEmpty()
                    .Max(),
                "Task" => _context.ChangeTracker.Entries<ProjectTask>()
                    .Select(entry => entry.Entity.Id)
                    .Where(id => id > 0)
                    .DefaultIfEmpty()
                    .Max(),
                "Comment" => _context.ChangeTracker.Entries<Comment>()
                    .Select(entry => entry.Entity.Id)
                    .Where(id => id > 0)
                    .DefaultIfEmpty()
                    .Max(),
                _ => 0
            };
        }

        private static bool IsSuccessStatusCode(ActionExecutedContext context)
        {
            if (context.Result is StatusCodeResult statusCodeResult)
            {
                return statusCodeResult.StatusCode is >= 200 and < 300;
            }

            if (context.Result is ObjectResult objectResult)
            {
                return objectResult.StatusCode == null || objectResult.StatusCode is >= 200 and < 300;
            }

            return context.Result is NoContentResult or OkResult or OkObjectResult or CreatedResult or CreatedAtActionResult;
        }

        private static (string EntityName, string Action)? DetermineAuditTarget(
            string httpMethod,
            string controllerName,
            string actionName)
        {
            if (controllerName == "Project")
            {
                if (actionName is "AssignUserToProject" or "AddMemberToProject" or "RemoveMember")
                {
                    return ("ProjectUser", "Updated");
                }

                return httpMethod switch
                {
                    "POST" => ("Project", "Created"),
                    "PUT" or "PATCH" => ("Project", "Updated"),
                    "DELETE" => ("Project", "Deleted"),
                    _ => null
                };
            }

            if (controllerName == "Task")
            {
                return httpMethod switch
                {
                    "POST" when actionName == "CreateTask" => ("Task", "Created"),
                    "POST" => ("Task", "Updated"),
                    "PUT" or "PATCH" => ("Task", "Updated"),
                    "DELETE" => ("Task", "Deleted"),
                    _ => null
                };
            }

            if (controllerName == "Comments")
            {
                return httpMethod switch
                {
                    "POST" => ("Comment", "Created"),
                    "PUT" or "PATCH" => ("Comment", "Updated"),
                    "DELETE" => ("Comment", "Deleted"),
                    _ => null
                };
            }

            return null;
        }
    }
}
