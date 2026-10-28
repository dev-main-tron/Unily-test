namespace dotnet.challenge.data.Entities;

public enum ReservationStatus
{
    Active,
    Confirmed,
    Released,
    Expired
}

public class Reservation
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public ReservationStatus Status { get; set; }
}
