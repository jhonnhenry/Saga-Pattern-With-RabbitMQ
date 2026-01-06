using RabbitMQ.Client;
using System.Text.Json;
using System.Text;
using Shared.Models;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure;

/// <summary>
/// Publicador de mensagens para RabbitMQ
/// </summary>
public class MessagePublisher : IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly IBasicProperties _basicProperties;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = null };
    private readonly ILogger<MessagePublisher> _logger;

    public MessagePublisher(RabbitMQSettings settings, ILogger<MessagePublisher> logger)
    {
        _logger = logger;

        _logger.LogInformation("Inicializando MessagePublisher - conectando ao RabbitMQ em {Host}:{Port}", settings.Host, settings.Port);

        var factory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password,
            VirtualHost = settings.VirtualHost,
            RequestedConnectionTimeout = settings.ConnectionTimeout,
            DispatchConsumersAsync = true
        };

        try
        {
            _connection = factory.CreateConnection();
            _logger.LogInformation("Conexão com RabbitMQ estabelecida");

            _channel = _connection.CreateModel();
            _logger.LogInformation("Canal criado");

            _basicProperties = _channel.CreateBasicProperties();
            _basicProperties.Persistent = true;
            _basicProperties.ContentType = "application/json";
            _basicProperties.ContentEncoding = "utf-8";

            _logger.LogInformation("MessagePublisher inicializado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao inicializar MessagePublisher");
            throw;
        }
    }

    /// <summary>
    /// Publica um evento no topic exchange
    /// </summary>
    public void PublishEvent<TEvent>(TEvent @event, string routingKey) where TEvent : DomainEvent
    {
        try
        {
            var eventTypeName = @event.GetType().Name;
            var json = JsonSerializer.Serialize(@event, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            _basicProperties.CorrelationId = @event.CorrelationId;
            _basicProperties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _basicProperties.Headers ??= new Dictionary<string, object>();
            _basicProperties.Headers["EventType"] = eventTypeName;
            _basicProperties.Headers["OrderId"] = @event.OrderId;

            _logger.LogInformation("Publicando evento {EventType} para exchange {Exchange} com routing key {RoutingKey} (OrderId: {OrderId}, CorrelationId: {CorrelationId})",
                eventTypeName, ExchangeNames.Events, routingKey, @event.OrderId, @event.CorrelationId);
            _logger.LogDebug("Payload do evento: {EventPayload}", json);

            _channel.BasicPublish(
                exchange: ExchangeNames.Events,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: _basicProperties,
                body: body
            );

            _logger.LogInformation("Evento {EventType} publicado com sucesso (CorrelationId: {CorrelationId})",
                eventTypeName, @event.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar evento {EventType} (OrderId: {OrderId}, CorrelationId: {CorrelationId})",
                @event.GetType().Name, @event.OrderId, @event.CorrelationId);
            throw;
        }
    }

    /// <summary>
    /// Publica um comando no direct exchange
    /// </summary>
    public void PublishCommand<TCommand>(TCommand command, string routingKey) where TCommand : Command
    {
        try
        {
            var commandTypeName = command.GetType().Name;
            var json = JsonSerializer.Serialize(command, _jsonOptions);
            var body = Encoding.UTF8.GetBytes(json);

            _basicProperties.CorrelationId = command.CorrelationId;
            _basicProperties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            _basicProperties.Headers ??= new Dictionary<string, object>();
            _basicProperties.Headers["CommandType"] = commandTypeName;
            _basicProperties.Headers["OrderId"] = command.OrderId;

            _logger.LogInformation("Publicando comando {CommandType} para exchange {Exchange} com routing key {RoutingKey} (OrderId: {OrderId}, CorrelationId: {CorrelationId})",
                commandTypeName, ExchangeNames.Commands, routingKey, command.OrderId, command.CorrelationId);
            _logger.LogDebug("Payload do comando: {CommandPayload}", json);

            _channel.BasicPublish(
                exchange: ExchangeNames.Commands,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: _basicProperties,
                body: body
            );

            _logger.LogInformation("Comando {CommandType} publicado com sucesso (CorrelationId: {CorrelationId})",
                commandTypeName, command.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar comando {CommandType} (OrderId: {OrderId}, CorrelationId: {CorrelationId})",
                command.GetType().Name, command.OrderId, command.CorrelationId);
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            _logger.LogInformation("Liberando recursos do MessagePublisher...");

            _channel?.Dispose();
            _logger.LogInformation("Canal liberado");

            _connection?.Dispose();
            _logger.LogInformation("Conexão liberada");

            _logger.LogInformation("MessagePublisher finalizado com sucesso");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar MessagePublisher");
            throw;
        }
    }
}
