using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories.Payment;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentRepository
{
    public List<PostPaymentResponse> Payments = new();

    public void Add(PostPaymentResponse payment)
    {
        Payments.Add(payment);
    }

    public PostPaymentResponse? Get(Guid paymentId)
    {
        return Payments.FirstOrDefault(p => p.Id == paymentId);
    }
}