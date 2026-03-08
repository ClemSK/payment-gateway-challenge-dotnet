using Microsoft.AspNetCore.Mvc;

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

        if (result.IsFailed)
        {
            return NotFound();
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<PostPaymentResponse>> PostPaymentAsync([FromBody] PostPaymentRequest request)
    {
        var result = await paymentService.ProcessPaymentAsync(request);

        if (result.IsFailed)
        {
            if (result.Errors.Any(e =>
                    e.Message.Contains("Service Unavailable") || e.Message.Contains("Error connecting")))
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, result.Errors);
            }

            return BadRequest(result.Errors);
        }

        return Ok(result.Value);
    }
}