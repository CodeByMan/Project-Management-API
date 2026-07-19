using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using ProjectManager.Models;
using System.Security.Claims;

namespace ProjectManager.Auth.Handlers
{
    public class TaskAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ProjectTask>
    {
        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            OperationAuthorizationRequirement requirement,
            ProjectTask task)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Task.CompletedTask;
            }

            // Administrators have global task authority.
            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var managesOwningProject =
                context.User.IsInRole("Manager") && task.Project?.CreatorId == userId;
            var isAssignedMember =
                context.User.IsInRole("Member") && task.AssignedUserId == userId;

            switch (requirement.Name)
            {
                case "View":
                    if (managesOwningProject || isAssignedMember)
                    {
                        context.Succeed(requirement);
                    }
                    break;

                case "UpdateStatus":
                    if (managesOwningProject || isAssignedMember)
                    {
                        context.Succeed(requirement);
                    }
                    break;

                case "Manage":
                case "Assign":
                case "Delete":
                    if (managesOwningProject)
                    {
                        context.Succeed(requirement);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
