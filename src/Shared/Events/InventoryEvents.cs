namespace Shared.Models;

/// <summary>
/// Publicado quando o inventário é reservado com sucesso
/// </summary>
public class InventoryReserved : DomainEvent
{
    public List<ReservedItemDto> ReservedItems { get; set; } = new();
    public DateTime ReservedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando a reserva de inventário falha
/// </summary>
public class InventoryReservationFailed : DomainEvent
{
    public required string Reason { get; set; }
    public List<FailedItemDto> FailedItems { get; set; } = new();
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o inventário reservado é liberado (compensação)
/// </summary>
public class InventoryReleased : DomainEvent
{
    public List<ReleasedItemDto> ReleasedItems { get; set; } = new();
    public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// DTO para item reservado
/// </summary>
public class ReservedItemDto
{
    public long ProductId { get; set; }
    public int ReservedQuantity { get; set; }
}

/// <summary>
/// DTO para item que falhou na reserva
/// </summary>
public class FailedItemDto
{
    public long ProductId { get; set; }
    public int RequestedQuantity { get; set; }
    public int AvailableQuantity { get; set; }
}

/// <summary>
/// DTO para item liberado
/// </summary>
public class ReleasedItemDto
{
    public long ProductId { get; set; }
    public int ReleasedQuantity { get; set; }
}
