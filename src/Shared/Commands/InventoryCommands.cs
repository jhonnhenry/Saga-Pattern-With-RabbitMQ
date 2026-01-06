namespace Shared.Models;

/// <summary>
/// Comando para reservar inventário
/// </summary>
public class ReserveInventoryCommand : Command
{
    public List<InventoryItemDto> Items { get; set; } = new();
}

/// <summary>
/// Comando para liberar inventário reservado
/// </summary>
public class ReleaseInventoryCommand : Command
{
    public List<InventoryItemDto> Items { get; set; } = new();
    public string Reason { get; set; } = "Saga compensation";
}

/// <summary>
/// DTO para item de inventário em comandos
/// </summary>
public class InventoryItemDto
{
    public long ProductId { get; set; }
    public int Quantity { get; set; }
}
