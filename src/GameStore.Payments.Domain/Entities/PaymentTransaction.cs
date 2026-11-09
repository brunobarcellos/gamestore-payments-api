namespace GameStore.Payments.Domain.Entities;

public static class PaymentStatus
{
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>
/// Entidade de domínio que representa o processamento de pagamento de uma compra de jogo.
/// </summary>
public class PaymentTransaction
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = "BRL";
    public string Status { get; private set; } = null!;
    public string? RejectionReason { get; private set; }
    public string? UserEmail { get; private set; }
    public string? GameTitle { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    protected PaymentTransaction() { }

    private PaymentTransaction(
        Guid orderId,
        Guid userId,
        Guid gameId,
        decimal price,
        string currency,
        string status,
        string? rejectionReason,
        string? userEmail,
        string? gameTitle)
    {
        Id = Guid.NewGuid();
        OrderId = orderId == Guid.Empty ? Guid.NewGuid() : orderId;
        UserId = userId;
        GameId = gameId;
        Price = price;
        Currency = string.IsNullOrWhiteSpace(currency) ? "BRL" : currency.ToUpperInvariant();
        Status = status;
        RejectionReason = rejectionReason;
        UserEmail = userEmail;
        GameTitle = gameTitle;
        ProcessedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Simula o processamento do pagamento de um pedido:
    /// Aprova compras com valor válido dentro do limite configurado; rejeita valores negativos ou acima do teto antifraude.
    /// </summary>
    public static PaymentTransaction SimulateProcessing(
        Guid orderId,
        Guid userId,
        Guid gameId,
        decimal price,
        string currency = "BRL",
        decimal maxApprovedAmount = 5000.00m,
        string? userEmail = null,
        string? gameTitle = null)
    {
        if (userId == Guid.Empty || gameId == Guid.Empty)
        {
            return new PaymentTransaction(
                orderId, userId, gameId, price, currency,
                PaymentStatus.Rejected,
                "Identificador de usuário ou jogo inválido.",
                userEmail, gameTitle);
        }

        if (price < 0 || price > maxApprovedAmount)
        {
            return new PaymentTransaction(
                orderId, userId, gameId, price, currency,
                PaymentStatus.Rejected,
                $"Valor do pedido ({price:N2}) fora dos limites permitidos pelo gateway simulado.",
                userEmail, gameTitle);
        }

        return new PaymentTransaction(
            orderId, userId, gameId, price, currency,
            PaymentStatus.Approved,
            null,
            userEmail, gameTitle);
    }
}

public interface IPaymentRepository
{
    Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentTransaction>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentTransaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> CommitAsync(CancellationToken cancellationToken = default);
}

