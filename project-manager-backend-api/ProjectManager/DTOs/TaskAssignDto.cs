using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class TaskAssignDto
    {
        [Required, StringLength(450)]
        public string NewAssignedUserId { get; set; } = string.Empty;
    }
}
