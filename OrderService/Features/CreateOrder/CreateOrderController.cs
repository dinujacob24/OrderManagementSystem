using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;

namespace OrderService.Features.CreateOrder
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(IMediator mediator, ILogger<OrdersController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult<CreateOrderResponse>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                // Map DTO to Command
                var command = new CreateOrderCommand(
                    request.CustomerId,
                    request.Items.Select(item => new OrderItemCommand(
                        item.ProductId,
                        item.ProductName,
                        item.Quantity,
                        item.UnitPrice
                    )).ToList()
                );

                var result = await _mediator.Send(command);

                _logger.LogInformation("Order {OrderId} created successfully", result.OrderId);

                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Invalid order request: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return StatusCode(500, new { error = "An error occurred while creating the order" });
            }
        }

        [HttpGet("{orderId}")]
        public async Task<ActionResult<OrderStatusResponse>> GetOrderStatus(int orderId)
        {
            var query = new GetOrderStatusQuery(orderId);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return NotFound(new { error = $"Order {orderId} not found" });
            }

            return Ok(result);
        }
    }
}
