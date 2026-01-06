namespace OrderService.DTOs;

public class CreateOrderRequest
{
    public long CustomerId { get; set; }
    public List<OrderItemRequest> Items { get; set; } = new();
    public required string ShippingAddress { get; set; }
}

public class OrderItemRequest
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class OrderResponse
{
    public long OrderId { get; set; }
    public required string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderDetailResponse
{
    public long OrderId { get; set; }
    public long CustomerId { get; set; }
    public required string Status { get; set; }
    public decimal TotalAmount { get; set; }
    public required string ShippingAddress { get; set; }
    public List<OrderItemResponse> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderItemResponse
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
