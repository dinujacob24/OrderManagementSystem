using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Domain;
using OrderService.Features.CreateOrder;
using OrderService.Infrastructure.CustomerClient;
using OrderService.Saga;
using OrderService.Tests.Helpers;

namespace OrderService.Tests.Features.CreateOrder;

public class CreateOrderCommandHandlerTests
{
    private static CreateOrderCommand Cmd(
        string customerId = "CUST-001",
        List<OrderItemCommand>? items = null) =>
        new(customerId, items ?? new List<OrderItemCommand>
        {
            new("PROD-1", "Widget", 2, 10m)
        });

    private static (CreateOrderCommandHandler handler, OrderService.Infrastructure.Database.OrderDbContext db, Mock<ICustomerValidationClient> customerMock, Mock<IPublishEndpoint> publishMock)
        Build(bool customerValid = true)
    {
        var db = TestDbContextFactory.Create();
        var publishMock = new Mock<IPublishEndpoint>();
        var saga = new OrderSagaOrchestrator(db, publishMock.Object, NullLogger<OrderSagaOrchestrator>.Instance);
        var customerMock = new Mock<ICustomerValidationClient>();
        customerMock
            .Setup(c => c.ValidateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(customerValid
                ? CustomerValidationResult.Ok()
                : CustomerValidationResult.Fail("Customer 'CUST-001' is not active."));
        var handler = new CreateOrderCommandHandler(
            db,
            saga,
            customerMock.Object,
            NullLogger<CreateOrderCommandHandler>.Instance);
        return (handler, db, customerMock, publishMock);
    }

    [Fact]
    public async Task Handle_PersistsOrder_AndReturnsResponse()
    {
        var (sut, db, _, _) = Build();

        var response = await sut.Handle(Cmd(), CancellationToken.None);

        response.CustomerId.Should().Be("CUST-001");
        response.TotalAmount.Should().Be(20m);
        response.OrderId.Should().BeGreaterThan(0);
        response.Message.Should().NotBeNullOrEmpty();
        var persisted = await db.Orders.SingleAsync();
        persisted.CustomerId.Should().Be("CUST-001");
        persisted.TotalAmount.Should().Be(20m);
    }

    [Fact]
    public async Task Handle_CalculatesTotalAmount_FromAllItems()
    {
        var (sut, db, _, _) = Build();
        var items = new List<OrderItemCommand>
        {
            new("PROD-1", "Widget", 2, 10m),
            new("PROD-2", "Gizmo", 3, 5.5m),
            new("PROD-3", "Doodad", 1, 100m)
        };

        var response = await sut.Handle(Cmd(items: items), CancellationToken.None);

        // 2*10 + 3*5.5 + 1*100 = 20 + 16.5 + 100 = 136.5
        response.TotalAmount.Should().Be(136.5m);
        var persisted = await db.Orders.SingleAsync();
        persisted.TotalAmount.Should().Be(136.5m);
    }

    [Fact]
    public async Task Handle_SetsCreatedAt_AndOrderDate_ToRecentUtcNow()
    {
        var (sut, db, _, _) = Build();
        var before = DateTime.UtcNow;

        await sut.Handle(Cmd(), CancellationToken.None);

        var after = DateTime.UtcNow;
        var persisted = await db.Orders.SingleAsync();
        persisted.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        persisted.OrderDate.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task Handle_StartsSaga_AndReturnsSagaId()
    {
        var (sut, db, _, _) = Build();

        var response = await sut.Handle(Cmd(), CancellationToken.None);

        response.SagaId.Should().NotBe(Guid.Empty);
        var sagaState = await db.SagaStates.SingleAsync();
        sagaState.SagaId.Should().Be(response.SagaId);
        sagaState.OrderId.Should().Be(response.OrderId);
        sagaState.CustomerId.Should().Be("CUST-001");
    }

    [Fact]
    public async Task Handle_AdvancesOrderStatus_ToOrderDetailsProcessing_AfterSaga()
    {
        var (sut, db, _, _) = Build();

        var response = await sut.Handle(Cmd(), CancellationToken.None);

        response.Status.Should().Be(OrderStatus.OrderDetailsProcessing);
        var persisted = await db.Orders.SingleAsync();
        persisted.Status.Should().Be(OrderStatus.OrderDetailsProcessing);
    }

    [Fact]
    public async Task Handle_WritesOrderCreatedEvent_ToOutbox()
    {
        var (sut, db, _, _) = Build();

        await sut.Handle(Cmd(), CancellationToken.None);

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be("OrderCreatedEvent");
        outbox.Processed.Should().BeFalse();
        outbox.Payload.Should().Contain("CUST-001");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task Handle_ThrowsArgumentException_WhenCustomerIdEmpty(string? customerId)
    {
        var (sut, _, _, _) = Build();

        var act = () => sut.Handle(Cmd(customerId: customerId!), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*CustomerId*");
    }

    [Fact]
    public async Task Handle_ThrowsArgumentException_WhenItemsListEmpty()
    {
        var (sut, _, _, _) = Build();

        var act = () => sut.Handle(Cmd(items: new List<OrderItemCommand>()), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*one order item*");
    }

    [Fact]
    public async Task Handle_ThrowsArgumentException_WhenItemsListNull()
    {
        var (sut, _, _, _) = Build();
        var cmd = new CreateOrderCommand("CUST-001", null!);

        var act = () => sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*one order item*");
    }

    [Fact]
    public async Task Handle_ThrowsArgumentException_WhenCustomerValidationFails()
    {
        var (sut, db, _, _) = Build(customerValid: false);

        var act = () => sut.Handle(Cmd(), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*not active*");
        (await db.Orders.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CallsCustomerValidation_Once_WithCustomerId()
    {
        var (sut, _, customerMock, _) = Build();

        await sut.Handle(Cmd(customerId: "CUST-042"), CancellationToken.None);

        customerMock.Verify(
            c => c.ValidateAsync("CUST-042", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PersistsNothing_WhenCustomerValidationFails()
    {
        var (sut, db, _, _) = Build(customerValid: false);

        try { await sut.Handle(Cmd(), CancellationToken.None); }
        catch (ArgumentException) { }

        (await db.Orders.AnyAsync()).Should().BeFalse();
        (await db.SagaStates.AnyAsync()).Should().BeFalse();
        (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
    }
}
