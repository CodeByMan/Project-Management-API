using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class LoginDto
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100)]
        public string Password { get; set; } = string.Empty;
    }
}
