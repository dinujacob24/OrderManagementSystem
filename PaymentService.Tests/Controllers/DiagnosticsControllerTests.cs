using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using PaymentService.Controllers;
using PaymentService.Domain;
using PaymentService.Tests.Helpers;

namespace PaymentService.Tests.Controllers;

public class DiagnosticsControllerTests
{
    [Fact]
    public async Task GetPayments_WithEmptyDb_Returns200WithEmptyList()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new DiagnosticsController(db);

        var result = await sut.GetPayments();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeAssignableTo<IEnumerable<Payment>>().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPayments_Returns50MostRecent_OrderedByCreatedAtDesc()
    {
        await using var db = TestDbContextFactory.Create();
        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 0; i < 75; i++)
        {
            db.Payments.Add(new PaymentBuilder()
                .WithOrderId(i)
                .WithCreatedAt(baseTime.AddMinutes(i))
                .Build());
        }
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetPayments();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var list = ok.Value.Should().BeAssignableTo<List<Payment>>().Subject;
        list.Should().HaveCount(50);
        list.Should().BeInDescendingOrder(p => p.CreatedAt);
        list[0].OrderId.Should().Be(74);
    }

    [Fact]
    public async Task GetPaymentByOrderId_WhenFound_ReturnsOkWithPayment()
    {
        await using var db = TestDbContextFactory.Create();
        var payment = new PaymentBuilder().WithOrderId(123).Build();
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetPaymentByOrderId(123);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<Payment>().Which.OrderId.Should().Be(123);
    }

    [Fact]
    public async Task GetPaymentByOrderId_WhenNotFound_Returns404WithMessage()
    {
        await using var db = TestDbContextFactory.Create();
        var sut = new DiagnosticsController(db);

        var result = await sut.GetPaymentByOrderId(404);

        var nf = result.Should().BeOfType<NotFoundObjectResult>().Subject;
        nf.Value.Should().Be("Payment not found for this order");
    }

    [Fact]
    public async Task GetOutboxMessages_FiltersOnlyPaymentRelatedMessageTypes()
    {
        await using var db = TestDbContextFactory.Create();
        db.OutboxMessages.AddRange(
            new OutboxMessageBuilder().WithMessageType("ProcessPaymentCommand").Build(),
            new OutboxMessageBuilder().WithMessageType("PaymentCompletedEvent").Build(),
            new OutboxMessageBuilder().WithMessageType("refund-completed").Build(),
            new OutboxMessageBuilder().WithMessageType("OrderCreatedEvent").Build()
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetOutboxMessages();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value!;
        var totalProp = payload.GetType().GetProperty("totalMessages")!.GetValue(payload);
        totalProp.Should().Be(2);
    }

    [Fact]
    public async Task GetOutboxMessages_TruncatesPayloadOver200Chars_WithEllipsis()
    {
        await using var db = TestDbContextFactory.Create();
        var longPayload = new string('A', 500);
        db.OutboxMessages.Add(new OutboxMessageBuilder()
            .WithMessageType("ProcessPaymentCommand")
            .WithPayload(longPayload)
            .Build());
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetOutboxMessages();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("messages")!.GetValue(ok.Value)!;
        var first = messages.Cast<object>().Single();
        var preview = (string)first.GetType().GetProperty("PayloadPreview")!.GetValue(first)!;
        preview.Should().HaveLength(203);
        preview.Should().EndWith("...");
        preview.Should().StartWith(new string('A', 200));
    }

    [Fact]
    public async Task GetOutboxMessages_ShortPayload_ReturnedInFullWithoutEllipsis()
    {
        await using var db = TestDbContextFactory.Create();
        db.OutboxMessages.Add(new OutboxMessageBuilder()
            .WithMessageType("PaymentCompletedEvent")
            .WithPayload("short")
            .Build());
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetOutboxMessages();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = (System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("messages")!.GetValue(ok.Value)!;
        var first = messages.Cast<object>().Single();
        var preview = (string)first.GetType().GetProperty("PayloadPreview")!.GetValue(first)!;
        preview.Should().Be("short");
    }

    [Fact]
    public async Task GetOutboxMessages_OrdersByCreatedAtDescending()
    {
        await using var db = TestDbContextFactory.Create();
        var t0 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        db.OutboxMessages.AddRange(
            new OutboxMessageBuilder().WithMessageType("ProcessPaymentCommand").WithCreatedAt(t0).Build(),
            new OutboxMessageBuilder().WithMessageType("ProcessPaymentCommand").WithCreatedAt(t0.AddMinutes(10)).Build(),
            new OutboxMessageBuilder().WithMessageType("ProcessPaymentCommand").WithCreatedAt(t0.AddMinutes(5)).Build()
        );
        await db.SaveChangesAsync();

        var sut = new DiagnosticsController(db);
        var result = await sut.GetOutboxMessages();

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var messages = ((System.Collections.IEnumerable)ok.Value!.GetType().GetProperty("messages")!.GetValue(ok.Value)!)
            .Cast<object>()
            .Select(m => (DateTime)m.GetType().GetProperty("CreatedAt")!.GetValue(m)!)
            .ToList();

        messages.Should().BeInDescendingOrder();
    }
}
