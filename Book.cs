using System.ComponentModel.DataAnnotations;

namespace lab8;

public class Book
{
    public int Id { get; set; }

    [Required(ErrorMessage = "A title is required.")]
    [StringLength(150, ErrorMessage = "Keep the title under 150 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "An author is required.")]
    [StringLength(100, ErrorMessage = "Keep the author name under 100 characters.")]
    public string Author { get; set; } = string.Empty;

    [Required(ErrorMessage = "An ISBN is required.")]
    [StringLength(25, ErrorMessage = "Keep the ISBN under 25 characters.")]
    public string ISBN { get; set; } = string.Empty;
}
