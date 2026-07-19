using ProjectManager.Data;
using ProjectManager.Models;

namespace ProjectManager.Services
{

    public interface IAuditService
    {
        Task LogAsync(string entityName, int entityId, string action, object? oldVal, object? newVal, string userId);
    }

    public class AuditService : IAuditService
    {
        private readonly ApplicationDbContext _context;

        public AuditService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(string entityName, int entityId, string action, object? oldVal, object? newVal, string userId)
        {
            var log = new ActivityLog
            {
                EntityName = entityName,
                EntityId = entityId,
                Action = action,
                UserId = userId,
                Timestamp = DateTimeOffset.UtcNow,
                // Serialize the objects to JSON strings
                OldValues = AuditValueSanitizer.Serialize(oldVal),
                NewValues = AuditValueSanitizer.Serialize(newVal)
            };

            _context.ActivityLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
