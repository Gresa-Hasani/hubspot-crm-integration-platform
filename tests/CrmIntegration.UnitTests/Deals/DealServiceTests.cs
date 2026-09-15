using CrmIntegration.Application.Common;
using CrmIntegration.Application.Deals;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Deals;

public class DealServiceTests
{
    private static DealService CreateService(
        out FakeDealRepository dealRepository,
        out FakeCompanyRepository companyRepository,
        out FakeContactRepository contactRepository)
    {
        dealRepository = new FakeDealRepository();
        companyRepository = new FakeCompanyRepository();
        contactRepository = new FakeContactRepository();
        return new DealService(
            dealRepository,
            companyRepository,
            contactRepository,
            new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(DealStage.QualifiedToBuy, DealStatus.Open)]
    [InlineData(DealStage.Proposal, DealStatus.Open)]
    [InlineData(DealStage.Negotiation, DealStatus.Open)]
    [InlineData(DealStage.ClosedWon, DealStatus.Won)]
    [InlineData(DealStage.ClosedLost, DealStatus.Lost)]
    public async Task CreateAsync_DerivesStatusFromStage(DealStage stage, DealStatus expectedStatus)
    {
        var service = CreateService(out _, out _, out _);

        var result = await service.CreateAsync(new CreateDealRequest
        {
            Name = "Enterprise Platform Subscription",
            Stage = stage,
            Amount = 25000,
            Currency = "eur"
        });

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal("EUR", result.Currency);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCurrencyIsNotThreeLetters()
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateAsync(new CreateDealRequest
        {
            Name = "Bad Currency Deal",
            Amount = 100,
            Currency = "EU"
        }));
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenContactIdDoesNotExist()
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateAsync(new CreateDealRequest
        {
            Name = "Orphan Contact Deal",
            Amount = 100,
            Currency = "EUR",
            ContactId = Guid.NewGuid()
        }));
    }
}
