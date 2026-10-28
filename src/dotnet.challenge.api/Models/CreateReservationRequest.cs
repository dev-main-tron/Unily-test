using System.ComponentModel.DataAnnotations;

namespace dotnet.challenge.api.Models;

public class CreateReservationRequest
{
    // Nullable so that [Required] can actually fire: on a non-nullable Guid a missing "productId"
    // silently binds to Guid.Empty and the request turns into a 404 instead of a 400.
    [Required]
    public Guid? ProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(1, 3600)]
    public int TtlSeconds { get; set; }
}
