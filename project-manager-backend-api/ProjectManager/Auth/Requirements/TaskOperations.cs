using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace ProjectManager.Auth.Requirements
{
    public static class TaskOperations
    {
        public static readonly OperationAuthorizationRequirement View = new() { Name = "View" };
        public static readonly OperationAuthorizationRequirement UpdateStatus = new() { Name = "UpdateStatus" };
        public static readonly OperationAuthorizationRequirement Manage = new() { Name = "Manage" };
        public static readonly OperationAuthorizationRequirement Assign = new() { Name = "Assign" };
        public static readonly OperationAuthorizationRequirement Delete = new() { Name = "Delete" };
    }
}
