using Shared.Models;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SagaOrchestrator.Sagas;

public class OrderSaga
{
    private readonly ILogger<OrderSaga> _logger;

    public OrderSaga(ILogger<OrderSaga> logger)
    {
        _logger = logger;
    }

    public SagaStatus GetNextState(SagaStatus currentStatus)
    {
        return currentStatus switch
        {
            SagaStatus.CREATED => SagaStatus.AWAITING_PAYMENT,
            SagaStatus.AWAITING_PAYMENT => SagaStatus.AWAITING_INVENTORY,
            SagaStatus.AWAITING_INVENTORY => SagaStatus.AWAITING_DELIVERY,
            SagaStatus.AWAITING_DELIVERY => SagaStatus.COMPLETED,
            _ => SagaStatus.FAILED
        };
    }

    public ProcessPaymentCommand CreatePaymentCommand(SagaState saga, OrderCreated orderEvent)
    {
        _logger.LogInformation("Criando comando de pagamento para a saga {SagaId}, pedido {OrderId}", saga.Id, saga.OrderId);

        return new ProcessPaymentCommand
        {
            OrderId = saga.OrderId,
            Amount = orderEvent.TotalAmount,
            PaymentMethod = "CreditCard",
            CorrelationId = orderEvent.CorrelationId
        };
    }

    public ReserveInventoryCommand CreateInventoryCommand(SagaState saga, OrderCreated orderEvent)
    {
        _logger.LogInformation("Criando comando de reserva de estoque para a saga {SagaId}, pedido {OrderId}", saga.Id, saga.OrderId);

        return new ReserveInventoryCommand
        {
            OrderId = saga.OrderId,
            Items = orderEvent.Items.Select(x => new InventoryItemDto
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity
            }).ToList(),
            CorrelationId = orderEvent.CorrelationId
        };
    }

    public ScheduleDeliveryCommand CreateDeliveryCommand(SagaState saga, OrderCreated orderEvent)
    {
        _logger.LogInformation("Criando comando de entrega para a saga {SagaId}, pedido {OrderId}", saga.Id, saga.OrderId);

        return new ScheduleDeliveryCommand
        {
            OrderId = saga.OrderId,
            ShippingAddress = orderEvent.ShippingAddress,
            PreferredDeliveryDate = DateTime.UtcNow.AddDays(5),
            DeliveryNotes = $"Order {saga.OrderId} from customer",
            CorrelationId = orderEvent.CorrelationId
        };
    }

    public ReleasePaymentCommand CreateReleasePaymentCommand(SagaState saga, string transactionId)
    {
        _logger.LogInformation("Criando comando de liberação de pagamento para a saga {SagaId}", saga.Id);

        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data);

        return new ReleasePaymentCommand
        {
            OrderId = saga.OrderId,
            Amount = data != null && data.ContainsKey("amount")
                ? decimal.Parse(data["amount"].ToString() ?? "0")
                : 0,
            OriginalTransactionId = transactionId,
            Reason = "Compensação da Saga",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    public ReleaseInventoryCommand CreateReleaseInventoryCommand(SagaState saga, OrderCreated orderEvent)
    {
        _logger.LogInformation("Criando comando de liberação de estoque para a saga {SagaId}", saga.Id);

        return new ReleaseInventoryCommand
        {
            OrderId = saga.OrderId,
            Items = orderEvent.Items.Select(x => new InventoryItemDto
            {
                ProductId = x.ProductId,
                Quantity = x.Quantity
            }).ToList(),
            Reason = "Compensação da Saga",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    public CancelDeliveryCommand CreateCancelDeliveryCommand(SagaState saga)
    {
        _logger.LogInformation("Criando comando de cancelamento de entrega para a saga {SagaId}", saga.Id);

        return new CancelDeliveryCommand
        {
            OrderId = saga.OrderId,
            CancellationReason = "Compensação da Saga",
            CorrelationId = Guid.NewGuid().ToString()
        };
    }

    public OrderCompleted CreateOrderCompletedEvent(SagaState saga)
    {
        _logger.LogInformation("Criando evento OrderCompleted para a saga {SagaId}, pedido {OrderId}", saga.Id, saga.OrderId);

        return new OrderCompleted
        {
            OrderId = saga.OrderId,
            CorrelationId = Guid.NewGuid().ToString(),
            CompletedAt = DateTime.UtcNow
        };
    }
}
