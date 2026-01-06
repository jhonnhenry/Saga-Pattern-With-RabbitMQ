namespace Shared.Models;

/// <summary>
/// Base class para todas as mensagens (eventos e comandos)
/// </summary>
public abstract class MessageBase
{
    /// <summary>
    /// ID único para rastreamento da mensagem através da saga
    /// </summary>
    public string CorrelationId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Timestamp de quando a mensagem foi criada
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// ID do pedido relacionado
    /// </summary>
    public long OrderId { get; set; }
}

/// <summary>
/// Base class para eventos de domínio
/// </summary>
public abstract class DomainEvent : MessageBase
{
}

/// <summary>
/// Base class para comandos
/// </summary>
public abstract class Command : MessageBase
{
}
