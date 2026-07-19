using ProjectManager.DTOs;
using System.ComponentModel.DataAnnotations;

namespace ProjectManager.Tests;

public class ValidationTests
{
    [Fact]
    public void Registration_RejectsInvalidEmailAndWeakPassword()
    {
        var dto = new RegisterDto
        {
            Email = "not-an-email",
            Password = "short",
            FirstName = "Test",
            LastName = "User"
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RegisterDto.Email)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(RegisterDto.Password)));
    }

    [Fact]
    public void Project_RejectsEndDateBeforeStartDate()
    {
        var dto = new CreateProjectDto
        {
            Name = "Project",
            Description = "Description",
            StartDate = new DateTime(2026, 7, 20),
            EndDate = new DateTime(2026, 7, 19),
            Status = "Active"
        };

        Assert.Contains(Validate(dto), error => error.MemberNames.Contains(nameof(CreateProjectDto.EndDate)));
    }

    [Fact]
    public void Task_RejectsUnknownPriorityStatusAndInvalidTags()
    {
        var dto = new CreateTaskDto
        {
            Title = "Task",
            Description = "Description",
            Priority = "Critical",
            DueDate = DateTime.UtcNow.AddDays(1),
            Status = "Finished",
            ProjectId = 1,
            AssignedUserId = "member-1",
            TagIds = new List<int> { 1, 0 }
        };

        var errors = Validate(dto);

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateTaskDto.Priority)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(CreateTaskDto.Status)));
        dto.Priority = "High";
        dto.Status = "Open";

        var tagErrors = Validate(dto);

        Assert.Contains(tagErrors, error => error.MemberNames.Contains(nameof(CreateTaskDto.TagIds)));
    }

    [Fact]
    public void RoleChange_RejectsUnknownRole()
    {
        var errors = Validate(new ChangeUserRoleDto { NewRole = "Owner" });

        Assert.NotEmpty(errors);
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }
}

