using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrderService.Domain;
using OrderService.Infrastructure.Database;
using OrderService.Saga;
using OrderService.Tests.Helpers;
using Shared.Messages.Events;

namespace OrderService.Tests.Saga;

public class OrderSagaOrchestratorTests
{
    private static (OrderSagaOrchestrator sut, OrderDbContext db, Mock<IPublishEndpoint> publish)
        Build()
    {
        var db = TestDbContextFactory.Create();
        var publish = new Mock<IPublishEndpoint>();
        var sut = new OrderSagaOrchestrator(db, publish.Object, NullLogger<OrderSagaOrchestrator>.Instance);
        return (sut, db, publish);
    }

    private static async Task<Order> SeedOrderAsync(OrderDbContext db, string status = OrderStatus.Pending, string customerId = "CUST-001", decimal total = 100m)
    {
        var order = new OrderBuilder().WithCustomerId(customerId).WithStatus(status).WithTotalAmount(total).Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    private static async Task<SagaState> SeedSagaAsync(OrderDbContext db, int orderId, Guid? sagaId = null, string currentStep = "OrderCreated", string customerId = "CUST-001")
    {
        var saga = new SagaStateBuilder()
            .WithSagaId(sagaId ?? Guid.NewGuid())
            .WithOrderId(orderId)
            .WithCustomerId(customerId)
            .WithCurrentStep(currentStep)
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();
        return saga;
    }

    // ----- StartOrderSaga -----

    [Fact]
    public async Task StartOrderSaga_CreatesSagaState_AndReturnsSagaId()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db);

