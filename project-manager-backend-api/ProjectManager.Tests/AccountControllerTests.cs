using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using ProjectManager.Controllers;
using ProjectManager.DTOs;
using ProjectManager.Models;

namespace ProjectManager.Tests;

public class AccountControllerTests
{
    [Fact]
    public async Task Registration_CreatesMemberAndSendsConfirmationEmail()
    {
        var userManager = JwtTests.CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync("member@example.com")).ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(manager => manager.CreateAsync(It.IsAny<ApplicationUser>(), "StrongPass1!"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Member"))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("confirmation-token");

        var roleManager = JwtTests.CreateRoleManager();
        roleManager.Setup(manager => manager.RoleExistsAsync("Member")).ReturnsAsync(true);
        var emailSender = new Mock<IEmailSender>();
        var controller = CreateController(userManager, roleManager, emailSender: emailSender);

        var result = await controller.Register(new RegisterDto
        {
            Email = "member@example.com",
            Password = "StrongPass1!",
            FirstName = "Test",
            LastName = "Member"
        });

        Assert.IsType<OkObjectResult>(result);
        userManager.Verify(manager => manager.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Member"), Times.Once);
        emailSender.Verify(sender => sender.SendEmailAsync(
            "member@example.com",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("confirm-email", StringComparison.OrdinalIgnoreCase))), Times.Once);
    }

    [Fact]
    public async Task Login_ReturnsJwtForConfirmedValidUser()
    {
        var user = new ApplicationUser
        {
            Id = "user-1",
            Email = "member@example.com",
            UserName = "member@example.com",
            FirstName = "Test",
            LastName = "Member"
        };
        var userManager = JwtTests.CreateUserManager();
        userManager.Setup(manager => manager.FindByEmailAsync(user.Email)).ReturnsAsync(user);
        userManager.Setup(manager => manager.IsEmailConfirmedAsync(user)).ReturnsAsync(true);
        userManager.Setup(manager => manager.GetRolesAsync(user)).ReturnsAsync(new[] { "Member" });
        var signInManager = CreateSignInManager(userManager);
        signInManager.Setup(manager => manager.CheckPasswordSignInAsync(user, "StrongPass1!", true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(service => service.CreateToken(user)).ReturnsAsync("signed-jwt");
        var controller = CreateController(userManager, JwtTests.CreateRoleManager(), signInManager, tokenService);

        var result = await controller.Login(new LoginDto
        {
            Email = user.Email,
            Password = "StrongPass1!"
        });

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Contains("signed-jwt", System.Text.Json.JsonSerializer.Serialize(okResult.Value));
    }

    private static AccountController CreateController(
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<RoleManager<IdentityRole>> roleManager,
        Mock<SignInManager<ApplicationUser>>? signInManager = null,
        Mock<ITokenService>? tokenService = null,
        Mock<IEmailSender>? emailSender = null)
    {
        signInManager ??= CreateSignInManager(userManager);
        tokenService ??= new Mock<ITokenService>();
        emailSender ??= new Mock<IEmailSender>();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Client:Url"] = "http://localhost:4200"
            })
            .Build();

        return new AccountController(
            userManager.Object,
            signInManager.Object,
            tokenService.Object,
            roleManager.Object,
            emailSender.Object,
            configuration);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManager(Mock<UserManager<ApplicationUser>> userManager)
    {
        return new Mock<SignInManager<ApplicationUser>>(
            userManager.Object,
            new HttpContextAccessor(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Options.Create(new IdentityOptions()),
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }
}
