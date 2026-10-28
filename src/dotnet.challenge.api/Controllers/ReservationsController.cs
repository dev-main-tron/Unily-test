using dotnet.challenge.api.Models;
using dotnet.challenge.api.Services;
using dotnet.challenge.data.Entities;
using Microsoft.AspNetCore.Mvc;

namespace dotnet.challenge.api.Controllers;

[ApiController]
[Route("api/reservations")]
[Produces("application/json")]
public class ReservationsController : ControllerBase
{
    private readonly IInventoryService _inventory;

    public ReservationsController(IInventoryService inventory)
    {
        _inventory = inventory;
    }

    [HttpPost]
    [ProducesResponseType<Reservation>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request, CancellationToken cancellationToken)
    {
        var (result, reservation) = await _inventory.TryReserveAsync(
            request.ProductId!.Value, request.Quantity, request.TtlSeconds, cancellationToken);

        return result switch
        {
            ReserveResult.Success => CreatedAtAction(nameof(GetReservation), new { id = reservation!.Id }, reservation),
            ReserveResult.ProductNotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Product not found."),
            ReserveResult.InsufficientStock => Problem(statusCode: StatusCodes.Status409Conflict, title: "Insufficient stock."),
            _ => throw new InvalidOperationException($"Unhandled reserve result {result}.")
        };
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType<Reservation>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservation(Guid id, CancellationToken cancellationToken)
    {
        var reservation = await _inventory.GetReservationAsync(id, cancellationToken);
        if (reservation is null)
        {
            return NotFound();
        }
        return Ok(reservation);
    }

    /// <summary>Confirms an Active reservation. Idempotent: confirming a Confirmed reservation returns 200.</summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType<Reservation>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken cancellationToken)
    {
        var result = await _inventory.ConfirmAsync(id, cancellationToken);
        return result switch
        {
            ConfirmResult.Success or ConfirmResult.AlreadyConfirmed =>
                Ok(await _inventory.GetReservationAsync(id, cancellationToken)),
            ConfirmResult.NotFound => NotFound(),
            ConfirmResult.IllegalState => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Reservation is not in a confirmable state.",
                detail: "Only Active, unexpired reservations can be confirmed."),
            _ => throw new InvalidOperationException($"Unhandled confirm result {result}.")
        };
    }

    /// <summary>Releases a reservation and returns its stock. Idempotent: Released/Expired returns 204.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Release(Guid id, CancellationToken cancellationToken)
    {
        var result = await _inventory.ReleaseAsync(id, cancellationToken);
        return result switch
        {
            ReleaseResult.Success or ReleaseResult.AlreadyReleased => NoContent(),
            ReleaseResult.NotFound => NotFound(),
            _ => throw new InvalidOperationException($"Unhandled release result {result}.")
        };
    }
}
