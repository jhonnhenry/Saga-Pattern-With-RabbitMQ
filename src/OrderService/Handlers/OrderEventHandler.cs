using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderService.Data;
using Shared.Models;

namespace OrderService.Handlers;

public class OrderEventHandler
{
    private readonly OrderDbContext _db;
    private readonly ILogger<OrderEventHandler> _logger;

    public OrderEventHandler(OrderDbContext db, ILogger<OrderEventHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task HandleOrderCompleted(OrderCompleted @event)
    {
        _logger.LogInformation("Processando evento OrderCompleted para pedido {OrderId}, correlationId: {CorrelationId}",
            @event.OrderId, @event.CorrelationId);

        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == @event.OrderId);

            if (order == null)
            {
                _logger.LogWarning("Pedido {OrderId} não encontrado para atualização de status", @event.OrderId);
                return;
            }

            order.Status = OrderStatus.COMPLETED;
            order.UpdatedAt = DateTime.UtcNow;

            _db.Orders.Update(order);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Pedido {OrderId} atualizado para status COMPLETED, correlationId: {CorrelationId}",
                @event.OrderId, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento OrderCompleted para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }

    public async Task HandleOrderFailed(OrderFailed @event)
    {
        _logger.LogInformation("Processando evento OrderFailed para pedido {OrderId}, razão: {Reason}, correlationId: {CorrelationId}",
            @event.OrderId, @event.Reason, @event.CorrelationId);

        try
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == @event.OrderId);

            if (order == null)
            {
                _logger.LogWarning("Pedido {OrderId} não encontrado para atualização de status", @event.OrderId);
                return;
            }

            order.Status = OrderStatus.FAILED;
            order.UpdatedAt = DateTime.UtcNow;

            _db.Orders.Update(order);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Pedido {OrderId} atualizado para status FAILED, correlationId: {CorrelationId}",
                @event.OrderId, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar evento OrderFailed para pedido {OrderId}", @event.OrderId);
            throw;
        }
    }
}
