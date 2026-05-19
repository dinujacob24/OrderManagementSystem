using FluentAssertions;
using OrderService.Domain;
using OrderService.Features.CreateOrder;
using OrderService.Tests.Helpers;

namespace OrderService.Tests.Features.GetOrderStatus;

public class GetOrderStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOrderStatus_WhenOrderExists()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder()
            .WithCustomerId("CUST-001")
            .WithTotalAmount(250.75m)
            .WithStatus(OrderStatus.PaymentProcessing)
            .Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var sut = new GetOrderStatusQueryHandler(db);

        var result = await sut.Handle(new GetOrderStatusQuery(order.OrderId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.OrderId.Should().Be(order.OrderId);
        result.CustomerId.Should().Be("CUST-001");
        result.TotalAmount.Should().Be(250.75m);
        result.Status.Should().Be(OrderStatus.PaymentProcessing);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new GetOrderStatusQueryHandler(db);

        var result = await sut.Handle(new GetOrderStatusQuery(9999), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_LeavesSagaStatusNull_WhenNoSagaState()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var sut = new GetOrderStatusQueryHandler(db);

        var result = await sut.Handle(new GetOrderStatusQuery(order.OrderId), CancellationToken.None);

        result!.SagaStatus.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PopulatesSagaStatus_WhenSagaStateExists()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var sagaId = Guid.NewGuid();
        var saga = new SagaStateBuilder()
            .WithSagaId(sagaId)
            .WithOrderId(order.OrderId)
            .WithCurrentStep("PaymentCompleted")
            .WithOrderDetailsCompleted()
            .WithPaymentCompleted()
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();
        var sut = new GetOrderStatusQueryHandler(db);

        var result = await sut.Handle(new GetOrderStatusQuery(order.OrderId), CancellationToken.None);

        result!.SagaStatus.Should().NotBeNull();
        result.SagaStatus!.SagaId.Should().Be(sagaId);
        result.SagaStatus.CurrentStep.Should().Be("PaymentCompleted");
        result.SagaStatus.IsOrderDetailsCompleted.Should().BeTrue();
        result.SagaStatus.IsPaymentCompleted.Should().BeTrue();
        result.SagaStatus.IsNotificationCompleted.Should().BeFalse();
        result.SagaStatus.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Handle_SurfacesErrorMessage_FromSagaState()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.Cancelled).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var saga = new SagaStateBuilder()
            .WithOrderId(order.OrderId)
            .WithCurrentStep("Compensated")
            .WithErrorMessage("Payment processing failed")
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();
        var sut = new GetOrderStatusQueryHandler(db);

        var result = await sut.Handle(new GetOrderStatusQuery(order.OrderId), CancellationToken.None);

        result!.Status.Should().Be(OrderStatus.Cancelled);
        result.SagaStatus!.ErrorMessage.Should().Be("Payment processing failed");
    }
}
