using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using OrderDetailsService.DTOs;
using OrderDetailsService.Features.GetOrderItems;

namespace OrderDetailsService.Tests.Features.GetOrderItems;

public class GetOrderItemsControllerTests
{
    [Fact]
    public async Task GetOrderItems_ReturnsOk_WithItems_WhenAnyExist()
    {
        var items = new List<OrderItemResponse>
        {
            new() { OrderItemId = 1, OrderId = 42, ProductId = "P-1", ProductName = "X", Quantity = 1, UnitPrice = 10m, TotalPrice = 10m, Status = "Added", CreatedAt = DateTime.UtcNow }
        };
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.Is<GetOrderItemsQuery>(q => q.OrderId == 42), It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);
        var sut = new GetOrderItemsController(mediator.Object);

        var result = await sut.GetOrderItems(42);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(items);
    }

    [Fact]
    public async Task GetOrderItems_ReturnsNotFound_WhenListIsEmpty()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetOrderItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemResponse>());
        var sut = new GetOrderItemsController(mediator.Object);

        var result = await sut.GetOrderItems(42);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetOrderItems_SendsQueryWithRouteOrderId()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetOrderItemsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OrderItemResponse> { new() { OrderItemId = 1 } });
        var sut = new GetOrderItemsController(mediator.Object);

        await sut.GetOrderItems(123);

        mediator.Verify(m => m.Send(It.Is<GetOrderItemsQuery>(q => q.OrderId == 123), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrderItems_PropagatesMediatorException()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetOrderItemsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        var sut = new GetOrderItemsController(mediator.Object);

        var act = async () => await sut.GetOrderItems(1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
    }
}
