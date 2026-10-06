using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Ramai.Infrastructure.Connectivity;

namespace Ramai.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Theory]
    [InlineData(true, true, HttpStatusCode.OK)]
    [InlineData(false, true, HttpStatusCode.ServiceUnavailable)]
    [InlineData(true, false, HttpStatusCode.ServiceUnavailable)]
    [InlineData(false, false, HttpStatusCode.ServiceUnavailable)]
    public async Task Health_aggregates_dependencies_and_preserves_correlation(
        bool postgres, bool redis, HttpStatusCode expected)
    {
        using var factory = new ApiFactory(postgres, redis);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", "integration-check");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("integration-check", Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("integration-check", json.RootElement.GetProperty("correlationId").GetString());
        var checks = json.RootElement.GetProperty("checks");
        Assert.Equal("Healthy", checks.GetProperty("application").GetProperty("status").GetString());
        Assert.Equal(postgres ? "Healthy" : "Unhealthy", checks.GetProperty("postgresql").GetProperty("status").GetString());
        Assert.Equal(redis ? "Healthy" : "Unhealthy", checks.GetProperty("redis").GetProperty("status").GetString());
        Assert.Equal(3, checks.EnumerateObject().Count());
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unknown_route_still_returns_a_generated_correlation_id()
    {
        using var factory = new ApiFactory(true, true);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/unknown-foundation-route");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(Guid.TryParseExact(Assert.Single(response.Headers.GetValues("X-Correlation-ID")), "N", out _));
    }

    private sealed class ApiFactory(bool postgres, bool redis) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IInfrastructureConnectivityVerifier>();
                services.AddSingleton<IInfrastructureConnectivityVerifier>(new FakeVerifier(postgres, redis));
            });
        }
    }

    private sealed class FakeVerifier(bool postgres, bool redis) : IInfrastructureConnectivityVerifier
    {
        public Task<ConnectivityCheckResult> CheckPostgreSqlAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectivityCheckResult("postgresql", postgres, postgres ? null : "Unavailable"));
        public Task<ConnectivityCheckResult> CheckRedisAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConnectivityCheckResult("redis", redis, redis ? null : "Unavailable"));
    }
}
