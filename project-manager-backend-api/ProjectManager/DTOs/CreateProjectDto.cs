using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class CreateProjectDto : IValidatableObject
    {
        [Required, StringLength(150, MinimumLength = 2)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(4000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        [Required, StringLength(50)]
        public string Status { get; set; } = string.Empty;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (EndDate < StartDate)
            {
                yield return new ValidationResult(
                    "EndDate must be on or after StartDate.",
                    new[] { nameof(EndDate) });
            }
        }
    }
}