        var sagaId = await sut.StartOrderSaga(order, new List<OrderItemDto>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 2, UnitPrice = 10m }
        });

        sagaId.Should().NotBe(Guid.Empty);
        var saga = await db.SagaStates.SingleAsync();
        saga.SagaId.Should().Be(sagaId);
        saga.OrderId.Should().Be(order.OrderId);
        saga.CustomerId.Should().Be("CUST-001");
        saga.CurrentStep.Should().Be("OrderCreated");
        saga.Status.Should().Be(OrderStatus.Pending);
        saga.RetryCount.Should().Be(0);
    }

    [Fact]
    public async Task StartOrderSaga_WritesOrderCreatedEvent_ToOutbox()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db, customerId: "CUST-042", total: 25m);

        await sut.StartOrderSaga(order, new List<OrderItemDto>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 1, UnitPrice = 25m }
        });

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be("OrderCreatedEvent");
        outbox.Processed.Should().BeFalse();
        outbox.Payload.Should().Contain("CUST-042");
    }

    [Fact]
    public async Task StartOrderSaga_AdvancesOrderStatus_ToOrderDetailsProcessing()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db);

        await sut.StartOrderSaga(order, new List<OrderItemDto> { new() { ProductId = "P1", Quantity = 1, UnitPrice = 1m } });

        var persisted = await db.Orders.SingleAsync();
        persisted.Status.Should().Be(OrderStatus.OrderDetailsProcessing);
    }

    // ----- HandleOrderDetailsCompleted -----

    [Fact]
    public async Task HandleOrderDetailsCompleted_Success_AdvancesSagaState()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.OrderDetailsProcessing);
        var saga = await SeedSagaAsync(db, order.OrderId);

        await sut.HandleOrderDetailsCompleted(new OrderDetailsCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        });
        await db.SaveChangesAsync();

        var updated = await db.SagaStates.SingleAsync();
        updated.IsOrderDetailsCompleted.Should().BeTrue();
        updated.CurrentStep.Should().Be("OrderDetailsCompleted");
        var updatedOrder = await db.Orders.SingleAsync();
        updatedOrder.Status.Should().Be(OrderStatus.PaymentProcessing);
        updatedOrder.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleOrderDetailsCompleted_Success_QueuesProcessPaymentCommand_ToOutbox()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.OrderDetailsProcessing, total: 99.95m);
        var saga = await SeedSagaAsync(db, order.OrderId);

        await sut.HandleOrderDetailsCompleted(new OrderDetailsCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        });
        await db.SaveChangesAsync();

        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(nameof(Shared.Messages.Commands.ProcessPaymentCommand));
        outbox.Payload.Should().Contain("99.95");
    }

    [Fact]
    public async Task HandleOrderDetailsCompleted_Failure_CompensatesOrder_AndPublishesEvents()
    {
        var (sut, db, publish) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.OrderDetailsProcessing);
        var saga = await SeedSagaAsync(db, order.OrderId);

        await sut.HandleOrderDetailsCompleted(new OrderDetailsCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = false,
            ErrorMessage = "Out of stock"
        });

        var updated = await db.SagaStates.SingleAsync();
        updated.Status.Should().Be(OrderStatus.Cancelled);
        updated.CurrentStep.Should().Be("Compensated");
        updated.ErrorMessage.Should().Contain("Out of stock");
        updated.CompletedAt.Should().NotBeNull();
        var updatedOrder = await db.Orders.SingleAsync();
        updatedOrder.Status.Should().Be(OrderStatus.Cancelled);
        publish.Verify(p => p.Publish(It.IsAny<Shared.Messages.Events.OrderCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        publish.Verify(p => p.Publish(It.IsAny<Shared.Messages.Commands.SendNotificationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleOrderDetailsCompleted_NoOps_WhenSagaNotFound()
    {
        var (sut, db, publish) = Build();

        await sut.HandleOrderDetailsCompleted(new OrderDetailsCompletedEvent
        {
            SagaId = Guid.NewGuid(),
            OrderId = 999,
            Success = true
        });

        (await db.OutboxMessages.AnyAsync()).Should().BeFalse();
        publish.VerifyNoOtherCalls();
    }

    // ----- HandlePaymentCompleted -----

    [Fact]
    public async Task HandlePaymentCompleted_Success_AdvancesSaga_AndQueuesNotification()
    {
        var (sut, db, _) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.PaymentProcessing);
        var saga = await SeedSagaAsync(db, order.OrderId, currentStep: "OrderDetailsCompleted");

        await sut.HandlePaymentCompleted(new PaymentCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        });
        await db.SaveChangesAsync();

        var updated = await db.SagaStates.SingleAsync();
        updated.IsPaymentCompleted.Should().BeTrue();
        updated.CurrentStep.Should().Be("PaymentCompleted");
        var updatedOrder = await db.Orders.SingleAsync();
        updatedOrder.Status.Should().Be(OrderStatus.NotificationProcessing);
        var outbox = await db.OutboxMessages.SingleAsync();
        outbox.MessageType.Should().Be(Shared.Messages.MessageTypes.SendNotificationCommand);
    }

    [Fact]
    public async Task HandlePaymentCompleted_Failure_CompensatesOrder()
    {
        var (sut, db, publish) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.PaymentProcessing);
        var saga = await SeedSagaAsync(db, order.OrderId);

        await sut.HandlePaymentCompleted(new PaymentCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = false,
            ErrorMessage = "Card declined"
        });

        var updatedOrder = await db.Orders.SingleAsync();
        updatedOrder.Status.Should().Be(OrderStatus.Cancelled);
        publish.Verify(p => p.Publish(It.IsAny<Shared.Messages.Events.OrderCancelledEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ----- HandleNotificationCompleted -----

    [Fact]
    public async Task HandleNotificationCompleted_Success_CompletesSaga_AndPublishesOrderCompletedEvent()
    {
        var (sut, db, publish) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.NotificationProcessing, total: 75m);
        var saga = await SeedSagaAsync(db, order.OrderId, currentStep: "PaymentCompleted");

        await sut.HandleNotificationCompleted(new NotificationCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = true
        });

        var updated = await db.SagaStates.SingleAsync();
        updated.IsNotificationCompleted.Should().BeTrue();
        updated.CurrentStep.Should().Be("Completed");
        updated.Status.Should().Be(OrderStatus.Completed);
        updated.CompletedAt.Should().NotBeNull();
        var updatedOrder = await db.Orders.SingleAsync();
        updatedOrder.Status.Should().Be(OrderStatus.Completed);
        publish.Verify(p => p.Publish(It.IsAny<OrderService.Messages.Events.OrderCompletedEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleNotificationCompleted_Failure_RetriesUpToThreeTimes()
    {
        var (sut, db, publish) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.NotificationProcessing);
        var saga = await SeedSagaAsync(db, order.OrderId);

        await sut.HandleNotificationCompleted(new NotificationCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = false
        });

        var updated = await db.SagaStates.SingleAsync();
        updated.RetryCount.Should().Be(1);
        updated.CurrentStep.Should().NotBe("NotificationFailed");
        publish.Verify(
            p => p.Publish(It.IsAny<Shared.Messages.Commands.SendNotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleNotificationCompleted_GivesUp_AfterMaxRetries()
    {
        var (sut, db, publish) = Build();
        var order = await SeedOrderAsync(db, status: OrderStatus.NotificationProcessing);
        var saga = new SagaStateBuilder()
            .WithOrderId(order.OrderId)
            .WithCurrentStep("PaymentCompleted")
            .Build();
        saga.RetryCount = 3;
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();

        await sut.HandleNotificationCompleted(new NotificationCompletedEvent
        {
            SagaId = saga.SagaId,
            OrderId = order.OrderId,
            Success = false
        });

        var updated = await db.SagaStates.SingleAsync();
        updated.CurrentStep.Should().Be("NotificationFailed");
        updated.ErrorMessage.Should().Contain("maximum retries");
        updated.Status.Should().Be(OrderStatus.Completed);
        publish.Verify(
            p => p.Publish(It.IsAny<Shared.Messages.Commands.SendNotificationCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleNotificationCompleted_NoOps_WhenSagaNotFound()
    {
        var (sut, db, publish) = Build();

        await sut.HandleNotificationCompleted(new NotificationCompletedEvent
        {
            SagaId = Guid.NewGuid(),
            OrderId = 999,
            Success = true
        });

        publish.VerifyNoOtherCalls();
    }
}
