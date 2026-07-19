using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class ProjectMemberDto
    {
        [Required, StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
    }
}
