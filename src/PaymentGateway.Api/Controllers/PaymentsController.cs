using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Common.Extensions;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController(PaymentService paymentService) : Controller
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PostPaymentResponse>> GetPaymentAsync(Guid id)
    {
        var result = paymentService.GetPayment(id);
        return result.ToActionResult(this);
    }
    
    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync([FromBody] PostPaymentRequest request)
    {
        var result = await paymentService.ProcessPaymentAsync(request);
        return result.ToActionResult(this);
    }
}