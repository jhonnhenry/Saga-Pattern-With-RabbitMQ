namespace Shared.Models;

/// <summary>
/// Entidade Order - persistida no OrderService
/// </summary>
public class Order
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PENDING;
    public decimal TotalAmount { get; set; }
    public required string ShippingAddress { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Entidade OrderItem - item do pedido
/// </summary>
public class OrderItem
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public Order? Order { get; set; }
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
