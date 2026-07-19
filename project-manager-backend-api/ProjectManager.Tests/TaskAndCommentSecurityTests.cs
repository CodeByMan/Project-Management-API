using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using ProjectManager.Controllers;
using ProjectManager.Data;
using ProjectManager.DTOs;
using ProjectManager.Interfaces;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Tests;

public class TaskAndCommentSecurityTests
{
    [Fact]
    public async Task CreateTask_RejectsAssigneeOutsideProjectMembership()
    {
        var userManager = JwtTests.CreateUserManager();
        var manager = new ApplicationUser { Id = "manager-1" };
        userManager.Setup(item => item.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(manager);
        var projectRepository = new Mock<IProjectRepository>();
        projectRepository.Setup(repository => repository.GetProjectByIdAsync(1)).ReturnsAsync(new Project
        {
            Id = 1,
            CreatorId = manager.Id,
            ProjectUsers = new List<ProjectUser> { new() { UserId = "member-1" } }
        });
        var taskRepository = new Mock<ITaskRepository>();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), "CanManageProject"))
            .ReturnsAsync(AuthorizationResult.Success());
        var controller = new TaskController(userManager.Object, taskRepository.Object, projectRepository.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("manager-1", "Manager") }
        };

        var result = await controller.CreateTask(new CreateTaskDto
        {
            Title = "Task",
            Description = "Description",
            Priority = "High",
            DueDate = DateTime.UtcNow.AddDays(1),
            Status = "Open",
            ProjectId = 1,
            AssignedUserId = "outsider"
        });

        Assert.IsType<BadRequestObjectResult>(result);
        taskRepository.Verify(repository => repository.CreateTaskAsync(It.IsAny<ProjectTask>(), It.IsAny<List<int>?>()), Times.Never);
    }

    [Fact]
    public async Task CreateTask_RejectsUnknownTagIds()
    {
        var userManager = JwtTests.CreateUserManager();
        var manager = new ApplicationUser { Id = "manager-1" };
        userManager.Setup(item => item.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(manager);
        var projectRepository = new Mock<IProjectRepository>();
        projectRepository.Setup(repository => repository.GetProjectByIdAsync(1)).ReturnsAsync(new Project
        {
            Id = 1,
            CreatorId = manager.Id,
            ProjectUsers = new List<ProjectUser> { new() { UserId = "member-1" } }
        });
        var taskRepository = new Mock<ITaskRepository>();
        taskRepository.Setup(repository => repository.TagsExistAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(false);
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), "CanManageProject"))
            .ReturnsAsync(AuthorizationResult.Success());
        var controller = new TaskController(userManager.Object, taskRepository.Object, projectRepository.Object, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("manager-1", "Manager") }
        };

        var result = await controller.CreateTask(new CreateTaskDto
        {
            Title = "Task",
            Description = "Description",
            Priority = "High",
            DueDate = DateTime.UtcNow.AddDays(1),
            Status = "Open",
            ProjectId = 1,
            AssignedUserId = "member-1",
            TagIds = new List<int> { 999 }
        });

        Assert.IsType<BadRequestObjectResult>(result);
        taskRepository.Verify(repository => repository.CreateTaskAsync(It.IsAny<ProjectTask>(), It.IsAny<List<int>?>()), Times.Never);
    }

    [Fact]
    public async Task AddComment_DeniesUserWithoutTaskAccess()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        await using var context = new ApplicationDbContext(options);
        context.Projects.Add(new Project
        {
            Id = 1,
            Name = "Project",
            Description = "Description",
            Status = "Active",
            CreatorId = "manager-1",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(1)
        });
        context.Tasks.Add(new ProjectTask
        {
            Id = 1,
            Title = "Task",
            Description = "Description",
            Priority = "High",
            Status = "Open",
            DueDate = DateTime.UtcNow.AddDays(1),
            CreatorId = "manager-1",
            AssignedUserId = "member-1",
            ProjectId = 1
        });
        await context.SaveChangesAsync();

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), "CanViewTask"))
            .ReturnsAsync(AuthorizationResult.Failed());
        var controller = new CommentsController(context, authorization.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = CreateHttpContext("outsider", "Member") }
        };

        var result = await controller.AddCommentToTask(1, new CreateCommentDto { Content = "Should not be saved" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Empty(context.Comments);
    }

    private static DefaultHttpContext CreateHttpContext(string userId, string role)
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        }, "Test"));
        return context;
    }
}
