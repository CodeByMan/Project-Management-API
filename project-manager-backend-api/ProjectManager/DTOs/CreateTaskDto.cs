using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class CreateTaskDto : IValidatableObject
    {
        [Required, StringLength(200, MinimumLength = 2)]
        public string Title { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required, RegularExpression("^(Low|Medium|High)$", ErrorMessage = "Priority must be Low, Medium, or High.")]
        public string Priority { get; set; } = string.Empty;

        [Required]
        public DateTime DueDate { get; set; }

        [Required, RegularExpression("^(Open|To Do|In Progress|Done)$", ErrorMessage = "Status must be Open, To Do, In Progress, or Done.")]
        public string Status { get; set; } = "Open";

        [Range(1, int.MaxValue)]
        public int ProjectId { get; set; }

        [Required, StringLength(450)]
        public string AssignedUserId { get; set; } = string.Empty;

        public List<int>? TagIds { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (TagIds?.Any(id => id <= 0) == true)
            {
                yield return new ValidationResult("Tag IDs must be positive integers.", new[] { nameof(TagIds) });
            }
        }
    }
}
