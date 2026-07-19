using Microsoft.AspNetCore.Authorization;
using ProjectManager.Auth.Handlers;
using ProjectManager.Auth.Requirements;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Tests;

public class AuthorizationHandlersTests
{
    [Fact]
    public async Task Admin_CanAccessAnyProject()
    {
        var project = new Project { CreatorId = "manager-1" };
        var context = CreateContext(ProjectOperations.Delete, CreateUser("admin-1", "Admin"), project);

        await new ProjectAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CannotViewUnrelatedProject()
    {
        var project = new Project { CreatorId = "manager-2" };
        var context = CreateContext(ProjectOperations.View, CreateUser("manager-1", "Manager"), project);

        await new ProjectAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Manager_CanManageOwnedProject()
    {
        var project = new Project { CreatorId = "manager-1" };
        var context = CreateContext(ProjectOperations.ManageMembers, CreateUser("manager-1", "Manager"), project);

        await new ProjectAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Member_CanViewOnlyJoinedProject()
    {
        var project = new Project
        {
            CreatorId = "manager-1",
            ProjectUsers = new List<ProjectUser>
            {
                new() { UserId = "member-1" }
            }
        };
        var joinedContext = CreateContext(ProjectOperations.View, CreateUser("member-1", "Member"), project);
        var unrelatedContext = CreateContext(ProjectOperations.View, CreateUser("member-2", "Member"), project);
        var handler = new ProjectAuthorizationHandler();

        await handler.HandleAsync(joinedContext);
        await handler.HandleAsync(unrelatedContext);

        Assert.True(joinedContext.HasSucceeded);
        Assert.False(unrelatedContext.HasSucceeded);
    }


    [Fact]
    public async Task Manager_CannotManageMembersOfUnrelatedProject()
    {
        var project = new Project { CreatorId = "manager-2" };
        var context = CreateContext(ProjectOperations.ManageMembers, CreateUser("manager-1", "Manager"), project);

        await new ProjectAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Member_CannotManageProjectMembers()
    {
        var project = new Project
        {
            CreatorId = "manager-1",
            ProjectUsers = new List<ProjectUser> { new() { UserId = "member-1" } }
        };
        var context = CreateContext(ProjectOperations.ManageMembers, CreateUser("member-1", "Member"), project);

        await new ProjectAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AssignedMember_CannotManageTaskDetails()
    {
        var task = new ProjectTask
        {
            AssignedUserId = "member-1",
            Project = new Project { CreatorId = "manager-1" }
        };
        var context = CreateContext(TaskOperations.Manage, CreateUser("member-1", "Member"), task);

        await new TaskAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task AssignedMember_CanUpdateTaskStatus()
    {
        var task = new ProjectTask
        {
            AssignedUserId = "member-1",
            Project = new Project { CreatorId = "manager-1" }
        };
        var context = CreateContext(TaskOperations.UpdateStatus, CreateUser("member-1", "Member"), task);

        await new TaskAuthorizationHandler().HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Theory]
    [MemberData(nameof(ManagerOnlyTaskOperations))]
    public async Task AssignedMember_CannotPerformManagerOnlyTaskOperations(IAuthorizationRequirement requirement)
    {
        var task = new ProjectTask
        {
            AssignedUserId = "member-1",
            Project = new Project { CreatorId = "manager-1" }
        };
        var context = CreateContext(requirement, CreateUser("member-1", "Member"), task);

        await new TaskAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    public static IEnumerable<object[]> ManagerOnlyTaskOperations =>
        new[]
        {
            new object[] { TaskOperations.Manage },
            new object[] { TaskOperations.Assign },
            new object[] { TaskOperations.Delete }
        };

    [Fact]
    public async Task Manager_CannotViewTaskFromUnrelatedProject()
    {
        var task = new ProjectTask
        {
            AssignedUserId = "member-1",
            Project = new Project { CreatorId = "manager-2" }
        };
        var context = CreateContext(TaskOperations.View, CreateUser("manager-1", "Manager"), task);

        await new TaskAuthorizationHandler().HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task CommentAuthor_CanEditOwnComment_ButNotAnotherUsersComment()
    {
        var handler = new CommentAuthorizationHandler();
        var ownComment = new Comment { AuthorId = "member-1" };
        var otherComment = new Comment { AuthorId = "member-2" };
        var user = CreateUser("member-1", "Member");
        var ownContext = CreateContext(CommentOperations.Edit, user, ownComment);
        var otherContext = CreateContext(CommentOperations.Edit, user, otherComment);

        await handler.HandleAsync(ownContext);
        await handler.HandleAsync(otherContext);

        Assert.True(ownContext.HasSucceeded);
        Assert.False(otherContext.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(
        IAuthorizationRequirement requirement,
        ClaimsPrincipal user,
        object resource)
    {
        return new AuthorizationHandlerContext(new[] { requirement }, user, resource);
    }

    private static ClaimsPrincipal CreateUser(string userId, string role)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Role, role)
            },
            "Test");

        return new ClaimsPrincipal(identity);
    }
}
