namespace GameStore.Contracts.Events;

/// <summary>
/// Evento consumido pela PaymentsAPI quando um pedido de compra de jogo é iniciado na CatalogAPI.
/// </summary>
public record OrderPlaceEvent(
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Currency = "BRL",
    string? UserEmail = null,
    string? GameTitle = null,
    DateTime CreatedAt = default
);

/// <summary>
/// Evento publicado pela PaymentsAPI após simular o processamento do pagamento (Status: Approved ou Rejected).
/// </summary>
public record PaymentProcessedEvent(
    Guid PaymentId,
    Guid OrderId,
    Guid UserId,
    Guid GameId,
    decimal Price,
    string Status,
    string Currency = "BRL",
    string? UserEmail = null,
    string? GameTitle = null,
    DateTime ProcessedAt = default
);

