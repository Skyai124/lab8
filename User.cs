using System.ComponentModel.DataAnnotations;

namespace lab8;

public class User
{
    public int Id { get; set; }

    [Required(ErrorMessage = "A name is required.")]
    [StringLength(100, ErrorMessage = "Keep the name under 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "An email address is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150, ErrorMessage = "Keep the email under 150 characters.")]
    public string Email { get; set; } = string.Empty;
}
