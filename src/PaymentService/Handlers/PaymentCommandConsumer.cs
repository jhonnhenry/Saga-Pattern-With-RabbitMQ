using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure;
using Shared.Models;
using System.Text.Json;

namespace PaymentService.Handlers;

public class PaymentCommandConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<PaymentCommandConsumer> _logger;
    private RabbitMQ.Client.IConnection? _connection;
    private RabbitMQ.Client.IModel? _channel;

    public PaymentCommandConsumer(ILogger<PaymentCommandConsumer> logger, IServiceProvider serviceProvider, RabbitMQSettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumidor de Comandos de Pagamento iniciando...");
        _logger.LogInformation($"[PaymentCommandConsumer] Iniciando - conectando a {_settings.Host}:{_settings.Port}");

        try
        {
            _logger.LogInformation($"[PaymentCommandConsumer] Criando factory de conexão");
            var factory = new RabbitMQ.Client.ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                DispatchConsumersAsync = true
            };

            _logger.LogInformation($"[PaymentCommandConsumer] Conectando ao RabbitMQ");
            _connection = factory.CreateConnection();
            _logger.LogInformation($"[PaymentCommandConsumer] Conectado! Criando canal");
            _channel = _connection.CreateModel();
            _logger.LogInformation($"[PaymentCommandConsumer] Canal criado");

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

                _logger.LogInformation("Comando de pagamento recebido - CommandType: {CommandType}, CorrelationId: {CorrelationId}", commandTypeHeader, correlationId);
                _logger.LogInformation($"[PaymentCommandConsumer] Mensagem: {message}");

                try
                {
                    var commandType = commandTypeHeader;

                    if (!string.IsNullOrEmpty(commandType) && commandType != "N/A")
                    {
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var handler = scope.ServiceProvider.GetRequiredService<PaymentCommandHandler>();

                            if (commandType.Contains("ProcessPaymentCommand"))
                            {
                                _logger.LogInformation($"[PaymentCommandConsumer]  Processando ProcessPaymentCommand");
                                var command = JsonSerializer.Deserialize<ProcessPaymentCommand>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                                if (command != null)
                                    await handler.HandleProcessPaymentCommand(command);
                            }
                            else if (commandType.Contains("ReleasePaymentCommand"))
                            {
                                _logger.LogInformation($"[PaymentCommandConsumer]  Processando ReleasePaymentCommand");
                                var command = JsonSerializer.Deserialize<ReleasePaymentCommand>(message, new JsonSerializerOptions { PropertyNamingPolicy = null });
                                if (command != null)
                                    await handler.HandleReleasePaymentCommand(command);
                            }
                            else
                            {
                                _logger.LogInformation($"️  [PaymentCommandConsumer] Tipo de comando desconhecido: {commandType}");
                            }
                        }
                    }

                    _channel?.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation($"[PaymentCommandConsumer] Comando processado com sucesso");
                    _logger.LogInformation("Comando de pagamento processado - CorrelationId: {CorrelationId}", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar comando de pagamento - CorrelationId: {CorrelationId}", correlationId);
                    _channel?.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _logger.LogInformation($"[PaymentCommandConsumer] Configurando consumidor para a fila: {QueueNames.PaymentCommands}");

            // Retry logic: aguarda pela fila estar disponível
            int maxRetries = 10;
            int retryCount = 0;
            int delayMs = 1000;

            while (retryCount < maxRetries)
            {
                try
                {
                    _channel?.BasicConsume(
                        queue: QueueNames.PaymentCommands,
                        autoAck: false,
                        consumerTag: "payment-consumer",
                        noLocal: false,
                        exclusive: false,
                        arguments: null,
                        consumer: consumer
                    );

                    _logger.LogInformation($"[PaymentCommandConsumer]  PRONTO! Escutando a fila: {QueueNames.PaymentCommands}");
                    _logger.LogInformation("Consumidor de Comandos de Pagamento iniciado com sucesso");
                    break;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.Message.Contains("RESOURCE_LOCKED"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogInformation($"[PaymentCommandConsumer] Máximo de tentativas atingido. A fila está bloqueada.");
                        throw;
                    }

                    _logger.LogInformation($"️  [PaymentCommandConsumer] Fila bloqueada (tentativa {retryCount}/{maxRetries}). Tentando novamente em {delayMs}ms...");
                    _logger.LogWarning("A fila de comandos de pagamento está bloqueada. Tentando novamente em {DelayMs}ms (tentativa {Attempt}/{MaxRetries})",
                        delayMs, retryCount, maxRetries);

                    await Task.Delay(delayMs, stoppingToken);
                    delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff, max 30s
                }
            }

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"[PaymentCommandConsumer] ERRO FATAL: {ex.Message}");
            _logger.LogInformation($"[PaymentCommandConsumer] Stack Trace: {ex.StackTrace}");
            _logger.LogError(ex, "Erro no Consumidor de Comandos de Pagamento");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consumidor de Comandos de Pagamento parando...");
        _channel?.Dispose();
        _connection?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
