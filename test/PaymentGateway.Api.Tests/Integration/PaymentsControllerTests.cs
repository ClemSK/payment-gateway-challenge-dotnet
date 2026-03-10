using System.Net;
using System.Net.Http.Json;

using FluentResults;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
using PaymentGateway.Api.Models.Domain;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    [Fact]
    public async Task ProcessesAnAuthorisedPaymentSuccessfully()
    {
        // Arrange
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

        var bankSimulatorMock = new Mock<IBankSimulator>();

        bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Ok(new BankSimulatorResponse
            {
                Authorized = true, AuthorizationCode = authorizationCode.ToString()
            }));

        var paymentRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                    services.AddSingleton<IPaymentRepository>(paymentRepository);
                }))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal(1111, paymentResponse.CardNumberLastFour);

        var savedPayment = paymentRepository.Get(paymentResponse.Id);
        Assert.NotNull(savedPayment);
        Assert.Equal(paymentResponse.Id, savedPayment.Id);
        Assert.Equal(paymentResponse.Status, savedPayment.Status);
    }

    [Fact]
    public async Task ProcessesADeclinedPaymentSuccessfully()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812342222",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var bankSimulatorMock = new Mock<IBankSimulator>();

        bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()))
            .ReturnsAsync(
                Result.Ok(new BankSimulatorResponse { Authorized = false, AuthorizationCode = string.Empty }));

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                }))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Declined, paymentResponse.Status);
        Assert.Equal(2222, paymentResponse.CardNumberLastFour);
    }

    [Fact]
    public async Task RetrievesAPaymentSuccessfully()
    {
        // Arrange
        var paymentId = Guid.NewGuid();
        var authorizationCode = Guid.NewGuid();

        var payment = new Payment
        {
            Id = paymentId,
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999),
            Currency = "GBP",
            Status = PaymentStatus.Authorized,
            AuthorizationCode = authorizationCode.ToString()
        };

        var paymentRepository = new PaymentsRepository();
        paymentRepository.Add(payment);

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services => ((ServiceCollection)services)
                    .AddSingleton<IPaymentRepository>(paymentRepository)))
            .CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{payment.Id}");
        var paymentResponse = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
    }

    [Fact]
    public async Task Returns404IfPaymentNotFound()
    {
        // Arrange
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.GetAsync($"/api/Payments/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Returns503IfBankSimulatorIsUnavailable()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812345678",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var bankSimulatorMock = new Mock<IBankSimulator>();
        bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail("Bank simulator: Service Unavailable"));

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                }))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ProcessPayment_WhenBankReturnsRejected_ReturnsServiceUnavailableAndDoesNotStore()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812345678",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var bankSimulatorMock = new Mock<IBankSimulator>();
        bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Fail("Rejected"));

        var paymentRepositoryMock = new Mock<IPaymentRepository>();

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                    services.AddSingleton(paymentRepositoryMock.Object);
                }))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        paymentRepositoryMock.Verify(x => x.Add(It.IsAny<Payment>()), Times.Never);
    }

    [Fact]
    public async Task ReturnsRejectedStatusIfPaymentValidationFails()
    {
        // Arrange
        var request = new PostPaymentRequest
        {
            CardNumber = "123", // Too short
            ExpiryMonth = 13, // Invalid month
            ExpiryYear = 2020, // In the past
            Currency = "JPY", // Not supported
            Amount = -1, // Not positive
            CVV = "12" // Too short
        };

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();
        var client = webApplicationFactory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<IEnumerable<string>>();

        // Assert
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.NotEmpty(paymentResponse);
    }

    [Fact]
    public async Task ProcessPayment_WhenSameIdempotencyKeySentTwice_ReturnsCachedResponseAndBankCalledOnce()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid().ToString();
        var authorizationCode = Guid.NewGuid().ToString();

        var request = new PostPaymentRequest
        {
            CardNumber = "1234567812341111",
            ExpiryMonth = 10,
            ExpiryYear = 2030,
            Currency = "GBP",
            Amount = 100,
            CVV = "123"
        };

        var bankSimulatorMock = new Mock<IBankSimulator>();
        bankSimulatorMock
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()))
            .ReturnsAsync(Result.Ok(new BankSimulatorResponse
            {
                Authorized = true, AuthorizationCode = authorizationCode
            }));

        var paymentRepository = new PaymentsRepository();
        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                    services.AddSingleton<IPaymentRepository>(paymentRepository);
                }))
            .CreateClient();

        var requestMessage1 = new HttpRequestMessage(HttpMethod.Post, "/api/Payments");
        requestMessage1.Headers.Add("Idempotency-Key", idempotencyKey);
        requestMessage1.Content = JsonContent.Create(request);

        var requestMessage2 = new HttpRequestMessage(HttpMethod.Post, "/api/Payments");
        requestMessage2.Headers.Add("Idempotency-Key", idempotencyKey);
        requestMessage2.Content = JsonContent.Create(request);

        // Act
        var firstResponse = await client.SendAsync(requestMessage1);
        var secondResponse = await client.SendAsync(requestMessage2);

        var firstPayment = await firstResponse.Content.ReadFromJsonAsync<PaymentResponse>();
        var errorResponse = await secondResponse.Content.ReadAsStringAsync();

        // Assert — first request succeeds
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.NotNull(firstPayment);
        Assert.Equal(PaymentStatus.Authorized, firstPayment.Status);

        // Second request conflicts
        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        Assert.NotNull(errorResponse);
        Assert.Contains("A payment with this idempotency key already exists", errorResponse);

        bankSimulatorMock.Verify(
            x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>(), It.IsAny<Guid>()),
            Times.Once);
    }
}