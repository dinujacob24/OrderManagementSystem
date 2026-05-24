using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using OrderService.Domain;
using OrderService.DTOs;
using OrderService.Features.CancelOrder;
using OrderService.Tests.Helpers;

namespace OrderService.Tests.Integration;

public class OrdersApiTests : IClassFixture<OrderApiFactory>
{
    private readonly OrderApiFactory _factory;
    private readonly HttpClient _client;

    public OrdersApiTests(OrderApiFactory factory)
    {
        _factory = factory;
        _factory.CustomerStub.ShouldSucceed = true;
        _client = factory.CreateAuthenticatedClient();
    }

    private static CreateOrderRequest NewCreateRequest(string customerId = "CUST-001") => new()
    {
        CustomerId = customerId,
        Items = new List<OrderItemDto>
        {
            new() { ProductId = "P1", ProductName = "Widget", Quantity = 2, UnitPrice = 10m }
        }
    };

    [Fact]
    public async Task POST_orders_returns_200_with_response()
    {
        _factory.CustomerStub.ShouldSucceed = true;

        var response = await _client.PostAsJsonAsync("/api/Orders", NewCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CreateOrderResponse>();
        body.Should().NotBeNull();
        body!.OrderId.Should().BeGreaterThan(0);
        body.TotalAmount.Should().Be(20m);
        body.SagaId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task POST_orders_returns_401_without_auth()
    {
        var unauthClient = _factory.CreateClient();

        var response = await unauthClient.PostAsJsonAsync("/api/Orders", NewCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task POST_orders_returns_400_when_customer_validation_fails()
    {
        _factory.CustomerStub.ShouldSucceed = false;
        _factory.CustomerStub.FailureReason = "Customer is suspended";
        try
        {
            var response = await _client.PostAsJsonAsync("/api/Orders", NewCreateRequest());

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            _factory.CustomerStub.ShouldSucceed = true;
        }
    }

    [Fact]
    public async Task POST_orders_returns_400_when_items_empty()
    {
        var bad = new CreateOrderRequest { CustomerId = "CUST-001", Items = new List<OrderItemDto>() };

        var response = await _client.PostAsJsonAsync("/api/Orders", bad);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GET_orders_returns_200_when_found()
    {
        await using var db = _factory.CreateDbContext();
        var order = new OrderBuilder().WithCustomerId("CUST-999").Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/Orders/{order.OrderId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<OrderStatusResponse>();
        body!.OrderId.Should().Be(order.OrderId);
        body.CustomerId.Should().Be("CUST-999");
    }

    [Fact]
    public async Task GET_orders_returns_404_when_missing()
    {
        var response = await _client.GetAsync("/api/Orders/999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_cancel_returns_200_and_marks_cancelled()
    {
        await using var db = _factory.CreateDbContext();
        var order = new OrderBuilder().Pending().Build();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        var request = new CancelOrderRequest { Reason = "Customer requested" };

        var response = await _client.PostAsJsonAsync($"/api/orders/{order.OrderId}/cancel", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CancelOrderResponse>();
        body!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task POST_cancel_returns_400_when_order_missing()
    {
        var request = new CancelOrderRequest { Reason = "n/a" };

        var response = await _client.PostAsJsonAsync("/api/orders/999999/cancel", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
