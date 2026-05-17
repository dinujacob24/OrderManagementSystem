using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace CustomerService.Tests.Integration;

public class HealthCheckTests : IClassFixture<CustomerApiFactory>
{
    private readonly HttpClient _client;

    public HealthCheckTests(CustomerApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_health_returns_200_with_healthy_status()
    {
        var response = await _client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.Should().Contain("\"status\": \"Healthy\"");
    }

    [Fact]
    public async Task GET_health_returns_database_check_entry()
    {
        var response = await _client.GetAsync("/health");

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().Contain(c => c.GetProperty("name").GetString() == "database");
        var dbCheck = checks.Single(c => c.GetProperty("name").GetString() == "database");
        dbCheck.GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task GET_health_live_returns_200_even_without_dependencies()
    {
        var response = await _client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        doc.RootElement.GetProperty("checks").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task GET_health_ready_runs_only_ready_tagged_checks()
    {
        var response = await _client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var checks = doc.RootElement.GetProperty("checks").EnumerateArray().ToList();
        checks.Should().HaveCount(1);
        checks[0].GetProperty("name").GetString().Should().Be("database");
        checks[0].GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString()).Should().Contain("ready");
    }

    [Fact]
    public async Task GET_health_returns_application_json_content_type()
    {
        var response = await _client.GetAsync("/health");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
