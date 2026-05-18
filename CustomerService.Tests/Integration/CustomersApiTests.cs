using System.Net;
using System.Net.Http.Json;
using CustomerService.Common.Enums;
using CustomerService.Features.CreateCustomer;
using CustomerService.Features.DeactivateCustomer;
using CustomerService.Features.GetCustomerById;
using CustomerService.Features.ListCustomers;
using CustomerService.Features.ReactivateCustomer;
using CustomerService.Features.UpdateCustomer;
using CustomerService.Tests.Helpers;
using FluentAssertions;

namespace CustomerService.Tests.Integration;

public class CustomersApiTests : IClassFixture<CustomerApiFactory>
{
    private readonly CustomerApiFactory _factory;
    private readonly HttpClient _client;

    public CustomersApiTests(CustomerApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CreateCustomerRequest NewCreateRequest(string id = "CUST-001") =>
        new(id, "Alice", "Smith", $"{id.ToLower()}@example.com", "555", "1 Main St", "Townsville", "US");

    [Fact]
    public async Task POST_customer_returns_201_with_response()
    {
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        var response = await _client.PostAsJsonAsync("/api/customers", NewCreateRequest(id));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>();
        body.Should().NotBeNull();
        body!.CustomerId.Should().Be(id);
    }

    [Fact]
    public async Task POST_customer_with_invalid_id_returns_400()
    {
        var bad = new CreateCustomerRequest("bad-id", "A", "B", "a@b.com", null, null, null, null);
        var response = await _client.PostAsJsonAsync("/api/customers", bad);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_customer_with_missing_required_fields_returns_400()
    {
        var bad = new CreateCustomerRequest("CUST-100", "", "", "not-an-email", null, null, null, null);
        var response = await _client.PostAsJsonAsync("/api/customers", bad);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_duplicate_customer_id_returns_409()
    {
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        var first = await _client.PostAsJsonAsync("/api/customers", NewCreateRequest(id));
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await _client.PostAsJsonAsync("/api/customers", NewCreateRequest(id));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GET_customer_by_id_returns_200()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();

        var response = await _client.GetAsync($"/api/customers/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetCustomerByIdResponse>();
        body!.CustomerId.Should().Be(id);
    }

    [Fact]
    public async Task GET_customer_by_id_returns_404_for_missing()
    {
        var response = await _client.GetAsync($"/api/customers/CUST-{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_customers_returns_list()
    {
        await using var db = _factory.CreateDbContext();
        var id1 = $"CUST-{Random.Shared.Next(100000, 999999)}";
        var id2 = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id1).WithEmail($"{id1}@example.com").Build());
        db.Customers.Add(new CustomerBuilder().WithId(id2).WithEmail($"{id2}@example.com").Build());
        await db.SaveChangesAsync();

        var response = await _client.GetAsync("/api/customers");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<ListCustomersResponse>>();
        body!.Select(c => c.CustomerId).Should().Contain(new[] { id1, id2 });
    }

    [Fact]
    public async Task PUT_customer_returns_200_and_updates_fields()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();
        var update = new UpdateCustomerRequest("Updated", "User", $"updated-{id}@example.com", "999", "New", "NewCity", "CA");

        var response = await _client.PutAsJsonAsync($"/api/customers/{id}", update);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UpdateCustomerResponse>();
        body!.FirstName.Should().Be("Updated");
        body.LastName.Should().Be("User");
    }

    [Fact]
    public async Task PUT_customer_returns_404_for_missing()
    {
        var update = new UpdateCustomerRequest("Updated", "User", "u@example.com", null, null, null, null);

        var response = await _client.PutAsJsonAsync("/api/customers/CUST-DOES-NOT-EXIST", update);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PUT_customer_returns_400_on_validation_failure()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();
        var bad = new UpdateCustomerRequest("", "", "not-an-email", null, null, null, null);

        var response = await _client.PutAsJsonAsync($"/api/customers/{id}", bad);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PATCH_deactivate_sets_status_to_inactive()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).Active().WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();

        var response = await _client.PatchAsync($"/api/customers/{id}/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<DeactivateCustomerResponse>();
        body!.Status.Should().Be(CustomerStatus.Inactive);
    }

    [Fact]
    public async Task PATCH_deactivate_returns_404_for_missing()
    {
        var response = await _client.PatchAsync("/api/customers/CUST-NO-SUCH/deactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PATCH_reactivate_sets_status_to_active()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).Inactive().WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();

        var response = await _client.PatchAsync($"/api/customers/{id}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReactivateCustomerResponse>();
        body!.Status.Should().Be(CustomerStatus.Active);
    }

    [Fact]
    public async Task PATCH_reactivate_suspended_returns_409()
    {
        await using var db = _factory.CreateDbContext();
        var id = $"CUST-{Random.Shared.Next(100000, 999999)}";
        db.Customers.Add(new CustomerBuilder().WithId(id).Suspended().WithEmail($"{id}@example.com").Build());
        await db.SaveChangesAsync();

        var response = await _client.PatchAsync($"/api/customers/{id}/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PATCH_reactivate_returns_404_for_missing()
    {
        var response = await _client.PatchAsync("/api/customers/CUST-NO-SUCH/reactivate", null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
