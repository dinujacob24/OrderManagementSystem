using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderDetailsService.DTOs;

namespace OrderDetailsService.Features.GetOrderItems
{
    [ApiController]
    [Route("api/orders/{orderId}/items")]
    public class GetOrderItemsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public GetOrderItemsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<ActionResult<List<OrderItemResponse>>> GetOrderItems(int orderId)
        {
            var query = new GetOrderItemsQuery(orderId);
            var items = await _mediator.Send(query);

            if (!items.Any())
            {
                return NotFound(new { message = "No items found for this order" });
            }

            return Ok(items);
        }
    }
}
