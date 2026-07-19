using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class AdminCreateUserDto
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
        public string Password { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required, RegularExpression("^(Admin|Manager|Member)$", ErrorMessage = "Role must be Admin, Manager, or Member.")]
        public string Role { get; set; } = string.Empty;
    }
}
