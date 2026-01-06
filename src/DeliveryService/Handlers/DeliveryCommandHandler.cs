using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure;
using Shared.Models;
using DeliveryService.Data;

namespace DeliveryService.Handlers;

public class DeliveryCommandHandler
{
    private readonly DeliveryDbContext _db;
    private readonly MessagePublisher _publisher;
    private readonly ILogger<DeliveryCommandHandler> _logger;

    public DeliveryCommandHandler(ILogger<DeliveryCommandHandler> logger, DeliveryDbContext db, MessagePublisher publisher)
    {
        _logger = logger;
        _db = db;
        _publisher = publisher;
    }

    public async Task HandleScheduleDeliveryCommand(ScheduleDeliveryCommand command)
    {
        _logger.LogInformation("Agendando entrega para o pedido {OrderId}, endereço: {Address}, correlationId: {CorrelationId}",
            command.OrderId, command.ShippingAddress, command.CorrelationId);

        // Verifica idempotência
        var existing = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == command.OrderId);
        if (existing != null)
        {
            _logger.LogInformation("Entrega já agendada para o pedido {OrderId}, status: {Status}", command.OrderId, existing.Status);

            if (existing.Status == "SCHEDULED")
            {
                PublishDeliveryScheduled(command);
            }
            return;
        }

        try
        {
            var delivery = new Delivery
            {
                OrderId = command.OrderId,
                Status = "SCHEDULED",
                ShippingAddress = command.ShippingAddress,
                EstimatedDeliveryDate = command.PreferredDeliveryDate != DateTime.MinValue
                    ? command.PreferredDeliveryDate
                    : DateTime.UtcNow.AddDays(5),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.Deliveries.Add(delivery);
            await _db.SaveChangesAsync();

            PublishDeliveryScheduled(command);

            _logger.LogInformation("Entrega agendada com sucesso para o pedido {OrderId}, data estimada: {EstimatedDate}",
                command.OrderId, delivery.EstimatedDeliveryDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao agendar entrega para o pedido {OrderId}", command.OrderId);
            PublishDeliverySchedulingFailed(command, $"Error: {ex.Message}");
            throw;
        }
    }

    public async Task HandleCancelDeliveryCommand(CancelDeliveryCommand command)
    {
        _logger.LogInformation("Cancelando entrega para o pedido {OrderId}, correlationId: {CorrelationId}",
            command.OrderId, command.CorrelationId);

        try
        {
            var delivery = await _db.Deliveries.FirstOrDefaultAsync(d => d.OrderId == command.OrderId);

            if (delivery != null)
            {
                delivery.Status = "CANCELLED";
                delivery.UpdatedAt = DateTime.UtcNow;

                _db.Deliveries.Update(delivery);
                await _db.SaveChangesAsync();

                _logger.LogInformation("Entrega cancelada para o pedido {OrderId}", command.OrderId);
            }
            else
            {
                _logger.LogWarning("Entrega não encontrada para o pedido {OrderId}, mas publicando evento de cancelamento mesmo assim", command.OrderId);
            }

            // Sempre publicar DeliveryCancelled, mesmo se não havia entrega registrada
            // Isso é importante para a compensação da saga completar corretamente
            var @event = new DeliveryCancelled
            {
                OrderId = command.OrderId,
                CancellationReason = command.CancellationReason,
                CorrelationId = command.CorrelationId
            };

            _publisher.PublishEvent(@event, EventRoutingKeys.DeliveryCancelled);

            _logger.LogInformation("Evento DeliveryCancelled publicado para o pedido {OrderId}", command.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cancelar entrega para o pedido {OrderId}", command.OrderId);
            throw;
        }
    }

    private void PublishDeliveryScheduled(ScheduleDeliveryCommand command)
    {
        var @event = new DeliveryScheduled
        {
            OrderId = command.OrderId,
            ShippingAddress = command.ShippingAddress,
            EstimatedDeliveryDate = command.PreferredDeliveryDate != DateTime.MinValue
                ? command.PreferredDeliveryDate
                : DateTime.UtcNow.AddDays(5),
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.DeliveryScheduled);
    }

    private void PublishDeliverySchedulingFailed(ScheduleDeliveryCommand command, string reason)
    {
        var @event = new DeliverySchedulingFailed
        {
            OrderId = command.OrderId,
            Reason = reason,
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.DeliverySchedulingFailed);
    }
}
