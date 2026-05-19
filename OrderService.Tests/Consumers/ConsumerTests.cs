using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Consumers;
using OrderService.Domain;
using OrderService.Saga;
using OrderService.Tests.Helpers;
using Shared.Messages.Events;

namespace OrderService.Tests.Consumers;

public class ConsumerTests
{
    [Fact]
    public async Task OrderDetailsCompletedConsumer_RoutesEvent_ToOrchestrator()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.OrderDetailsProcessing).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var saga = new SagaStateBuilder().WithOrderId(order.OrderId).Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();

        var orchestrator = new OrderSagaOrchestrator(db, Mock.Of<IPublishEndpoint>(), NullLogger<OrderSagaOrchestrator>.Instance);
        var sut = new OrderDetailsCompletedConsumer(orchestrator, NullLogger<OrderDetailsCompletedConsumer>.Instance);
        var evt = new OrderDetailsCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        };
        var ctx = new Mock<ConsumeContext<OrderDetailsCompletedEvent>>();
        ctx.Setup(c => c.Message).Returns(evt);

        await sut.Consume(ctx.Object);
        await db.SaveChangesAsync();

        var updated = await db.SagaStates.SingleAsync();
        updated.IsOrderDetailsCompleted.Should().BeTrue();
        updated.CurrentStep.Should().Be("OrderDetailsCompleted");
    }

    [Fact]
    public async Task PaymentCompletedConsumer_RoutesEvent_ToOrchestrator()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.PaymentProcessing).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var saga = new SagaStateBuilder()
            .WithOrderId(order.OrderId)
            .WithCurrentStep("OrderDetailsCompleted")
            .WithOrderDetailsCompleted()
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();

        var orchestrator = new OrderSagaOrchestrator(db, Mock.Of<IPublishEndpoint>(), NullLogger<OrderSagaOrchestrator>.Instance);
        var sut = new PaymentCompletedConsumer(orchestrator, NullLogger<PaymentCompletedConsumer>.Instance);
        var evt = new PaymentCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        };
        var ctx = new Mock<ConsumeContext<PaymentCompletedEvent>>();
        ctx.Setup(c => c.Message).Returns(evt);

        await sut.Consume(ctx.Object);
        await db.SaveChangesAsync();

        var updated = await db.SagaStates.SingleAsync();
        updated.IsPaymentCompleted.Should().BeTrue();
        updated.CurrentStep.Should().Be("PaymentCompleted");
    }

    [Fact]
    public async Task NotificationCompletedConsumer_RoutesEvent_ToOrchestrator()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.NotificationProcessing).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var saga = new SagaStateBuilder()
            .WithOrderId(order.OrderId)
            .WithCurrentStep("PaymentCompleted")
            .WithPaymentCompleted()
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();

        var publish = new Mock<IPublishEndpoint>();
        var orchestrator = new OrderSagaOrchestrator(db, publish.Object, NullLogger<OrderSagaOrchestrator>.Instance);
        var sut = new NotificationCompletedConsumer(orchestrator, NullLogger<NotificationCompletedConsumer>.Instance);
        var evt = new NotificationCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        };
        var ctx = new Mock<ConsumeContext<NotificationCompletedEvent>>();
        ctx.Setup(c => c.Message).Returns(evt);

        await sut.Consume(ctx.Object);

        var updated = await db.SagaStates.SingleAsync();
        updated.IsNotificationCompleted.Should().BeTrue();
        updated.Status.Should().Be(OrderStatus.Completed);
        publish.Verify(p => p.Publish(It.IsAny<OrderService.Messages.Events.OrderCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RefundCompletedConsumer_Success_MarksOrderRefunded()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.Cancelled).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var sut = new RefundCompletedConsumer(db, NullLogger<RefundCompletedConsumer>.Instance);

        await sut.ConsumeAsync(new RefundCompletedEvent
        {
            OrderId = order.OrderId,
            Success = true,
            Message = "OK"
        });

        var updated = await db.Orders.SingleAsync();
        updated.Status.Should().Be(OrderStatus.Refunded);
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefundCompletedConsumer_Failure_KeepsOrderInCurrentStatus()
    {
        await using var db = TestDbContextFactory.Create();
        var order = new OrderBuilder().WithStatus(OrderStatus.Cancelled).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var sut = new RefundCompletedConsumer(db, NullLogger<RefundCompletedConsumer>.Instance);

        await sut.ConsumeAsync(new RefundCompletedEvent
        {
            OrderId = order.OrderId,
            Success = false,
            Message = "Refund declined"
        });

        var updated = await db.Orders.SingleAsync();
        updated.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task RefundCompletedConsumer_NoOps_WhenOrderNotFound()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new RefundCompletedConsumer(db, NullLogger<RefundCompletedConsumer>.Instance);

        await sut.ConsumeAsync(new RefundCompletedEvent
        {
            OrderId = 9999,
            Success = true
        });

        (await db.Orders.AnyAsync()).Should().BeFalse();
    }
}
