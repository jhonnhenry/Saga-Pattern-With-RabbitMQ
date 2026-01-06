using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure;
using Shared.Models;
using System.Text.Json;

namespace DeliveryService.Handlers;

public class DeliveryCommandConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<DeliveryCommandConsumer> _logger;
    private RabbitMQ.Client.IConnection? _connection;
    private RabbitMQ.Client.IModel? _channel;

    public DeliveryCommandConsumer(ILogger<DeliveryCommandConsumer> logger, IServiceProvider serviceProvider, RabbitMQSettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumidor de Comandos de Entrega iniciando...");

        try
        {
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel!.BasicQos(0, 1, false);

            var consumer = new RabbitMQ.Client.Events.AsyncEventingBasicConsumer(_channel!);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                var correlationId = ea.BasicProperties?.CorrelationId ?? "N/A";

                // Convert header from byte[] to string
                string commandTypeHeader = "N/A";
                if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.ContainsKey("CommandType"))
                {
                    var commandTypeValue = ea.BasicProperties.Headers["CommandType"];
                    if (commandTypeValue is byte[] bytes)
                    {
                        commandTypeHeader = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    else if (commandTypeValue is string str)
                    {
                        commandTypeHeader = str;
                    }
                }

                _logger.LogInformation("Comando de entrega recebido - CommandType: {CommandType}, CorrelationId: {CorrelationId}", commandTypeHeader, correlationId);

                try
                {
                    var commandType = commandTypeHeader;

                    if (!string.IsNullOrEmpty(commandType) && commandType != "N/A")
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var handler = scope.ServiceProvider.GetRequiredService<DeliveryCommandHandler>();

                            if (commandType.Contains("ScheduleDeliveryCommand"))
                            {
                                var command = JsonSerializer.Deserialize<ScheduleDeliveryCommand>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                                if (command != null)
                                    await handler.HandleScheduleDeliveryCommand(command);
                            }
                            else if (commandType.Contains("CancelDeliveryCommand"))
                            {
                                var command = JsonSerializer.Deserialize<CancelDeliveryCommand>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                                if (command != null)
                                    await handler.HandleCancelDeliveryCommand(command);
                            }
                        }
                    }

                    _channel?.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation("Comando de entrega processado - CorrelationId: {CorrelationId}", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar comando de entrega - CorrelationId: {CorrelationId}", correlationId);
                    _channel?.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            // Retry logic: aguarda pela fila estar disponível
            int maxRetries = 10;
            int retryCount = 0;
            int delayMs = 1000;

            while (retryCount < maxRetries)
            {
                try
                {
                    _channel?.BasicConsume(
                        queue: QueueNames.DeliveryCommands,
                        autoAck: false,
                        consumerTag: "delivery-consumer",
                        noLocal: false,
                        exclusive: false,
                        arguments: null,
                        consumer: consumer
                    );

                    _logger.LogInformation("Consumidor de Comandos de Entrega iniciado com sucesso");
                    break;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.Message.Contains("RESOURCE_LOCKED"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogError("Máximo de tentativas atingido. A fila está bloqueada.");
                        throw;
                    }

                    _logger.LogWarning("A fila de comandos de entrega está bloqueada. Tentando novamente em {DelayMs}ms (tentativa {Attempt}/{MaxRetries})",
                        delayMs, retryCount, maxRetries);

                    await Task.Delay(delayMs, stoppingToken);
                    delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff, max 30s
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no Consumidor de Comandos de Entrega");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consumidor de Comandos de Entrega parando...");
        _channel?.Dispose();
        _connection?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
