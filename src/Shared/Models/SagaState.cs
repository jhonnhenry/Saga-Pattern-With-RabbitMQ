namespace Shared.Models;

public enum SagaStatus
{
    CREATED = 0,
    AWAITING_PAYMENT = 1,
    AWAITING_INVENTORY = 2,
    AWAITING_DELIVERY = 3,
    COMPENSATING = 4,
    COMPLETED = 5,
    FAILED = 6
}

/// <summary>
/// Entidade SagaState - gerencia o estado da saga de pedidos
/// </summary>
public class SagaState
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public SagaStatus Status { get; set; } = SagaStatus.CREATED;
    public required string CurrentStep { get; set; }
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Armazena dados contextuais da saga em JSON
    /// Exemplo: { "paymentTransactionId": "123", "reservedItems": [...] }
    /// </summary>
    public string Data { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<SagaEvent> SagaEvents { get; set; } = new();
}

/// <summary>
/// Registro de evento da saga
/// </summary>
public class SagaEvent
{
    public long Id { get; set; }
    public long SagaId { get; set; }
    public SagaState? SagaState { get; set; }
    public required string EventType { get; set; }
    public required string EventData { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
