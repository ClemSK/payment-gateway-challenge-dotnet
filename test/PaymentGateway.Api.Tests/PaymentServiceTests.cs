using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<ILogger<PaymentService>> _loggerMock;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _loggerMock = new Mock<ILogger<PaymentService>>();
        _sut = new PaymentService(_loggerMock.Object, _paymentRepositoryMock.Object);
    }

    [Fact]
    public void GetPayment_WhenPaymentExists_ReturnsPayment()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var expected = new PostPaymentResponse
        {
            Id = paymentId,
            Amount = 10050,
            Status = PaymentStatus.Authorized,
            CardNumberLastFour = 4321,
            ExpiryMonth = 10,
            ExpiryYear = 2026,
            Currency = "GBP"
        };

        _paymentRepositoryMock
            .Setup(x => x.Get(paymentId))
            .Returns(expected);

        // Act
        var actual = _sut.GetPayment(paymentId);

        // Assert
        actual.Should().NotBeNull();
        actual.IsSuccess.Should().BeTrue();
        actual.Value.Should().BeOfType<PostPaymentResponse>();

        actual.Value.Should().BeEquivalentTo(expected);

        _paymentRepositoryMock.Verify(x => x.Get(paymentId), Times.Once);
    }

    [Fact]
    public void GetPayment_WhenPaymentDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        _paymentRepositoryMock.Setup(x => x.Get(paymentId)).Returns((PostPaymentResponse?)null);

        // Act
        var actual = _sut.GetPayment(paymentId);

        // Assert
        actual.IsFailed.Should().BeTrue();
        actual.Errors.Should().ContainSingle(e => e.Message == "Payment not found");
    }
}