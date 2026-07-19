using System.ComponentModel.DataAnnotations;

public class UpdateProfileDto
{
    [Required, StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Password), StringLength(100)]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
    public string? NewPassword { get; set; }
}
