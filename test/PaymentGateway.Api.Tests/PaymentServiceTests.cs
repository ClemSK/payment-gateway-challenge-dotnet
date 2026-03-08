using FluentAssertions;

using FluentResults;

using Microsoft.Extensions.Logging;

using Moq;

using PaymentGateway.Api.Common.GuidGenerator;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Repositories.Payment;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    private readonly Mock<IPaymentRepository> _paymentRepositoryMock;
    private readonly Mock<IBankSimulator> _bankSimulatorMock;
    private readonly Mock<IGuidGenerator> _guidGeneratorMock;
    private readonly Mock<ILogger<PaymentService>> _loggerMock;
    private readonly PaymentService _sut;

    public PaymentServiceTests()
    {
        _paymentRepositoryMock = new Mock<IPaymentRepository>();
        _bankSimulatorMock = new Mock<IBankSimulator>();
        _guidGeneratorMock = new Mock<IGuidGenerator>();
        _loggerMock = new Mock<ILogger<PaymentService>>();
        _sut = new PaymentService(_loggerMock.Object, _paymentRepositoryMock.Object, _bankSimulatorMock.Object,
            _guidGeneratorMock.Object);
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

    [Fact]
    public async Task ProcessPayment_WhenBankSimulatorReturnsAuthorized_ReturnsAuthorizedResponse()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var authorizationCode = Guid.NewGuid();

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812341111",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var expected = new PostPaymentResponse()
        {
            Id = paymentId,
            CardNumberLastFour = 1111,
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            Status = PaymentStatus.Authorized
        };

        var bankResponse =
            new BankSimulatorResponse { Authorized = true, AuthorizationCode = authorizationCode.ToString() };

        _bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
            .ReturnsAsync(Result.Ok(bankResponse));

        _guidGeneratorMock
            .Setup(x => x.NewGuid())
            .Returns(paymentId);

        // Act
        var actual = await _sut.ProcessPaymentAsync(request);

        // Assert
        actual.IsSuccess.Should().BeTrue();
        actual.Value.Should().BeEquivalentTo(expected);

        _paymentRepositoryMock.Verify(x => x.Add(It.IsAny<PostPaymentResponse>()), Times.Once);
    }

    [Fact]
    public async Task ProcessPayment_WhenBankSimulatorReturnsUnauthorized_ReturnsDeclinedResponse()
    {
        // Arrange
        var paymentId = Guid.NewGuid();

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812342222",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var expected = new PostPaymentResponse()
        {
            Id = paymentId,
            CardNumberLastFour = 2222,
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            Status = PaymentStatus.Declined
        };

        var bankResponse = new BankSimulatorResponse { Authorized = false, AuthorizationCode = "" };

        _bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
            .ReturnsAsync(Result.Ok(bankResponse));

        _guidGeneratorMock
            .Setup(x => x.NewGuid())
            .Returns(paymentId);

        // Act
        var actual = await _sut.ProcessPaymentAsync(request);

        // Assert
        actual.IsSuccess.Should().BeTrue();
        actual.Value.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ProcessPayment_WhenBankSimulatorIsUnavailable_ReturnsServiceUnavailableResult()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812340000",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        _bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
            .ReturnsAsync(Result.Fail("Bank simulator: Service Unavailable"));

        // Act
        var actual = await _sut.ProcessPaymentAsync(request);

        // Assert
        actual.IsFailed.Should().BeTrue();
        actual.Errors.Should().ContainSingle(e => e.Message.Contains("Service Unavailable"));
    }
}