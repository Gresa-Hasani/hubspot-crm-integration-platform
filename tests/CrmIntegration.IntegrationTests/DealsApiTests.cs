using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Deals;
using CrmIntegration.Domain.Enums;
using Xunit;

namespace CrmIntegration.IntegrationTests;

public class DealsApiTests : IClassFixture<CrmApiFactory>
{
    private readonly HttpClient _client;

    public DealsApiTests(CrmApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateThenUpdate_ToClosedWon_DerivesWonStatus()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/deals", new CreateDealRequest
        {
            Name = "Enterprise Platform Subscription",
            Stage = DealStage.QualifiedToBuy,
            Amount = 25000,
            Currency = "eur"
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<DealResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(DealStatus.Open, created!.Status);
        Assert.Equal("EUR", created.Currency);

        var updateResponse = await _client.PutAsJsonAsync($"/api/deals/{created.Id}", new UpdateDealRequest
        {
            Name = created.Name,
            Stage = DealStage.ClosedWon,
            Amount = created.Amount,
            Currency = created.Currency
        });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<DealResponse>(CrmApiFactory.JsonOptions);
        Assert.Equal(DealStatus.Won, updated!.Status);
        Assert.Equal(DealStage.ClosedWon, updated.Stage);
    }

    [Fact]
    public async Task Create_ReturnsBadRequest_WhenCurrencyIsNotThreeLetters()
    {
        var response = await _client.PostAsJsonAsync("/api/deals", new CreateDealRequest
        {
            Name = "Bad Currency Deal",
            Amount = 100,
            Currency = "EU"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_ReturnsNotFound_ForUnknownDeal()
    {
        var response = await _client.PutAsJsonAsync($"/api/deals/{Guid.NewGuid()}", new UpdateDealRequest
        {
            Name = "Ghost Deal",
            Amount = 10,
            Currency = "EUR"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
