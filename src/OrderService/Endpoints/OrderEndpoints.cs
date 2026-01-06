using OrderService.Data;
using OrderService.DTOs;
using Shared.Infrastructure;
using Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace OrderService.Endpoints;

public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/orders")
            .WithName("Orders")
            .WithOpenApi();

        group.MapPost("/", CreateOrder)
            .WithName("CreateOrder")
            .WithOpenApi()
            .WithSummary("Cria um novo pedido e inicia a saga");

        group.MapGet("/{orderId}", GetOrderById)
            .WithName("GetOrder")
            .WithOpenApi()
            .WithSummary("Obtém detalhes do pedido pelo ID");

        group.MapGet("/", GetAllOrders)
            .WithName("GetAllOrders")
            .WithOpenApi()
            .WithSummary("Obtém todos os pedidos");
    }

    private static async Task<IResult> CreateOrder(
        CreateOrderRequest request,
        OrderDbContext db,
        MessagePublisher publisher,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("OrderEndpoints");
        logger.LogInformation("Criando pedido para cliente {CustomerId} com {ItemCount} itens",
            request.CustomerId, request.Items.Count);

        try
        {
            var order = new Order
            {
                CustomerId = request.CustomerId,
                Status = OrderStatus.PENDING,
                ShippingAddress = request.ShippingAddress,
                TotalAmount = request.Items.Sum(x => x.Price * x.Quantity),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var item in request.Items)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    Price = item.Price
                });
            }

            db.Orders.Add(order);
            await db.SaveChangesAsync();

            // Publica o evento OrderCreated para iniciar a saga
            var orderCreatedEvent = new OrderCreated
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                TotalAmount = order.TotalAmount,
                ShippingAddress = order.ShippingAddress,
                Items = order.Items.Select(x => new OrderItemDto
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity,
                    Price = x.Price
                }).ToList()
            };

            logger.LogInformation("Publicando evento OrderCreated para OrderId: {OrderId}, RoutingKey: {RoutingKey}, Valor: {Amount}",
                order.Id, EventRoutingKeys.OrderCreated, order.TotalAmount);

            publisher.PublishEvent(orderCreatedEvent, EventRoutingKeys.OrderCreated);

            logger.LogInformation("Pedido criado com ID {OrderId}, evento publicado para iniciar a saga. Evento OrderCreated publicado com routing key: {RoutingKey}",
                order.Id, EventRoutingKeys.OrderCreated);

            return Results.Created($"/api/orders/{order.Id}", new OrderResponse
            {
                OrderId = order.Id,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                CreatedAt = order.CreatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao criar pedido");
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetOrderById(
        long orderId,
        OrderDbContext db,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("OrderEndpoints");
        logger.LogInformation("Buscando pedido {OrderId}", orderId);

        try
        {
            var order = await db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return Results.NotFound(new { error = $"Pedido {orderId} não encontrado"});
            }

            return Results.Ok(new OrderDetailResponse
            {
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Status = order.Status.ToString(),
                TotalAmount = order.TotalAmount,
                ShippingAddress = order.ShippingAddress,
                Items = order.Items.Select(x => new OrderItemResponse
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity,
                    Price = x.Price
                }).ToList(),
                CreatedAt = order.CreatedAt,
                UpdatedAt = order.UpdatedAt
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao buscar pedido {OrderId}", orderId);
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAllOrders(
        OrderDbContext db,
        ILoggerFactory loggerFactory)
    {
        var logger = loggerFactory.CreateLogger("OrderEndpoints");
        logger.LogInformation("Buscando todos os pedidos");

        try
        {
            var orders = await db.Orders.Include(o => o.Items).ToListAsync();

            var response = orders.Select(o => new OrderDetailResponse
            {
                OrderId = o.Id,
                CustomerId = o.CustomerId,
                Status = o.Status.ToString(),
                TotalAmount = o.TotalAmount,
                ShippingAddress = o.ShippingAddress,
                Items = o.Items.Select(x => new OrderItemResponse
                {
                    ProductId = x.ProductId,
                    Quantity = x.Quantity,
                    Price = x.Price
                }).ToList(),
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt
            }).ToList();

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao buscar todos os pedidos");
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
