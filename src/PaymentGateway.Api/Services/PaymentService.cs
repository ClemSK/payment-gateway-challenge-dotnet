using FluentResults;

using PaymentGateway.Api.Common.Extensions;
using PaymentGateway.Api.Common.GuidGenerator;
using PaymentGateway.Api.Common.Mapping;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
using PaymentGateway.Api.Models.Domain;
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
    public async Task<Result<PaymentResponse>> ProcessPaymentAsync(PostPaymentRequest request)
    {
        var validationResult = await new PostPaymentRequestValidator().ValidateAsync(request);

        if (!validationResult.IsValid)
        {
            return Result.Fail<PaymentResponse>(new PaymentError(PaymentErrorType.ServiceUnavailable,
                validationResult.Errors));
        }

        var bankResult = await bankSimulator.ProcessPaymentAsync(request.ToBankSimulatorRequest());

        if (bankResult.IsFailed)
        {
            return Result.Fail<PaymentResponse>(bankResult.ToPaymentError());
        }

        var status = bankResult.Value.Authorized ? PaymentStatus.Authorized : PaymentStatus.Declined;
        return Result.Ok(CreateAndStorePayment(request, status, bankResult.Value.AuthorizationCode)
            .ToPaymentResponse());
    }

    private Payment CreateAndStorePayment(PostPaymentRequest request, PaymentStatus status, string authorizationCode)
    {
        var payment = new Payment
        {
            Id = guidGenerator.NewGuid(),
            Status = status,
            CardNumberLastFour = request.GetCardNumberLastFour(),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount,
            AuthorizationCode = string.IsNullOrEmpty(authorizationCode) ? null : authorizationCode
        };

        paymentRepository.Add(payment);
        return payment;
    }

    public Result<PaymentResponse> GetPayment(Guid paymentId)
    {
        var payment = paymentRepository.Get(paymentId);

        if (payment == null)
        {
            return Result.Fail<PaymentResponse>(new PaymentError(PaymentErrorType.NotFound, "Payment not found"));
        }

        return Result.Ok(payment.ToPaymentResponse());
    }
}