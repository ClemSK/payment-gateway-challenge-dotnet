using FluentResults;

using PaymentGateway.Api.Common.GuidGenerator;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;
using PaymentGateway.Api.Validation;

namespace PaymentGateway.Api.Services;

public class PaymentService(
    ILogger<PaymentService> logger,
    IPaymentRepository paymentRepository,
    IBankSimulator bankSimulator,
    IGuidGenerator guidGenerator
)
{
    public async Task<Result<PostPaymentResponse>> ProcessPaymentAsync(PostPaymentRequest request)
    {
        var validator = new PostPaymentRequestValidator();
        var validationResult = await validator.ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return Result.Fail<PostPaymentResponse>(validationResult.Errors.Select(e => e.ErrorMessage));
        }

        var bankRequest = new BankSimulatorRequest
        {
            CardNumber = request.CardNumber,
            ExpiryDate = $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency = request.Currency,
            Amount = request.Amount,
            Cvv = request.CVV
        };

        var bankResult = await bankSimulator.ProcessPaymentAsync(bankRequest);

        if (bankResult.IsFailed)
        {
            return Result.Fail<PostPaymentResponse>(bankResult.Errors);
        }

        var paymentResponse = new PostPaymentResponse
        {
            Id = guidGenerator.NewGuid(),
            Status = bankResult.Value.Authorized
                ? PaymentStatus.Authorized
                : PaymentStatus.Declined,
            CardNumberLastFour = int.Parse(request.CardNumber[^4..]),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        paymentRepository.Add(paymentResponse);

        return Result.Ok(paymentResponse);
    }

    public Result<PostPaymentResponse> GetPayment(Guid paymentId)
    {
        var payment = paymentRepository.Get(paymentId);

        if (payment == null)
        {
            return Result.Fail("Payment not found");
        }

        return Result.Ok(payment);
    }
}