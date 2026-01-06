namespace Shared.Models;

/// <summary>
/// Publicado quando um novo pedido é criado
/// </summary>
public class OrderCreated : DomainEvent
{
    public long CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public required string ShippingAddress { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Publicado quando o pedido é completado com sucesso
/// </summary>
public class OrderCompleted : DomainEvent
{
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o pedido falha
/// </summary>
public class OrderFailed : DomainEvent
{
    public required string Reason { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o pedido iniciou o processo de compensação
/// </summary>
public class OrderCompensationStarted : DomainEvent
{
    public required string FailureReason { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO para itens do pedido
/// </summary>
public class OrderItemDto
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}
