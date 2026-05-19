using System.Net;
using System.Text.Json;
using FluentAssertions;
using OrderService.Domain;
using OrderService.Tests.Helpers;

namespace OrderService.Tests.Integration;

public class DiagnosticsApiTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public DiagnosticsApiTests(OrderApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_diagnostics_outbox_returns_200_and_lists_messages()
    {
        await using var db = _factory.CreateDbContext();
        db.OutboxMessages.Add(new OrderService.Infrastructure.Database.OutboxMessage
        {
            MessageType = "OrderCreatedEvent",
            Payload = "{\"OrderId\":1}",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/Diagnostics/outbox");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("totalMessages");
        json.Should().Contain("OrderCreatedEvent");
    }

    [Fact]
    public async Task GET_diagnostics_saga_returns_order_and_saga_state()
    {
        await using var db = _factory.CreateDbContext();
        var order = new OrderBuilder().WithCustomerId("CUST-DIAG").Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var saga = new SagaStateBuilder()
            .WithOrderId(order.OrderId)
            .WithCurrentStep("OrderCreated")
            .WithCustomerId("CUST-DIAG")
            .Build();
        db.SagaStates.Add(saga);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/Diagnostics/saga/{order.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("order").GetProperty("customerId").GetString().Should().Be("CUST-DIAG");
        doc.RootElement.GetProperty("sagaState").GetProperty("currentStep").GetString().Should().Be("OrderCreated");
    }

    [Fact]
    public async Task POST_diagnostics_test_saga_update_returns_404_when_missing()
    {
        var response = await _client.PostAsync("/api/Diagnostics/test-saga-update/999999", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
