using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Infrastructure;
using Shared.Models;
using PaymentService.Data;

namespace PaymentService.Handlers;

public class PaymentCommandHandler
{
    private readonly PaymentDbContext _db;
    private readonly MessagePublisher _publisher;
    private readonly ILogger<PaymentCommandHandler> _logger;

    public PaymentCommandHandler(ILogger<PaymentCommandHandler> logger, PaymentDbContext db, MessagePublisher publisher)
    {
        _logger = logger;
        _db = db;
        _publisher = publisher;
    }

    public async Task HandleProcessPaymentCommand(ProcessPaymentCommand command)
    {
        _logger.LogInformation("Processando comando de pagamento para o pedido {OrderId}, valor {Amount}, correlationId {CorrelationId}",
            command.OrderId, command.Amount, command.CorrelationId);

        // Verifica idempotência
        var existing = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == command.OrderId);
        if (existing != null)
        {
            _logger.LogInformation("Pagamento já processado para o pedido {OrderId}, status: {Status}", command.OrderId, existing.Status);

            if (existing.Status == "COMPLETED")
            {
                PublishPaymentCompleted(command, existing.TransactionId);
            }
            else if (existing.Status == "FAILED")
            {
                PublishPaymentFailed(command, "Tentativa anterior de pagamento falhou");
            }
            return;
        }

        try
        {
            // Simula processamento de pagamento
            var transactionId = $"TXN-{command.OrderId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

            // No mundo real, chame o gateway de pagamento aqui
            bool paymentSucceeded = SimulatePaymentGateway(command.Amount);

            if (paymentSucceeded)
            {
                var payment = new Payment
                {
                    OrderId = command.OrderId,
                    Amount = command.Amount,
                    Status = "COMPLETED",
                    TransactionId = transactionId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                PublishPaymentCompleted(command, transactionId);

                _logger.LogInformation("Pagamento processado com sucesso para o pedido {OrderId}", command.OrderId);
            }
            else
            {
                var payment = new Payment
                {
                    OrderId = command.OrderId,
                    Amount = command.Amount,
                    Status = "FAILED",
                    TransactionId = transactionId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.Payments.Add(payment);
                await _db.SaveChangesAsync();

                PublishPaymentFailed(command, "Gateway de pagamento recusou");

                _logger.LogWarning("Pagamento falhou para o pedido {OrderId}", command.OrderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar pagamento para o pedido {OrderId}", command.OrderId);
            PublishPaymentFailed(command, $"Error: {ex.Message}");
            throw;
        }
    }

    public async Task HandleReleasePaymentCommand(ReleasePaymentCommand command)
    {
        _logger.LogInformation("Liberando pagamento para o pedido {OrderId}, correlationId {CorrelationId}",
            command.OrderId, command.CorrelationId);

        var payment = await _db.Payments.FirstOrDefaultAsync(p => p.OrderId == command.OrderId);
        if (payment == null)
        {
            _logger.LogWarning("Pagamento não encontrado para o pedido {OrderId}", command.OrderId);
            return;
        }

        try
        {
            // Simula reembolso
            string refundTransactionId = $"RFD-{payment.TransactionId}";
            payment.Status = "REFUNDED";
            payment.UpdatedAt = DateTime.UtcNow;

            _db.Payments.Update(payment);
            await _db.SaveChangesAsync();

            var paymentRefunded = new PaymentRefunded
            {
                OrderId = command.OrderId,
                Amount = command.Amount,
                OriginalTransactionId = payment.TransactionId,
                RefundTransactionId = refundTransactionId,
                CorrelationId = command.CorrelationId
            };

            _publisher.PublishEvent(paymentRefunded, EventRoutingKeys.PaymentRefunded);

            _logger.LogInformation("Pagamento reembolsado para o pedido {OrderId}", command.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao reembolsar pagamento para o pedido {OrderId}", command.OrderId);
            throw;
        }
    }

    private void PublishPaymentCompleted(ProcessPaymentCommand command, string transactionId)
    {
        var @event = new PaymentCompleted
        {
            OrderId = command.OrderId,
            Amount = command.Amount,
            TransactionId = transactionId,
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.PaymentCompleted);
    }

    private void PublishPaymentFailed(ProcessPaymentCommand command, string reason)
    {
        var @event = new PaymentFailed
        {
            OrderId = command.OrderId,
            Amount = command.Amount,
            Reason = reason,
            CorrelationId = command.CorrelationId
        };

        _publisher.PublishEvent(@event, EventRoutingKeys.PaymentFailed);
    }

    private bool SimulatePaymentGateway(decimal amount)
    {
        // simulando 90% de sucesso
        return new Random().Next(100) < 90;
    }
}
