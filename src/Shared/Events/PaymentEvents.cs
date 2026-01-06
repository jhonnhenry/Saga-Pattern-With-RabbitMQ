namespace Shared.Models;

/// <summary>
/// Publicado quando o pagamento é processado com sucesso
/// </summary>
public class PaymentCompleted : DomainEvent
{
    public decimal Amount { get; set; }
    public required string TransactionId { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o processamento do pagamento falha
/// </summary>
public class PaymentFailed : DomainEvent
{
    public decimal Amount { get; set; }
    public required string Reason { get; set; }
    public DateTime FailedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Publicado quando o pagamento é reembolsado (compensação)
/// </summary>
public class PaymentRefunded : DomainEvent
{
    public decimal Amount { get; set; }
    public required string OriginalTransactionId { get; set; }
    public required string RefundTransactionId { get; set; }
    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
}
