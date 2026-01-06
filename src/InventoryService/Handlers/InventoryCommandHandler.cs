using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure;
using Shared.Models;
using InventoryService.Data;

namespace InventoryService.Handlers;

public class InventoryCommandHandler
{
    private readonly InventoryDbContext _db;
    private readonly MessagePublisher _publisher;
    private readonly ILogger<InventoryCommandHandler> _logger;

    public InventoryCommandHandler(ILogger<InventoryCommandHandler> logger, InventoryDbContext db, MessagePublisher publisher)
    {
        _logger = logger;
        _db = db;
        _publisher = publisher;
    }

    public async Task HandleReserveInventoryCommand(ReserveInventoryCommand command)
    {
        _logger.LogInformation("Processando reserva de estoque para o pedido {OrderId}, quantidade de itens: {ItemCount}, correlationId: {CorrelationId}",
            command.OrderId, command.Items.Count, command.CorrelationId);

        // Verifica idempotência
        var existingReservations = await _db.Reservations
            .Where(r => r.OrderId == command.OrderId)
            .ToListAsync();

        if (existingReservations.Any())
        {
            _logger.LogInformation("Reservas já existem para o pedido {OrderId}", command.OrderId);

            var allReserved = existingReservations.All(r => r.Status == "RESERVED");
            if (allReserved)
            {
                PublishInventoryReserved(command, existingReservations);
                return;
            }
        }

        try
        {
            var failedItems = new List<FailedItemDto>();
            var reservedItems = new List<ReservedItemDto>();

            foreach (var item in command.Items)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);

                if (product == null)
                {
                    failedItems.Add(new FailedItemDto
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.Quantity,
                        AvailableQuantity = 0
                    });
                    continue;
                }

                int availableQty = product.AvailableQuantity - product.ReservedQuantity;

                if (availableQty < item.Quantity)
                {
                    failedItems.Add(new FailedItemDto
                    {
                        ProductId = item.ProductId,
                        RequestedQuantity = item.Quantity,
                        AvailableQuantity = availableQty
                    });
                    continue;
                }

                // Reserva o estoque
                product.ReservedQuantity += item.Quantity;
                product.UpdatedAt = DateTime.UtcNow;

                var reservation = new InventoryReservation
                {
                    OrderId = command.OrderId,
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Status = "RESERVED",
                    CreatedAt = DateTime.UtcNow
                };

                _db.Products.Update(product);
                _db.Reservations.Add(reservation);

                reservedItems.Add(new ReservedItemDto
                {
                    ProductId = item.ProductId,
                    ReservedQuantity = item.Quantity
                });
            }

            if (failedItems.Any())
            {
                // Rollback de quaisquer itens reservados
                foreach (var reserved in reservedItems)
                {
                    var product = await _db.Products.FirstAsync(p => p.Id == reserved.ProductId);
                    product.ReservedQuantity -= reserved.ReservedQuantity;
                    _db.Products.Update(product);
                }

                await _db.SaveChangesAsync();
                PublishInventoryReservationFailed(command, failedItems);
                return;
            }

            await _db.SaveChangesAsync();
            PublishInventoryReserved(command, reservedItems);

            _logger.LogInformation("Estoque reservado com sucesso para o pedido {OrderId}, itens reservados: {Count}",
                command.OrderId, reservedItems.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reservar estoque para o pedido {OrderId}", command.OrderId);
            PublishInventoryReservationFailed(command, new List<FailedItemDto>());
            throw;
        }
    }

    public async Task HandleReleaseInventoryCommand(ReleaseInventoryCommand command)
    {
        _logger.LogInformation("Liberando estoque para o pedido {OrderId}, correlationId: {CorrelationId}",
            command.OrderId, command.CorrelationId);

        try
        {
            var reservations = await _db.Reservations
                .Where(r => r.OrderId == command.OrderId && r.Status == "RESERVED")
                .ToListAsync();

            var releasedItems = new List<ReleasedItemDto>();

            foreach (var reservation in reservations)
            {
                var product = await _db.Products.FirstAsync(p => p.Id == reservation.ProductId);
                product.ReservedQuantity -= reservation.Quantity;
                product.UpdatedAt = DateTime.UtcNow;

                reservation.Status = "RELEASED";

                _db.Products.Update(product);
                _db.Reservations.Update(reservation);

                releasedItems.Add(new ReleasedItemDto
                {
                    ProductId = reservation.ProductId,
                    ReleasedQuantity = reservation.Quantity
                });
            }

            await _db.SaveChangesAsync();

            var @event = new InventoryReleased
            {
                OrderId = command.OrderId,
                ReleasedItems = releasedItems,
                CorrelationId = command.CorrelationId
            };

            _publisher.PublishEvent(@event, EventRoutingKeys.InventoryReleased);

            _logger.LogInformation("Estoque liberado para o pedido {OrderId}", command.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao liberar estoque para o pedido {OrderId}", command.OrderId);
            throw;
        }
    }

    private void PublishInventoryReserved(ReserveInventoryCommand command, List<ReservedItemDto> items)
    {
        var @event = new InventoryReserved
        {
            OrderId = command.OrderId,
            ReservedItems = items,
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.InventoryReserved);
    }

    private void PublishInventoryReserved(ReserveInventoryCommand command, List<InventoryReservation> reservations)
    {
        var items = reservations.Select(r => new ReservedItemDto
        {
            ProductId = r.ProductId,
            ReservedQuantity = r.Quantity
        }).ToList();

        PublishInventoryReserved(command, items);
    }

    private void PublishInventoryReservationFailed(ReserveInventoryCommand command, List<FailedItemDto> failedItems)
    {
        var @event = new InventoryReservationFailed
        {
            OrderId = command.OrderId,
            Reason = failedItems.Any() ? "Estoque insuficiente": "Erro ao processar reserva",
            FailedItems = failedItems,
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.InventoryReservationFailed);
    }
}
