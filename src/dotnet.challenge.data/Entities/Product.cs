namespace dotnet.challenge.data.Entities;

public class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Units currently available for new reservations.
    /// Decremented when a reservation is created, re-incremented when a reservation is released or expires.
    /// Must never be negative.
    /// </summary>
    public int Stock { get; set; }
}
