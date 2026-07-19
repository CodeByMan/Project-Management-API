using System.ComponentModel.DataAnnotations;

namespace ProjectManager.DTOs
{
    public class ChangeUserRoleDto
    {
        [Required, RegularExpression("^(Admin|Manager|Member)$", ErrorMessage = "Role must be Admin, Manager, or Member.")]
        public string NewRole { get; set; } = string.Empty;
    }
}
