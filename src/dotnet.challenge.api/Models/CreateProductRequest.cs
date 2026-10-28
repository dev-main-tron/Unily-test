using System.ComponentModel.DataAnnotations;

namespace dotnet.challenge.api.Models;

public class CreateProductRequest
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    public int Stock { get; set; }
}
