using GameStore.Contracts.Events;
using GameStore.Payments.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameStore.Payments.Application.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class;
}

public record PaymentResponse(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Currency,
    string Status,
    string? RejectionReason,
    string? UserEmail,
    string? GameTitle,
    DateTime ProcessedAt
);

public interface IPaymentService
{
    Task<PaymentResponse> ProcessOrderPaymentAsync(OrderPlaceEvent orderEvent, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentResponse>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}

public class PaymentService : IPaymentService
{
    private readonly IPaymentRepository _paymentRepository;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        IPaymentRepository paymentRepository,
        IEventPublisher eventPublisher,
        ILogger<PaymentService> logger)
    {
        _paymentRepository = paymentRepository;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<PaymentResponse> ProcessOrderPaymentAsync(OrderPlaceEvent orderEvent, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[PaymentsAPI] Iniciando simulação de pagamento para OrderId={OrderId}, UserId={UserId}, GameId={GameId}, Price={Price}",
            orderEvent.OrderId, orderEvent.UserId, orderEvent.GameId, orderEvent.Price);

        var transaction = PaymentTransaction.SimulateProcessing(
            orderId: orderEvent.OrderId,
            userId: orderEvent.UserId,
            gameId: orderEvent.GameId,
            price: orderEvent.Price,
            currency: orderEvent.Currency,
            userEmail: orderEvent.UserEmail,
            gameTitle: orderEvent.GameTitle
        );

        await _paymentRepository.AddAsync(transaction, cancellationToken);
        await _paymentRepository.CommitAsync(cancellationToken);

        var paymentProcessedEvent = new PaymentProcessedEvent(
            PaymentId: transaction.Id,
            OrderId: transaction.OrderId,
            UserId: transaction.UserId,
            GameId: transaction.GameId,
            Price: transaction.Price,
            Status: transaction.Status,
            Currency: transaction.Currency,
            UserEmail: transaction.UserEmail,
            GameTitle: transaction.GameTitle,
            ProcessedAt: transaction.ProcessedAt
        );

        await _eventPublisher.PublishAsync(paymentProcessedEvent, cancellationToken);

        _logger.LogInformation(
            "[PaymentsAPI] Pagamento processado e PaymentProcessedEvent publicado! PaymentId={PaymentId}, Status={Status}",
            transaction.Id, transaction.Status);

        return MapToResponse(transaction);
    }

    public async Task<IEnumerable<PaymentResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await _paymentRepository.GetAllAsync(cancellationToken);
        return items.Select(MapToResponse);
    }

    public async Task<IEnumerable<PaymentResponse>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var items = await _paymentRepository.GetByUserIdAsync(userId, cancellationToken);
        return items.Select(MapToResponse);
    }

    private static PaymentResponse MapToResponse(PaymentTransaction t) => new(
        t.Id,
        t.OrderId,
        t.UserId,
        t.GameId,
        t.Price,
        t.Currency,
        t.Status,
        t.RejectionReason,
        t.UserEmail,
        t.GameTitle,
        t.ProcessedAt
    );
}

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPaymentService, PaymentService>();
        return services;
    }
}

