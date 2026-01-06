namespace Shared.Infrastructure;

/// <summary>
/// Configurações para conexão com RabbitMQ
/// </summary>
public class RabbitMQSettings
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";

    /// <summary>
    /// Timeout em milissegundos para operações
    /// </summary>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Nomes das exchanges no RabbitMQ
/// </summary>
public static class ExchangeNames
{
    public const string Commands = "saga.commands";
    public const string Events = "saga.events";
    public const string DeadLetter = "saga.dlq";
}

/// <summary>
/// Nomes das queues por serviço
/// </summary>
public static class QueueNames
{
    public const string OrderCommands = "order.commands";
    public const string PaymentCommands = "payment.commands";
    public const string InventoryCommands = "inventory.commands";
    public const string DeliveryCommands = "delivery.commands";
    public const string SagaOrchestratorEvents = "saga.orchestrator.events";
    public const string OrderEvents = "order.events";
    public const string DeadLetter = "saga.dlq";
}

/// <summary>
/// Routing keys para direct exchange (comandos)
/// </summary>
public static class CommandRoutingKeys
{
    public const string Order = "order";
    public const string Payment = "payment";
    public const string Inventory = "inventory";
    public const string Delivery = "delivery";
}

/// <summary>
/// Routing key patterns para topic exchange (eventos)
/// </summary>
public static class EventRoutingPatterns
{
    public const string AllEvents = "saga.events.#";
    public const string OrderEvents = "saga.events.order.*";
    public const string PaymentEvents = "saga.events.payment.*";
    public const string InventoryEvents = "saga.events.inventory.*";
    public const string DeliveryEvents = "saga.events.delivery.*";
}

/// <summary>
/// Routing keys específicas para eventos
/// </summary>
public static class EventRoutingKeys
{
    // Eventos de Pedido
    public const string OrderCreated = "saga.events.order.created";
    public const string OrderCompleted = "saga.events.order.completed";
    public const string OrderFailed = "saga.events.order.failed";
    public const string OrderCompensationStarted = "saga.events.order.compensation.started";

    // Eventos de Pagamento
    public const string PaymentCompleted = "saga.events.payment.completed";
    public const string PaymentFailed = "saga.events.payment.failed";
    public const string PaymentRefunded = "saga.events.payment.refunded";

    // Eventos de Estoque
    public const string InventoryReserved = "saga.events.inventory.reserved";
    public const string InventoryReservationFailed = "saga.events.inventory.reservation.failed";
    public const string InventoryReleased = "saga.events.inventory.released";

    // Eventos de Entrega
    public const string DeliveryScheduled = "saga.events.delivery.scheduled";
    public const string DeliverySchedulingFailed = "saga.events.delivery.scheduling.failed";
    public const string DeliveryCancelled = "saga.events.delivery.cancelled";

    // Eventos de DLQ
    public const string DLQ = "saga.events.dlq";

}
