using System.Net;
using System.Net.Http.Json;

using FluentResults;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
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
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
            .ReturnsAsync(Result.Ok(new BankSimulatorResponse
            {
                Authorized = true, AuthorizationCode = authorizationCode.ToString()
            }));

        var webApplicationFactory = new WebApplicationFactory<PaymentsController>();

        var client = webApplicationFactory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton(bankSimulatorMock.Object);
                }))
            .CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/Payments", request);
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paymentResponse);
        Assert.Equal(PaymentStatus.Authorized, paymentResponse.Status);
        Assert.Equal(1111, paymentResponse.CardNumberLastFour);
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
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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
        var payment = new PostPaymentResponse
        {
            Id = Guid.NewGuid(),
            ExpiryYear = _random.Next(2023, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999),
            Currency = "GBP",
            Status = PaymentStatus.Authorized
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
        var paymentResponse = await response.Content.ReadFromJsonAsync<PostPaymentResponse>();

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
            .Setup(x => x.ProcessPaymentAsync(It.IsAny<BankSimulatorRequest>()))
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
}