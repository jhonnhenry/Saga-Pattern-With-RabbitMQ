using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SagaOrchestrator.Handlers;
using Shared.Infrastructure;
using Shared.Models;
using System.Text.Json;
using System.Threading.Channels;

namespace SagaOrchestrator.Handlers;

public class SagaEventConsumer : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly RabbitMQSettings _settings;
    private readonly ILogger<SagaEventConsumer> _logger;
    private RabbitMQ.Client.IConnection? _connection;
    private RabbitMQ.Client.IModel? _channel;

    // Mapeamento de tipos de eventos para handlers
    private readonly Dictionary<string, Func<SagaOrchestrationHandler, string, Task>> _eventHandlers;

    public SagaEventConsumer(ILogger<SagaEventConsumer> logger, IServiceProvider serviceProvider, RabbitMQSettings settings)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _settings = settings;
        _eventHandlers = InitializeEventHandlers();
    }

    /// <summary>
    /// Inicializa o mapeamento de tipos de eventos para seus respectivos handlers
    /// </summary>
    private Dictionary<string, Func<SagaOrchestrationHandler, string, Task>> InitializeEventHandlers()
    {
        return new Dictionary<string, Func<SagaOrchestrationHandler, string, Task>>
        {
            { nameof(OrderCreated), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<OrderCreated>(message);
                if (@event != null) await handler.HandleOrderCreated(@event);
            }},
            { nameof(PaymentCompleted), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<PaymentCompleted>(message);
                if (@event != null) await handler.HandlePaymentCompleted(@event);
            }},
            { nameof(PaymentFailed), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<PaymentFailed>(message);
                if (@event != null) await handler.HandlePaymentFailed(@event);
            }},
            { nameof(PaymentRefunded), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<PaymentRefunded>(message);
                if (@event != null) await handler.HandlePaymentRefunded(@event);
            }},
            { nameof(InventoryReserved), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<InventoryReserved>(message);
                if (@event != null) await handler.HandleInventoryReserved(@event);
            }},
            { nameof(InventoryReservationFailed), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<InventoryReservationFailed>(message);
                if (@event != null) await handler.HandleInventoryReservationFailed(@event);
            }},
            { nameof(InventoryReleased), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<InventoryReleased>(message);
                if (@event != null) await handler.HandleInventoryReleased(@event);
            }},
            { nameof(DeliveryScheduled), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<DeliveryScheduled>(message);
                if (@event != null) await handler.HandleDeliveryScheduled(@event);
            }},
            { nameof(DeliverySchedulingFailed), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<DeliverySchedulingFailed>(message);
                if (@event != null) await handler.HandleDeliverySchedulingFailed(@event);
            }},
            { nameof(DeliveryCancelled), async (handler, message) =>
            {
                var @event = JsonSerializer.Deserialize<DeliveryCancelled>(message);
                if (@event != null) await handler.HandleDeliveryCancelled(@event);
            }}
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumidor de Eventos da Saga iniciando...");
        _logger.LogInformation($"[SagaEventConsumer] Iniciando - conectando a {_settings.Host}:{_settings.Port}/{_settings.VirtualHost}");

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

            _logger.LogInformation($"[SagaEventConsumer] Conectando ao RabbitMQ");
            _connection = factory.CreateConnection();
            _logger.LogInformation($"[SagaEventConsumer] Conectado! Criando canal");
            _channel = _connection.CreateModel();
            _logger.LogInformation($"[SagaEventConsumer] Canal criado");

            _channel!.BasicQos(0, 1, false);

            //Criando Exchanges e Queues se não existirem
            _channel?.ExchangeDeclare(exchange: ExchangeNames.Commands, type: ExchangeType.Direct);
            _channel?.ExchangeDeclare(exchange: ExchangeNames.Events, type: ExchangeType.Topic);
            _channel?.ExchangeDeclare(exchange: ExchangeNames.DeadLetter, type: ExchangeType.Direct);

            var queueArguments = new Dictionary<string, object>
            {
                //Define para qual exchange uma mensagem será enviada quando ela "morre"(falha)
                { "x-dead-letter-exchange", ExchangeNames.DeadLetter },
                //Define qual routing key usar quando enviar para o DLX
                { "x-dead-letter-routing-key", EventRoutingKeys.DLQ },
                //Tempo máximo em milissegundos que a mensagem fica na fila (300000 = 5 minutos)
                //Se ninguém consumir em 5 minutos, ela expira, vai para o DLX (sagaDlq)
                { "x-message-ttl", 300000 }
                /*
                    Por que isso é importante?

                    SEM os argumentos:
                    -  Mensagens que falham são perdidas silenciosamente
                    -  Sem rastreamento de erros
                    -  Saga fica presa em estado AWAITING_PAYMENT para sempre                 
                 */
            };

            // Declarando commands queues
            _channel?.QueueDeclare(queue: QueueNames.OrderCommands, durable: true, autoDelete: false,
                arguments: queueArguments, exclusive: false);
            _channel?.QueueDeclare(queue: QueueNames.PaymentCommands, durable: true, autoDelete: false,
                arguments: queueArguments, exclusive: false);
            _channel?.QueueDeclare(queue: QueueNames.InventoryCommands, durable: true, autoDelete: false,
                arguments: queueArguments, exclusive: false);
            _channel?.QueueDeclare(queue: QueueNames.DeliveryCommands, durable: true, autoDelete: false,
                arguments: queueArguments, exclusive: false);

            // Declarando events queues
            _channel?.QueueDeclare(queue: QueueNames.SagaOrchestratorEvents, durable: true, autoDelete: false,
                arguments: null, exclusive: false);
            _channel?.QueueDeclare(queue: QueueNames.OrderEvents, durable: true, autoDelete: false,
                arguments: null, exclusive: false);

            // Declarando DLQ
            _channel?.QueueDeclare(queue: QueueNames.DeadLetter, durable: true, autoDelete: false,
                arguments: null, exclusive: false);

            // Bind command queues
            _channel?.QueueBind(queue: QueueNames.OrderCommands, exchange: ExchangeNames.Commands, routingKey: CommandRoutingKeys.Order);
            _channel?.QueueBind(queue: QueueNames.PaymentCommands, exchange: ExchangeNames.Commands, routingKey: CommandRoutingKeys.Payment);
            _channel?.QueueBind(queue: QueueNames.InventoryCommands, exchange: ExchangeNames.Commands, routingKey: CommandRoutingKeys.Inventory);
            _channel?.QueueBind(queue: QueueNames.DeliveryCommands, exchange: ExchangeNames.Commands, routingKey: CommandRoutingKeys.Delivery);

            // Bind saga orchestrator events queue
            _channel?.QueueBind(queue: QueueNames.SagaOrchestratorEvents, exchange: ExchangeNames.Events, routingKey: EventRoutingPatterns.AllEvents);

            // Bind order events queue
            _channel?.QueueBind(queue: QueueNames.OrderEvents, exchange: ExchangeNames.Events, routingKey: EventRoutingKeys.OrderCompleted);
            _channel?.QueueBind(queue: QueueNames.OrderEvents, exchange: ExchangeNames.Events, routingKey: EventRoutingKeys.OrderFailed);

            // Bind DLQ
            _channel?.QueueBind(queue: QueueNames.DeadLetter, exchange: ExchangeNames.DeadLetter, routingKey: EventRoutingKeys.DLQ);

            var consumer = new RabbitMQ.Client.Events.AsyncEventingBasicConsumer(_channel!);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = System.Text.Encoding.UTF8.GetString(body);
                var correlationId = ea.BasicProperties?.CorrelationId ?? "N/A";

                // Convert header from byte[] to string
                string eventTypeHeader = "N/A";
                if (ea.BasicProperties?.Headers != null && ea.BasicProperties.Headers.ContainsKey("EventType"))
                {
                    var eventTypeValue = ea.BasicProperties.Headers["EventType"];
                    if (eventTypeValue is byte[] bytes)
                    {
                        eventTypeHeader = System.Text.Encoding.UTF8.GetString(bytes);
                    }
                    else if (eventTypeValue is string str)
                    {
                        eventTypeHeader = str;
                    }
                }

                var routingKey = ea.RoutingKey ?? "N/A";

                _logger.LogInformation("Evento da Saga recebido - CorrelationId: {CorrelationId}, EventType: {EventType}, RoutingKey: {RoutingKey}",
                    correlationId, eventTypeHeader, routingKey);
                _logger.LogInformation("Mensagem de evento: {Message}", message);

                try
                {
                    var eventType = eventTypeHeader;

                    if (!string.IsNullOrEmpty(eventType) && eventType != "N/A")
                    {
                        _logger.LogInformation($"[SagaEventConsumer] Processando tipo de evento: {eventType}");
                        using (var scope = _serviceProvider.CreateScope())
                        {
                            var handler = scope.ServiceProvider.GetRequiredService<SagaOrchestrationHandler>();

                            // Usa o dicionário de handlers para rotear o evento
                            if (_eventHandlers.TryGetValue(eventType, out var eventHandler))
                            {
                                _logger.LogInformation($"[SagaEventConsumer]  Evento {eventType} correspondido");
                                await eventHandler(handler, message);
                            }
                            else
                            {
                                _logger.LogWarning("️  Nenhum handler encontrado para o tipo de evento: {EventType}", eventType);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogInformation($"[SagaEventConsumer] Nenhum tipo de evento detectado! eventType é: '{eventType}'");
                    }

                    _channel?.BasicAck(ea.DeliveryTag, false);
                    _logger.LogInformation("Evento da Saga processado com sucesso - CorrelationId: {CorrelationId}", correlationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar evento da Saga - CorrelationId: {CorrelationId}", correlationId);
                    _channel?.BasicNack(ea.DeliveryTag, false, true);
                }
            };

            _logger.LogInformation($"[SagaEventConsumer] Configurando consumidor para a fila: {QueueNames.SagaOrchestratorEvents}");

            // Retry logic: aguarda pela fila estar disponível
            int maxRetries = 10;
            int retryCount = 0;
            int delayMs = 1000;

            while (retryCount < maxRetries)
            {
                try
                {
                    _channel?.BasicConsume(
                        queue: QueueNames.SagaOrchestratorEvents,
                        autoAck: false,
                        consumerTag: "saga-consumer",
                        noLocal: false,
                        exclusive: false,
                        arguments: null,
                        consumer: consumer
                    );

                    _logger.LogInformation($"[SagaEventConsumer]  PRONTO! Escutando a fila: {QueueNames.SagaOrchestratorEvents}");
                    _logger.LogInformation("Consumidor de Eventos da Saga iniciado com sucesso - escutando a fila: {QueueName}", QueueNames.SagaOrchestratorEvents);
                    break;
                }
                catch (RabbitMQ.Client.Exceptions.OperationInterruptedException ex) when (ex.Message.Contains("RESOURCE_LOCKED"))
                {
                    retryCount++;
                    if (retryCount >= maxRetries)
                    {
                        _logger.LogInformation($"[SagaEventConsumer] Máximo de tentativas atingido. A fila está bloqueada.");
                        throw;
                    }

                    _logger.LogInformation($"️  [SagaEventConsumer] Fila bloqueada (tentativa {retryCount}/{maxRetries}). Tentando novamente em {delayMs}ms...");
                    _logger.LogWarning("A fila de eventos da Saga está bloqueada. Tentando novamente em {DelayMs}ms (tentativa {Attempt}/{MaxRetries})",
                        delayMs, retryCount, maxRetries);

                    await Task.Delay(delayMs, stoppingToken);
                    delayMs = Math.Min(delayMs * 2, 30000); // Exponential backoff, max 30s
                }
            }
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogInformation($"[SagaEventConsumer] ERRO FATAL: {ex.Message}");
            _logger.LogInformation($"[SagaEventConsumer] Stack Trace: {ex.StackTrace}");
            _logger.LogError(ex, "Erro no Consumidor de Eventos da Saga");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Consumidor de Eventos da Saga parando...");
        _channel?.Dispose();
        _connection?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
