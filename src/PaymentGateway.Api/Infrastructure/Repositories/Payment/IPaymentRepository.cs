using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Repositories.Payment;

public interface IPaymentRepository
{
    void Add(PostPaymentResponse payment);
    PostPaymentResponse? Get(Guid paymentId);
}