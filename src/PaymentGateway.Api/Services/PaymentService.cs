using FluentResults;

using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;

namespace PaymentGateway.Api.Services;

public class PaymentService
{
    private readonly ILogger _logger;
    private readonly IPaymentRepository _paymentRepository;

    public PaymentService(ILogger<PaymentService> logger, IPaymentRepository paymentRepository)
    {
        _logger = logger;
        _paymentRepository = paymentRepository;
    }

    public Result<PostPaymentResponse> GetPayment(Guid paymentId)
    {
        var payment = _paymentRepository.Get(paymentId);

        if (payment == null)
        {
            return Result.Fail("Payment not found");
        }

        return Result.Ok(payment);
    }
}