namespace Shared.Models;

/// <summary>
/// Comando para processar um pagamento
/// </summary>
public class ProcessPaymentCommand : Command
{
    public long CustomerId { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "CreditCard";
}

/// <summary>
/// Comando para liberar/reembolsar um pagamento
/// </summary>
public class ReleasePaymentCommand : Command
{
    public decimal Amount { get; set; }
    public required string OriginalTransactionId { get; set; }
    public string Reason { get; set; } = "Saga compensation";
}
