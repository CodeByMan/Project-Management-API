using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using ProjectManager.Controllers;
using ProjectManager.DTOs;
using ProjectManager.Interfaces;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Tests;

public class TaskAuthorizationRegressionTests
{
    [Fact]
    public async Task AssignedMember_CannotReassignTask()
    {
        var task = CreateAssignedTask();
        var taskRepository = CreateTaskRepository(task);
        var authorization = CreateAuthorization("CanAssignTask", AuthorizationResult.Failed());
        var controller = CreateController(taskRepository, authorization);

        var result = await controller.AssignUserToTask(task.Id, new TaskAssignDto
        {
            NewAssignedUserId = "member-2"
        });

        AssertForbidden(result);
        taskRepository.Verify(repository =>
            repository.AssignTaskToUserAsync(It.IsAny<ProjectTask>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignedMember_CannotChangeTaskDueDate()
    {
        var task = CreateAssignedTask();
        var originalDueDate = task.DueDate;
        var taskRepository = CreateTaskRepository(task);
        var authorization = CreateAuthorization("CanManageTask", AuthorizationResult.Failed());
        var controller = CreateController(taskRepository, authorization);

        var result = await controller.UpdateTask(task.Id, CreateUpdateDto(task, dueDate: originalDueDate.AddDays(7)));

        AssertForbidden(result);
        Assert.Equal(originalDueDate, task.DueDate);
        taskRepository.Verify(repository => repository.UpdateTaskAsync(It.IsAny<ProjectTask>()), Times.Never);
    }

    [Fact]
    public async Task AssignedMember_CannotChangeTaskPriority()
    {
        var task = CreateAssignedTask();
        var originalPriority = task.Priority;
        var taskRepository = CreateTaskRepository(task);
        var authorization = CreateAuthorization("CanManageTask", AuthorizationResult.Failed());
        var controller = CreateController(taskRepository, authorization);

        var result = await controller.UpdateTask(task.Id, CreateUpdateDto(task, priority: "Low"));

        AssertForbidden(result);
        Assert.Equal(originalPriority, task.Priority);
        taskRepository.Verify(repository => repository.UpdateTaskAsync(It.IsAny<ProjectTask>()), Times.Never);
    }

    [Fact]
    public async Task AssignedMember_CannotReplaceTaskTags()
    {
        var task = CreateAssignedTask();
        var taskRepository = CreateTaskRepository(task);
        var authorization = CreateAuthorization("CanManageTask", AuthorizationResult.Failed());
        var controller = CreateController(taskRepository, authorization);

        var result = await controller.UpdateTaskTags(task.Id, new List<int> { 2, 3 });

        AssertForbidden(result);
        taskRepository.Verify(repository =>
            repository.UpdateTaskTagsAsync(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
        taskRepository.Verify(repository =>
            repository.TagsExistAsync(It.IsAny<IEnumerable<int>>()), Times.Never);
    }

    [Fact]
    public async Task Manager_CannotReplaceTaskTagsWithUnknownIds()
    {
        var task = CreateAssignedTask();
        var taskRepository = CreateTaskRepository(task);
        taskRepository.Setup(repository => repository.TagsExistAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(false);
        var authorization = CreateAuthorization("CanManageTask", AuthorizationResult.Success());
        var controller = CreateController(taskRepository, authorization, "manager-1", "Manager");

        var result = await controller.UpdateTaskTags(task.Id, new List<int> { 999 });

        Assert.IsType<BadRequestObjectResult>(result);
        taskRepository.Verify(repository =>
            repository.UpdateTaskTagsAsync(It.IsAny<int>(), It.IsAny<List<int>>()), Times.Never);
    }

    [Fact]
    public async Task AssignedMember_CanUpdateOnlyTaskStatus()
    {
        var task = CreateAssignedTask();
        var taskRepository = CreateTaskRepository(task);
        taskRepository.Setup(repository => repository.UpdateTaskStatusAsync(task, "In Progress"))
            .ReturnsAsync(true);
        var authorization = CreateAuthorization("CanUpdateTaskStatus", AuthorizationResult.Success());
        var controller = CreateController(taskRepository, authorization);

        var result = await controller.UpdateTaskStatusByMember(task.Id, new ChangeTaskStatusDto
        {
            NewStatus = "In Progress"
        });

        Assert.IsType<NoContentResult>(result);
        taskRepository.Verify(repository => repository.UpdateTaskStatusAsync(task, "In Progress"), Times.Once);
    }

    private static ProjectTask CreateAssignedTask()
    {
        return new ProjectTask
        {
            Id = 10,
            Title = "Original title",
            Description = "Original description",
            Priority = "High",
            DueDate = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            Status = "Open",
            CreatorId = "manager-1",
            AssignedUserId = "member-1",
            ProjectId = 1,
            Project = new Project
            {
                Id = 1,
                CreatorId = "manager-1",
                ProjectUsers = new List<ProjectUser>
                {
                    new() { UserId = "member-1" },
                    new() { UserId = "member-2" }
                }
            }
        };
    }

    private static UpdateTaskDto CreateUpdateDto(
        ProjectTask task,
        DateTime? dueDate = null,
        string? priority = null)
    {
        return new UpdateTaskDto
        {
            Title = task.Title,
            Description = task.Description,
            Priority = priority ?? task.Priority,
            DueDate = dueDate ?? task.DueDate,
            Status = task.Status
        };
    }

    private static Mock<ITaskRepository> CreateTaskRepository(ProjectTask task)
    {
        var repository = new Mock<ITaskRepository>();
        repository.Setup(item => item.GetTaskByIdAsync(task.Id)).ReturnsAsync(task);
        return repository;
    }

    private static Mock<IAuthorizationService> CreateAuthorization(
        string policy,
        AuthorizationResult result)
    {
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service =>
                service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), policy))
            .ReturnsAsync(result);
        return authorization;
    }

    private static TaskController CreateController(
        Mock<ITaskRepository> taskRepository,
        Mock<IAuthorizationService> authorization,
        string userId = "member-1",
        string role = "Member")
    {
        var controller = new TaskController(
            JwtTests.CreateUserManager().Object,
            taskRepository.Object,
            Mock.Of<IProjectRepository>(),
            authorization.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext(userId, role)
            }
        };

        return controller;
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

    private static void AssertForbidden(IActionResult result)
    {
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }
}
