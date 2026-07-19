using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class ConfirmEmailDto
    {
        [Required, StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required, StringLength(4096)]
        public string Token { get; set; } = string.Empty;
    }
}
