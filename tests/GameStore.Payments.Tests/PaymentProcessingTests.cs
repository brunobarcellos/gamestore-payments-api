using FluentAssertions;
using GameStore.Contracts.Events;
using GameStore.Payments.Application.Services;
using GameStore.Payments.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GameStore.Payments.Tests;

public class PaymentProcessingTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock = new();
    private readonly Mock<IEventPublisher> _eventPublisherMock = new();

    private PaymentService CreateSut()
    {
        return new PaymentService(
            _paymentRepositoryMock.Object,
            _eventPublisherMock.Object,
            NullLogger<PaymentService>.Instance
        );
    }

    [Fact]
    public async Task ProcessOrderPaymentAsync_WithValidOrder_ShouldApproveAndPublishPaymentProcessedEvent()
    {
        // Arrange
        var orderEvent = new OrderPlaceEvent(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Price: 149.90m,
            Currency: "BRL",
            UserEmail: "gamer@gamestore.com",
            GameTitle: "Cyber Realm 2077"
        );

        var sut = CreateSut();

        // Act
        var response = await sut.ProcessOrderPaymentAsync(orderEvent);

        // Assert
        response.Should().NotBeNull();
        response.Status.Should().Be(PaymentStatus.Approved);
        response.Price.Should().Be(149.90m);

        _paymentRepositoryMock.Verify(r => r.AddAsync(It.IsAny<PaymentTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _paymentRepositoryMock.Verify(r => r.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);

        _eventPublisherMock.Verify(p => p.PublishAsync(
            It.Is<PaymentProcessedEvent>(e =>
                e.OrderId == orderEvent.OrderId &&
                e.UserId == orderEvent.UserId &&
                e.GameId == orderEvent.GameId &&
                e.Price == 149.90m &&
                e.Status == PaymentStatus.Approved),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProcessOrderPaymentAsync_WithAmountAboveLimit_ShouldRejectAndPublishPaymentProcessedEventWithRejectedStatus()
    {
        // Arrange
        var orderEvent = new OrderPlaceEvent(
            OrderId: Guid.NewGuid(),
            UserId: Guid.NewGuid(),
            GameId: Guid.NewGuid(),
            Price: 99999.00m,
            Currency: "BRL"
        );

        var sut = CreateSut();

        // Act
        var response = await sut.ProcessOrderPaymentAsync(orderEvent);

        // Assert
        response.Status.Should().Be(PaymentStatus.Rejected);
        response.RejectionReason.Should().NotBeNullOrWhiteSpace();

        _eventPublisherMock.Verify(p => p.PublishAsync(
            It.Is<PaymentProcessedEvent>(e =>
                e.OrderId == orderEvent.OrderId &&
                e.Status == PaymentStatus.Rejected),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

