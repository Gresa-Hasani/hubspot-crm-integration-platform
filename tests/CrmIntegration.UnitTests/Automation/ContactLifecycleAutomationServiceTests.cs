using CrmIntegration.Application.Automation;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Automation;

public class ContactLifecycleAutomationServiceTests
{
    private static (ContactLifecycleAutomationService Service, FakeContactLifecycleTransitionRepository TransitionRepository, FakeAutomationExecutor AutomationExecutor) CreateService()
    {
        var transitionRepository = new FakeContactLifecycleTransitionRepository();
        var automationExecutor = new FakeAutomationExecutor();
        var service = new ContactLifecycleAutomationService(
            transitionRepository, automationExecutor, new FakeUnitOfWork(),
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            NullLogger<ContactLifecycleAutomationService>.Instance);
        return (service, transitionRepository, automationExecutor);
    }

    [Fact]
    public async Task EvaluateAsync_ReturnsNull_WhenLifecycleStageUnchanged()
    {
        var (service, transitionRepository, automationExecutor) = CreateService();
        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@b.example", LifecycleStage = LifecycleStage.Lead, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(contact, previousStage: LifecycleStage.Lead, TransitionSource.HubSpotSync, "corr-1");

        Assert.Null(result);
        Assert.Empty(transitionRepository.Transitions);
        Assert.Empty(automationExecutor.Calls);
    }

    [Fact]
    public async Task EvaluateAsync_RecordsTransitionAndTriggersAutomation_WhenLifecycleStageChanges()
    {
        var (service, transitionRepository, automationExecutor) = CreateService();
        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@b.example", LifecycleStage = LifecycleStage.Customer, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(contact, previousStage: LifecycleStage.Opportunity, TransitionSource.InternalUpdate, "corr-1");

        Assert.NotNull(result);
        Assert.Equal(LifecycleStage.Opportunity, result!.FromStage);
        Assert.Equal(LifecycleStage.Customer, result.ToStage);
        Assert.Single(transitionRepository.Transitions);

        var call = Assert.Single(automationExecutor.Calls);
        Assert.Equal(AutomationType.ContactLifecycleTransition, call.AutomationType);
        Assert.Equal(EntityType.Contact, call.EntityType);
        Assert.Equal(contact.Id, call.EntityId);
        Assert.Equal($"ContactLifecycleTransition:{result.Id}", call.IdempotencyKey);
    }

    [Fact]
    public async Task EvaluateAsync_RecordsTransitionWithNullFromStage_WhenContactIsNew()
    {
        var (service, transitionRepository, _) = CreateService();
        var contact = new Contact { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", Email = "a@b.example", LifecycleStage = LifecycleStage.Lead, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

        var result = await service.EvaluateAsync(contact, previousStage: null, TransitionSource.InternalUpdate, "corr-1");

        Assert.NotNull(result);
        Assert.Null(result!.FromStage);
        Assert.Single(transitionRepository.Transitions);
    }
}
