using MediatR;
using Microsoft.AspNetCore.Mvc;
using OrderService.DTOs;

namespace OrderService.Features.CreateOrder
{
    // ⚠️ WARNING: This is a SYNCHRONOUS version - NOT RECOMMENDED for production!
    // This is only for educational purposes to show the difference.

    [ApiController]
    [Route("api/[controller]")]
    public class OrdersControllerSync : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<OrdersControllerSync> _logger;

        public OrdersControllerSync(IMediator mediator, ILogger<OrdersControllerSync> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpPost]
        public ActionResult<CreateOrderResponse> CreateOrder([FromBody] CreateOrderRequest request)
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

                // ❌ BAD: .Result blocks the thread!
                var result = _mediator.Send(command).Result;

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
        public ActionResult<OrderStatusResponse> GetOrderStatus(int orderId)
        {
            var query = new GetOrderStatusQuery(orderId);

            // ❌ BAD: .Result blocks the thread!
            var result = _mediator.Send(query).Result;

            if (result == null)
            {
                return NotFound(new { error = $"Order {orderId} not found" });
            }

            return Ok(result);
        }
    }
}
