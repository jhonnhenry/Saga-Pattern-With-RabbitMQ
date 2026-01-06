using SagaOrchestrator.Data;
using SagaOrchestrator.Sagas;
using Shared.Infrastructure;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace SagaOrchestrator.Handlers;

public class SagaOrchestrationHandler
{
    private readonly SagaDbContext _db;
    private readonly MessagePublisher _publisher;
    private readonly OrderSaga _orderSaga;
    private readonly ILogger<SagaOrchestrationHandler> _logger;

    public SagaOrchestrationHandler(ILogger<SagaOrchestrationHandler> logger, SagaDbContext db, MessagePublisher publisher, ILogger<OrderSaga> sagaLogger)
    {
        _logger = logger;
        _db = db;
        _publisher = publisher;
        _orderSaga = new OrderSaga(sagaLogger);
    }

    // Manipuladores de Eventos

    public async Task HandleOrderCreated(OrderCreated @event)
    {
        _logger.LogInformation("Processando evento OrderCreated para pedido {OrderId}, correlationId {CorrelationId}, valor {Amount}",
            @event.OrderId, @event.CorrelationId, @event.TotalAmount);

        try
        {
            _logger.LogInformation("Criando estado da Saga para pedido {OrderId}", @event.OrderId);
            // Criar estado da Saga
            var sagaState = new SagaState
            {
                OrderId = @event.OrderId,
                Status = SagaStatus.CREATED,
                CurrentStep = "OrderCreated",
                Data = JsonSerializer.Serialize(new { amount = @event.TotalAmount }),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.SagaStates.Add(sagaState);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Estado da Saga criado com Id: {SagaId}", sagaState.Id);

            // Registrar evento
            await RecordSagaEvent(sagaState.Id, "OrderCreated", @event);
            _logger.LogInformation("Evento registrado para saga {SagaId}", sagaState.Id);

            // Transicionar para AWAITING_PAYMENT e publicar ProcessPaymentCommand
            sagaState.Status = SagaStatus.AWAITING_PAYMENT;
            sagaState.CurrentStep = "ProcessPaymentCommand";
            sagaState.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(sagaState);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Saga {SagaId} transitou para AWAITING_PAYMENT", sagaState.Id);

            var paymentCommand = _orderSaga.CreatePaymentCommand(sagaState, @event);
            _logger.LogInformation("Publicando comando de pagamento com chave de roteamento: {RoutingKey}", CommandRoutingKeys.Payment);
            _publisher.PublishCommand(paymentCommand, CommandRoutingKeys.Payment);

            _logger.LogInformation("Saga {SagaId} transitou para AWAITING_PAYMENT, comando de pagamento publicado", sagaState.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento OrderCreated para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandlePaymentCompleted(PaymentCompleted @event)
    {
        _logger.LogInformation("Processando evento PaymentCompleted para pedido {OrderId}, correlationId {CorrelationId}",
            @event.OrderId, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null)
            {
                _logger.LogWarning("Saga não encontrada para pedido {OrderId}", @event.OrderId);
                return;
            }

            // Atualizar saga data com ID de transação
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();
            data["paymentTransactionId"] = @event.TransactionId;
            saga.Data = JsonSerializer.Serialize(data);

            // Registrar evento
            await RecordSagaEvent(saga.Id, "PaymentCompleted", @event);

            // Transicionar para AWAITING_INVENTORY
            saga.Status = SagaStatus.AWAITING_INVENTORY;
            saga.CurrentStep = "ReserveInventoryCommand";
            saga.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            // Publicar comando de inventário
            var orderCreatedEvent = await GetOrderCreatedEvent(saga.Id);
            var inventoryCommand = _orderSaga.CreateInventoryCommand(saga, orderCreatedEvent);
            _publisher.PublishCommand(inventoryCommand, CommandRoutingKeys.Inventory);

            _logger.LogInformation("Saga {SagaId} transitou para AWAITING_INVENTORY, comando de inventário publicado", saga.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento PaymentCompleted para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandlePaymentFailed(PaymentFailed @event)
    {
        _logger.LogInformation("Processando evento PaymentFailed para pedido {OrderId}, razão: {Reason}, correlationId {CorrelationId}",
            @event.OrderId, @event.Reason, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registrar evento
            await RecordSagaEvent(saga.Id, "PaymentFailed", @event);

            // Marcar saga como falha
            saga.Status = SagaStatus.FAILED;
            saga.CurrentStep = "PaymentFailed";
            saga.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            // Publicar evento OrderFailed para notificar OrderService
            var orderFailedEvent = new OrderFailed
            {
                OrderId = saga.OrderId,
                CorrelationId = @event.CorrelationId,
                Reason = $"Falha no pagamento: {@event.Reason}",
                FailedAt = DateTime.UtcNow
            };
            _publisher.PublishEvent(orderFailedEvent, EventRoutingKeys.OrderFailed);

            _logger.LogWarning("Saga {SagaId} marcada como FAILED e evento OrderFailed publicado", saga.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento PaymentFailed");
            throw;
        }
    }

    public async Task HandlePaymentRefunded(PaymentRefunded @event)
    {
        _logger.LogInformation("Processando evento PaymentRefunded para pedido {OrderId}, transação original: {OriginalTransactionId}, transação de reembolso: {RefundTransactionId}, correlationId {CorrelationId}",
            @event.OrderId, @event.OriginalTransactionId, @event.RefundTransactionId, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registra evento
            await RecordSagaEvent(saga.Id, "PaymentRefunded", @event);

            // Atualiza saga data com refund transaction ID
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();
            data["refundTransactionId"] = @event.RefundTransactionId;

            // NOVO: Marca compensação de pagamento como concluída
            if (data.ContainsKey("compensationProgress"))
            {
                var progressJson = data["compensationProgress"].ToString() ?? "";
                var progress = JsonSerializer.Deserialize<Dictionary<string, bool>>(progressJson) ?? new();
                progress["paymentRefunded"] = true;
                data["compensationProgress"] = JsonSerializer.SerializeToElement(progress);
            }

            saga.Data = JsonSerializer.Serialize(data);
            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Saga {SagaId} pagamento reembolsado - transação de reembolso: {RefundTransactionId}",
                saga.Id, @event.RefundTransactionId);

            // NOVO: Verifica se todas as compensações completaram
            await CheckAndCompleteCompensation(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento PaymentRefunded para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleInventoryReserved(InventoryReserved @event)
    {
        _logger.LogInformation("Processando evento InventoryReserved para pedido {OrderId}, itens reservados: {Count}, correlationId {CorrelationId}",
            @event.OrderId, @event.ReservedItems.Count, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registrar evento
            await RecordSagaEvent(saga.Id, "InventoryReserved", @event);

            // Transicionar para AWAITING_DELIVERY
            saga.Status = SagaStatus.AWAITING_DELIVERY;
            saga.CurrentStep = "ScheduleDeliveryCommand";
            saga.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            // Publicar comando de entrega
            var orderCreatedEvent = await GetOrderCreatedEvent(saga.Id);
            var deliveryCommand = _orderSaga.CreateDeliveryCommand(saga, orderCreatedEvent);
            _publisher.PublishCommand(deliveryCommand, CommandRoutingKeys.Delivery);

            _logger.LogInformation("Saga {SagaId} transitou para AWAITING_DELIVERY, comando de entrega publicado", saga.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento InventoryReserved");
            throw;
        }
    }

    public async Task HandleInventoryReservationFailed(InventoryReservationFailed @event)
    {
        _logger.LogInformation("Processando evento InventoryReservationFailed para pedido {OrderId}, correlationId {CorrelationId}",
            @event.OrderId, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registrar evento
            await RecordSagaEvent(saga.Id, "InventoryReservationFailed", @event);

            // Iniciar compensação
            await StartCompensation(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento InventoryReservationFailed");
            throw;
        }
    }

    public async Task HandleInventoryReleased(InventoryReleased @event)
    {
        _logger.LogInformation("Processando evento InventoryReleased para pedido {OrderId}, itens liberados: {Count}, correlationId {CorrelationId}",
            @event.OrderId, @event.ReleasedItems.Count, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registra evento
            await RecordSagaEvent(saga.Id, "InventoryReleased", @event);

            // NOVO: Marca compensação de inventário como concluída
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();

            if (data.ContainsKey("compensationProgress"))
            {
                var progressJson = data["compensationProgress"].ToString() ?? "";
                var progress = JsonSerializer.Deserialize<Dictionary<string, bool>>(progressJson) ?? new();
                progress["inventoryReleased"] = true;
                data["compensationProgress"] = JsonSerializer.SerializeToElement(progress);

                saga.Data = JsonSerializer.Serialize(data);
                _db.SagaStates.Update(saga);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Saga {SagaId} inventário liberado durante compensação", saga.Id);

            // NOVO: Verifica se todas as compensações completaram
            await CheckAndCompleteCompensation(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento InventoryReleased para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleDeliveryScheduled(DeliveryScheduled @event)
    {
        _logger.LogInformation("Processando evento DeliveryScheduled para pedido {OrderId}, correlationId {CorrelationId}",
            @event.OrderId, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registrar evento
            await RecordSagaEvent(saga.Id, "DeliveryScheduled", @event);

            // Marcar saga como COMPLETED
            saga.Status = SagaStatus.COMPLETED;
            saga.CurrentStep = "Completed";
            saga.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            // Publicar evento OrderCompleted para notificar OrderService
            var orderCompletedEvent = _orderSaga.CreateOrderCompletedEvent(saga);
            _publisher.PublishEvent(orderCompletedEvent, EventRoutingKeys.OrderCompleted);

            _logger.LogInformation("Saga {SagaId} marcada como COMPLETED, evento OrderCompleted publicado", saga.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento DeliveryScheduled");
            throw;
        }
    }

    public async Task HandleDeliverySchedulingFailed(DeliverySchedulingFailed @event)
    {
        _logger.LogInformation("Processando evento DeliverySchedulingFailed para pedido {OrderId}, correlationId {CorrelationId}",
            @event.OrderId, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registrar evento
            await RecordSagaEvent(saga.Id, "DeliverySchedulingFailed", @event);

            // Iniciar compensação
            await StartCompensation(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento DeliverySchedulingFailed");
            throw;
        }
    }

    public async Task HandleDeliveryCancelled(DeliveryCancelled @event)
    {
        _logger.LogInformation("Processando evento DeliveryCancelled para pedido {OrderId}, razão: {Reason}, correlationId {CorrelationId}",
            @event.OrderId, @event.CancellationReason, @event.CorrelationId);

        try
        {
            var saga = await _db.SagaStates.FirstOrDefaultAsync(s => s.OrderId == @event.OrderId);
            if (saga == null) return;

            // Registra evento
            await RecordSagaEvent(saga.Id, "DeliveryCancelled", @event);

            // NOVO: Marca compensação de entrega como concluída
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();

            if (data.ContainsKey("compensationProgress"))
            {
                var progressJson = data["compensationProgress"].ToString() ?? "";
                var progress = JsonSerializer.Deserialize<Dictionary<string, bool>>(progressJson) ?? new();
                progress["deliveryCancelled"] = true;
                data["compensationProgress"] = JsonSerializer.SerializeToElement(progress);

                saga.Data = JsonSerializer.Serialize(data);
                _db.SagaStates.Update(saga);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Saga {SagaId} entrega cancelada durante compensação", saga.Id);

            // NOVO: Verifica se todas as compensações completaram
            await CheckAndCompleteCompensation(saga);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento DeliveryCancelled para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    // Compensação

    private async Task StartCompensation(SagaState saga)
    {
        _logger.LogInformation("Iniciando compensação para saga {SagaId}", saga.Id);

        saga.Status = SagaStatus.COMPENSATING;
        saga.CurrentStep = "Compensating";
        saga.UpdatedAt = DateTime.UtcNow;

        // NOVO: Inicializar rastreamento de compensações no campo Data
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();
        data["compensationProgress"] = JsonSerializer.SerializeToElement(new Dictionary<string, bool>
        {
            { "paymentRefunded", false },
            { "inventoryReleased", false },
            { "deliveryCancelled", false }
        });
        saga.Data = JsonSerializer.Serialize(data);

        _db.SagaStates.Update(saga);
        await _db.SaveChangesAsync();

        // Buscar evento original do pedido
        var orderCreatedEvent = await GetOrderCreatedEvent(saga.Id);

        // Liberar em ordem reversa: Entrega -> Inventário -> Pagamento

        var releasePaymentCmd = _orderSaga.CreateReleasePaymentCommand(saga, "");
        _publisher.PublishCommand(releasePaymentCmd, CommandRoutingKeys.Payment);

        var releaseInventoryCmd = _orderSaga.CreateReleaseInventoryCommand(saga, orderCreatedEvent);
        _publisher.PublishCommand(releaseInventoryCmd, CommandRoutingKeys.Inventory);

        var cancelDeliveryCmd = _orderSaga.CreateCancelDeliveryCommand(saga);
        _publisher.PublishCommand(cancelDeliveryCmd, CommandRoutingKeys.Delivery);

        // REMOVIDO: não marca mais como FAILED aqui - aguarda confirmação das compensações
        // saga.Status = SagaStatus.FAILED;
        // _db.SagaStates.Update(saga);
        // await _db.SaveChangesAsync();

        _logger.LogInformation("Comandos de compensação publicados para saga {SagaId}, aguardando confirmações", saga.Id);
    }

    /// <summary>
    /// Verifica se todas as compensações foram concluídas e marca saga como FAILED
    /// </summary>
    private async Task CheckAndCompleteCompensation(SagaState saga)
    {
        // Parse do campo Data
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(saga.Data) ?? new();

        if (!data.ContainsKey("compensationProgress"))
        {
            _logger.LogWarning("Saga {SagaId} não possui rastreamento de compensationProgress", saga.Id);
            return;
        }

        // Extrair progresso de compensação
        var progressJson = data["compensationProgress"].ToString() ?? "";
        var progress = JsonSerializer.Deserialize<Dictionary<string, bool>>(progressJson);

        if (progress == null)
        {
            _logger.LogError("Falha ao desserializar compensationProgress para saga {SagaId}", saga.Id);
            return;
        }

        // Verificar se TODAS as compensações foram concluídas
        bool allCompleted = progress["paymentRefunded"] &&
                           progress["inventoryReleased"] &&
                           progress["deliveryCancelled"];

        if (allCompleted)
        {
            _logger.LogInformation("Todas as compensações concluídas para saga {SagaId}, marcando como FAILED", saga.Id);

            saga.Status = SagaStatus.FAILED;
            saga.CurrentStep = "CompensationCompleted";
            saga.UpdatedAt = DateTime.UtcNow;

            _db.SagaStates.Update(saga);
            await _db.SaveChangesAsync();

            // Publicar evento OrderFailed para notificar OrderService
            var orderFailedEvent = new OrderFailed
            {
                OrderId = saga.OrderId,
                CorrelationId = Guid.NewGuid().ToString(),
                Reason = "Saga compensação concluída",
                FailedAt = DateTime.UtcNow
            };
            _publisher.PublishEvent(orderFailedEvent, EventRoutingKeys.OrderFailed);

            _logger.LogInformation("Saga {SagaId} marcada como FAILED e evento OrderFailed publicado", saga.Id);
        }
        else
        {
            _logger.LogInformation("Saga {SagaId} compensações em andamento: Payment={PaymentRefunded}, Inventory={InventoryReleased}, Delivery={DeliveryCancelled}",
                saga.Id, progress["paymentRefunded"], progress["inventoryReleased"], progress["deliveryCancelled"]);
        }
    }

    // Auxiliares

    private async Task RecordSagaEvent(long sagaId, string eventType, object eventData)
    {
        var sagaEvent = new SagaEvent
        {
            SagaId = sagaId,
            EventType = eventType,
            EventData = JsonSerializer.Serialize(eventData),
            CreatedAt = DateTime.UtcNow
        };

        _db.SagaEvents.Add(sagaEvent);
        await _db.SaveChangesAsync();
    }

    private async Task<OrderCreated> GetOrderCreatedEvent(long sagaId)
    {
        var sagaEvent = await _db.SagaEvents
            .Where(e => e.SagaId == sagaId && e.EventType == "OrderCreated")
            .FirstOrDefaultAsync();

        if (sagaEvent == null)
            throw new InvalidOperationException($"Evento OrderCreated não encontrado para saga {sagaId}");

        var @event = JsonSerializer.Deserialize<OrderCreated>(sagaEvent.EventData);
        if (@event == null)
            throw new InvalidOperationException($"Falha ao desserializar evento OrderCreated para saga {sagaId}");

        return @event;
    }
}
