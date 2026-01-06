namespace Shared.Models;

/// <summary>
/// Publicado quando a entrega é agendada com sucesso
/// </summary>
public class DeliveryScheduled : DomainEvent
{
    public required string ShippingAddress { get; set; }
    public DateTime EstimatedDeliveryDate { get; set; }
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o agendamento da entrega falha
/// </summary>
public class DeliverySchedulingFailed : DomainEvent
{
    public required string Reason { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando a entrega é cancelada (compensação)
/// </summary>
public class DeliveryCancelled : DomainEvent
{
    public required string CancellationReason { get; set; }
    public DateTime CancelledAt { get; set; } = DateTime.UtcNow;
}
