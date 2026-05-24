using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PaymentService.Domain;
using PaymentService.Tests.Helpers;

namespace PaymentService.Tests.Infrastructure;

public class PaymentDbContextModelTests
{
    [Fact]
    public void Payment_Amount_HasDecimalPrecision18_2()
    {
        using var db = TestDbContextFactory.Create();
        var amountProp = db.Model.FindEntityType(typeof(Payment))!
            .FindProperty(nameof(Payment.Amount))!;

        amountProp.GetPrecision().Should().Be(18);
        amountProp.GetScale().Should().Be(2);
    }

    [Fact]
    public void Payment_HasIndexOnSagaId()
    {
        using var db = TestDbContextFactory.Create();
        var entity = db.Model.FindEntityType(typeof(Payment))!;

        entity.GetIndexes().Should().Contain(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(Payment.SagaId));
    }

    [Fact]
    public void Payment_HasIndexOnOrderId()
    {
        using var db = TestDbContextFactory.Create();
        var entity = db.Model.FindEntityType(typeof(Payment))!;

        entity.GetIndexes().Should().Contain(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == nameof(Payment.OrderId));
    }

    [Fact]
    public void OutboxMessage_HasCompositeIndexOnMessageTypeAndProcessed()
    {
        using var db = TestDbContextFactory.Create();
        var entity = db.Model.FindEntityType(typeof(OutboxMessage))!;

        entity.GetIndexes().Should().Contain(i =>
            i.Properties.Count == 2
            && i.Properties[0].Name == nameof(OutboxMessage.MessageType)
            && i.Properties[1].Name == nameof(OutboxMessage.Processed));
    }
}
