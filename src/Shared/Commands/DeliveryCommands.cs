namespace Shared.Models;

/// <summary>
/// Comando para agendar uma entrega
/// </summary>
public class ScheduleDeliveryCommand : Command
{
    public required string ShippingAddress { get; set; }
    public DateTime PreferredDeliveryDate { get; set; }
    public required string DeliveryNotes { get; set; }
}

/// <summary>
/// Comando para cancelar uma entrega agendada
/// </summary>
public class CancelDeliveryCommand : Command
{
    public string CancellationReason { get; set; } = "Saga compensation";
}
