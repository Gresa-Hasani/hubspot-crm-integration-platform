using CrmIntegration.Application.Common;
using CrmIntegration.Application.Contacts;
using CrmIntegration.Domain.Entities;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Contacts;

public class ContactServiceTests
{
    private static ContactService CreateService(
        out FakeContactRepository contactRepository,
        out FakeCompanyRepository companyRepository,
        out FakeUnitOfWork unitOfWork)
    {
        contactRepository = new FakeContactRepository();
        companyRepository = new FakeCompanyRepository();
        unitOfWork = new FakeUnitOfWork();
        return new ContactService(
            contactRepository,
            companyRepository,
            unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
    }

    [Fact]
    public async Task CreateAsync_NormalizesEmailAndPhone()
    {
        var service = CreateService(out _, out _, out _);

        var result = await service.CreateAsync(new CreateContactRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "  Alice@ACME.example  ",
            Phone = "+1 (555) 123-4567"
        });

        Assert.Equal("alice@acme.example", result.Email);
        Assert.Equal("+15551234567", result.Phone);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenCompanyIdDoesNotExist()
    {
        var service = CreateService(out _, out _, out _);
        var missingCompanyId = Guid.NewGuid();

        await Assert.ThrowsAsync<DomainValidationException>(() => service.CreateAsync(new CreateContactRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@acme.example",
            CompanyId = missingCompanyId
        }));
    }

    [Fact]
    public async Task CreateAsync_Succeeds_WhenCompanyIdExists()
    {
        var service = CreateService(out _, out var companyRepository, out _);
        var company = new Company { Id = Guid.NewGuid(), Name = "Acme", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        companyRepository.Companies.Add(company);

        var result = await service.CreateAsync(new CreateContactRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@acme.example",
            CompanyId = company.Id
        });

        Assert.Equal(company.Id, result.CompanyId);
    }

    [Fact]
    public async Task UpdateAsync_Throws_WhenContactDoesNotExist()
    {
        var service = CreateService(out _, out _, out _);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), new UpdateContactRequest
        {
            FirstName = "Alice",
            LastName = "Smith",
            Email = "alice@acme.example"
        }));
    }
}
