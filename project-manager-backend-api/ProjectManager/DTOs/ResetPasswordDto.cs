using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class ResetPasswordDto
    {
        [Required, StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, StringLength(4096)]
        public string Token { get; set; } = string.Empty;
    }
}
