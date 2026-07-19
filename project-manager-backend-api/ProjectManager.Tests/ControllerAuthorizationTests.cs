using Microsoft.AspNetCore.Authorization;
using ProjectManager.Controllers;
using System.Reflection;

namespace ProjectManager.Tests;

public class ControllerAuthorizationTests
{
    [Theory]
    [InlineData(nameof(AdminController.CreateUser))]
    [InlineData(nameof(AdminController.DeleteUser))]
    [InlineData(nameof(AdminController.ChangeUserRole))]
    public void AdministrativeMutations_RequireAdminRole(string actionName)
    {
        var method = typeof(AdminController).GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(method);
        var authorizeAttributes = method!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .ToList();

        Assert.Contains(authorizeAttributes, attribute => attribute.Roles == "Admin");
    }
}
