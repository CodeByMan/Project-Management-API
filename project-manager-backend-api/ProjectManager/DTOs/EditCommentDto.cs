using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class EditCommentDto
    {
        [Required, StringLength(2000, MinimumLength = 1)]
        public string Content { get; set; } = string.Empty;
    }
}
