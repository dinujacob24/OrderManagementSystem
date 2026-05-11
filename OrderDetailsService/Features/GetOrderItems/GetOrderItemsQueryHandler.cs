using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderDetailsService.DTOs;
using OrderDetailsService.Infrastructure.Database;

namespace OrderDetailsService.Features.GetOrderItems
{
    public class GetOrderItemsQueryHandler : IRequestHandler<GetOrderItemsQuery, List<OrderItemResponse>>
    {
        private readonly OrderDetailsDbContext _context;
        private readonly ILogger<GetOrderItemsQueryHandler> _logger;

        public GetOrderItemsQueryHandler(OrderDetailsDbContext context, ILogger<GetOrderItemsQueryHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<OrderItemResponse>> Handle(GetOrderItemsQuery request, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Getting order items for OrderId: {OrderId}", request.OrderId);

            var items = await _context.OrderItems
                .Where(oi => oi.OrderId == request.OrderId)
                .Select(oi => new OrderItemResponse
                {
                    OrderItemId = oi.OrderItemId,
                    OrderId = oi.OrderId,
                    ProductId = oi.ProductId,
                    ProductName = oi.ProductName,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    TotalPrice = oi.TotalPrice,
                    Status = oi.Status,
                    CreatedAt = oi.CreatedAt
                })
                .ToListAsync(cancellationToken);

            return items;
        }
    }
}
