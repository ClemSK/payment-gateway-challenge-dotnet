using PaymentGateway.Api.Models.Domain;
using PaymentGateway.Api.Repositories.Payment;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository : IPaymentRepository
{
    public List<Payment> Payments = new();

    public void Add(Payment payment)
    {
        Payments.Add(payment);
    }

    public Payment? Get(Guid paymentId)
    {
        return Payments.FirstOrDefault(p => p.Id == paymentId);
    }
}