namespace OrderService.Features.CancelOrder;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/orders")]
[Authorize]
public class CancelOrderController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<CancelOrderController> _logger;

    public CancelOrderController(
        IMediator mediator,
        ILogger<CancelOrderController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost("{orderId}/cancel")]
    public async Task<ActionResult<CancelOrderResponse>> CancelOrder(
        int orderId,
        [FromBody] CancelOrderRequest request)
    {
        _logger.LogInformation("Cancelling order {OrderId}", orderId);

        var command = new CancelOrderCommand
        {
            OrderId = orderId,
            Reason = request.Reason
        };

        var response = await _mediator.Send(command);

        if (!response.Success)
        {
            return BadRequest(response);
        }

        return Ok(response);
    }
}
