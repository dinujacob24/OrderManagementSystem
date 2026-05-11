using MediatR;
using OrderDetailsService.DTOs;

namespace OrderDetailsService.Features.GetOrderItems
{
    public record GetOrderItemsQuery(int OrderId) : IRequest<List<OrderItemResponse>>;
}
