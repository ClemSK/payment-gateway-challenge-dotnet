using FluentResults;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Infrastructure.Clients.BankSimulator;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;

namespace PaymentGateway.Api.Services;

public class PaymentService(
    ILogger<PaymentService> logger,
    IPaymentRepository paymentRepository,
    IBankSimulator bankSimulator)
{

    public PaymentService(ILogger<PaymentService> logger, IPaymentRepository paymentRepository)
    {
        _logger = logger;
        _paymentRepository = paymentRepository;
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