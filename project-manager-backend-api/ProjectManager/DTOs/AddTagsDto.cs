using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class AddTagsDto : IValidatableObject
    {
        public List<int?>? TagIds { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (TagIds?.Any(id => id is null or <= 0) == true)
            {
                yield return new ValidationResult("Tag IDs must be positive integers.", new[] { nameof(TagIds) });
            }
        }
    }
}
