using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using dotnet.challenge.api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace dotnet.challenge.tests;

/// <summary>HTTP contract: status codes, Location header and JSON shape (README "HTTP status codes").</summary>
public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    private async Task<Guid> CreateProductAsync(int stock)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "Widget", stock });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private async Task<HttpResponseMessage> ReserveAsync(Guid productId, int quantity, int ttlSeconds = 60) =>
        await _client.PostAsJsonAsync("/api/reservations", new { productId, quantity, ttlSeconds });

    [Fact]
    public async Task CreateProduct_Returns201WithLocation()
    {
        var response = await _client.PostAsJsonAsync("/api/products", new { name = "Widget", stock = 5 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        (await _client.GetAsync(response.Headers.Location)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("{\"name\":\"\",\"stock\":1}")]
    [InlineData("{\"name\":\"Widget\",\"stock\":-1}")]
    public async Task CreateProduct_InvalidBody_Returns400(string body)
    {
        var response = await _client.PostAsync("/api/products", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reserve_Returns201_AndStatusIsSerialisedAsString()
    {
        var productId = await CreateProductAsync(5);

        var response = await ReserveAsync(productId, 2);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetString().Should().Be("Active");
    }

    [Fact]
    public async Task Reserve_InsufficientStock_Returns409()
    {
        var productId = await CreateProductAsync(1);
        (await ReserveAsync(productId, 2)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reserve_UnknownProduct_Returns404()
    {
        (await ReserveAsync(Guid.NewGuid(), 1)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("{\"quantity\":1,\"ttlSeconds\":60}")]
    [InlineData("{\"productId\":\"6f1c2a3b-0000-0000-0000-000000000001\",\"quantity\":0,\"ttlSeconds\":60}")]
    [InlineData("{\"productId\":\"6f1c2a3b-0000-0000-0000-000000000001\",\"quantity\":1,\"ttlSeconds\":0}")]
    public async Task Reserve_InvalidBody_Returns400(string body)
    {
        var response = await _client.PostAsync("/api/reservations", new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Confirm_IsIdempotent_And_ReleaseAfterConfirmReturns204()
    {
        var productId = await CreateProductAsync(5);
        var reservationId = (await (await ReserveAsync(productId, 2)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        (await _client.PostAsync($"/api/reservations/{reservationId}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.PostAsync($"/api/reservations/{reservationId}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _client.DeleteAsync($"/api/reservations/{reservationId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.DeleteAsync($"/api/reservations/{reservationId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _client.PostAsync($"/api/reservations/{reservationId}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UnknownReservation_Returns404_ForGetConfirmAndRelease()
    {
        var id = Guid.NewGuid();
        (await _client.GetAsync($"/api/reservations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.PostAsync($"/api/reservations/{id}/confirm", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _client.DeleteAsync($"/api/reservations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
