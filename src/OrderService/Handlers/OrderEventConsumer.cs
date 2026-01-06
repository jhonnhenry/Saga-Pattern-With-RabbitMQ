using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Infrastructure;
using Shared.Models;
using System.Text.Json;

namespace OrderService.Handlers;

public class OrderEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<OrderEventConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public OrderEventConsumer(ILogger<OrderEventConsumer> logger, IServiceProvider serviceProvider, RabbitMQSettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumidor de Eventos de Pedido iniciando...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _logger.LogInformation("Conectado ao RabbitMQ");

            _channel = _connection.CreateModel();
            _logger.LogInformation("Canal criado");

            _channel.BasicQos(0, 1, false);

            var consumer = new AsyncEventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                var correlationId = ea.BasicProperties?.CorrelationId ?? "N/A";

                // Extrair EventType do header
                string eventType = "N/A";
                if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.ContainsKey("EventType"))
                {
                    var eventTypeValue = ea.BasicProperties.Headers["EventType"];
                    if (eventTypeValue is byte[] bytes)
                    {
                        eventType = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    else if (eventTypeValue is string str)
                    {
                        eventType = str;
                    }
                }

                _logger.LogInformation("Evento recebido - EventType: {EventType}, CorrelationId: {CorrelationId}", eventType, correlationId);

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var handler = scope.ServiceProvider.GetRequiredService<OrderEventHandler>();

                        if (eventType.Contains("OrderCompleted"))
                        {
                            _logger.LogInformation("Processando OrderCompleted");
                            var @event = JsonSerializer.Deserialize<OrderCompleted>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                            if (@event != null)
                                await handler.HandleOrderCompleted(@event);
                        }
                        else if (eventType.Contains("OrderFailed"))
                        {
                            _logger.LogInformation("Processando OrderFailed");
                            var @event = JsonSerializer.Deserialize<OrderFailed>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                            if (@event != null)
                                await handler.HandleOrderFailed(@event);
                        }
                    }

                    _channel.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation("Evento processado com sucesso - CorrelationId: {CorrelationId}", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar evento - CorrelationId: {CorrelationId}, EventType: {EventType}", correlationId, eventType);
                    _channel.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _logger.LogInformation("Configurando consumidor para a fila: {QueueName}", QueueNames.OrderEvents);

            // Retry logic: aguarda pela fila estar disponível
            int maxRetries = 10;
            int retryCount = 0;
            int delayMs = 1000;

            while (retryCount < maxRetries)
            {
                try
                {
                    _channel.BasicConsume(queue: QueueNames.OrderEvents, autoAck: false, consumer: consumer);

                    _logger.LogInformation("Consumidor de eventos de pedido escutando a fila: {QueueName}", QueueNames.OrderEvents);
                    _logger.LogInformation("Consumidor de eventos de pedido iniciado com sucesso");
                    break;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.Message.Contains("NOT_FOUND") || ex.Message.Contains("RESOURCE_LOCKED"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("Máximo de tentativas atingido. Fila '{Queue}' não está disponível.", QueueNames.OrderEvents);
                        throw;
                    }

                    _logger.LogWarning("Fila '{Queue}' não disponível (tentativa {Attempt}/{MaxRetries}). Tentando novamente em {DelayMs}ms...",
                        QueueNames.OrderEvents, retryCount, maxRetries, delayMs);

                    await Task.Delay(delayMs, stoppingToken);
                    delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff, max 30s
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal no consumidor de eventos de pedido");
            throw;
        }
    }

    public override void Dispose()
    {
        try
        {
            _channel?.Dispose();
            _connection?.Dispose();
            _logger.LogInformation("Consumidor de eventos de pedido finalizado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar consumidor de eventos de pedido");
        }

        base.Dispose();
    }
}
