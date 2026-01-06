using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure;

/// <summary>
/// Classe abstrata para consumir mensagens do RabbitMQ
/// Serviços específicos herdam dessa classe e implementam seus handlers
/// </summary>
public abstract class MessageConsumer : IDisposable
{
    protected readonly IConnection Connection;
    protected readonly IModel Channel;
    protected readonly ILogger Logger;
    protected readonly JsonSerializerOptions JsonOptions;

    public MessageConsumer(RabbitMQSettings settings, ILogger logger)
    {
        Logger = logger;
        JsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = null };

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

        Connection = factory.CreateConnection();
        Channel = Connection.CreateModel();
    }

    /// <summary>
    /// Inicia o consumo de mensagens de uma fila específica
    /// </summary>
    public void StartConsuming(string queueName, Func<string, Task> messageHandler)
    {
        var consumer = new AsyncEventingBasicConsumer(Channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                Logger.LogInformation(
                    "Mensagem recebida - Queue: {Queue}, CorrelationId: {CorrelationId}, Message: {Message}",
                    queueName,
                    ea.BasicProperties?.CorrelationId ?? "N/A",
                    message
                );

                await messageHandler(message);

                // ACK - reconhece que a mensagem foi processada
                Channel.BasicAck(ea.DeliveryTag, false);

                Logger.LogInformation("Mensagem processada com sucesso - CorrelationId: {CorrelationId}",
                    ea.BasicProperties?.CorrelationId ?? "N/A");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Erro ao processar mensagem - Queue: {Queue}, CorrelationId: {CorrelationId}",
                    queueName,
                    ea.BasicProperties?.CorrelationId ?? "N/A");

                // NACK - nega o processamento, retorna à fila (com requeue)
                // Após TTL expirar, irá para DLQ
                Channel.BasicNack(ea.DeliveryTag, false, true);
            }
        };

        Channel.BasicQos(0, 1, false); // Processar uma mensagem por vez

        Channel.BasicConsume(
            queue: queueName,
            autoAck: false, // Manual ACK
            consumerTag: $"{queueName}-consumer",
            consumer: consumer
        );

        Logger.LogInformation("Iniciado consumo de mensagens da fila: {QueueName}", queueName);
    }

    /// <summary>
    /// Deserializa a mensagem JSON para um tipo específico
    /// </summary>
    protected T DeserializeMessage<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Falha ao desserializar mensagem de tipo {typeof(T).Name}");
    }

    public virtual void Dispose()
    {
        Channel?.Dispose();
        Connection?.Dispose();
    }
}
