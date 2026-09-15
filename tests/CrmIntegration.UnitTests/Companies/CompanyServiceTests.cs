using CrmIntegration.Application.Companies;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Companies;

public class CompanyServiceTests
{
    private static CompanyService CreateService(out FakeCompanyRepository repository, out FakeUnitOfWork unitOfWork)
    {
        repository = new FakeCompanyRepository();
        unitOfWork = new FakeUnitOfWork();
        return new CompanyService(repository, unitOfWork, new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task CreateAsync_NormalizesDomainAndName()
    {
        var service = CreateService(out var repository, out var unitOfWork);

        var result = await service.CreateAsync(new CreateCompanyRequest
        {
            Name = "  Acme   Technologies  ",
            Domain = "https://www.Acme.example/",
            Country = "USA"
        });

        Assert.Equal("Acme Technologies", result.Name);
        Assert.Equal("acme.example", result.Domain);
        Assert.Equal("United States", result.Country);
        Assert.Equal(new DateTime(2026, 1, 1), result.CreatedAt);
        Assert.Equal(1, unitOfWork.SaveChangesCallCount);
        Assert.Single(repository.Companies);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNull_WhenCompanyDoesNotExist()
    {
        var service = CreateService(out _, out _);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
