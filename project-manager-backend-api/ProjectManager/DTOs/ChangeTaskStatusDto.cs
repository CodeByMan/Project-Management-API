using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class ChangeTaskStatusDto
    {
        [Required, RegularExpression("^(Open|To Do|In Progress|Done)$", ErrorMessage = "Status must be Open, To Do, In Progress, or Done.")]
        public string NewStatus { get; set; } = string.Empty;
    }
}
