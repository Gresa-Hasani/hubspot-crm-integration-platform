using CrmIntegration.Application.Common;
using CrmIntegration.Application.Webhooks;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CrmIntegration.UnitTests.Webhooks;

public class IntegrationEventRetryServiceTests
{
    [Fact]
    public async Task RetryAsync_ResetsDeadLetteredEventToReceived()
    {
        var repository = new FakeIntegrationEventRepository();
        var unitOfWork = new FakeUnitOfWork();
        var integrationEvent = new IntegrationEvent
        {
            Id = Guid.NewGuid(), ExternalEventId = "1", Status = IntegrationEventStatus.DeadLettered,
            AttemptCount = 3, FailureCategory = "Transient", LastError = "boom", CorrelationId = "corr-1", ReceivedAt = DateTime.UtcNow
        };
        repository.Events.Add(integrationEvent);
        var service = new IntegrationEventRetryService(repository, unitOfWork, NullLogger<IntegrationEventRetryService>.Instance);

        var result = await service.RetryAsync(integrationEvent.Id);

        Assert.Equal(IntegrationEventStatus.Received, result.Status);
        Assert.Equal(0, result.AttemptCount);
        Assert.Null(result.FailureCategory);
        Assert.Null(result.LastError);
    }

    [Fact]
    public async Task RetryAsync_Throws_WhenEventIsNotDeadLettered()
    {
        var repository = new FakeIntegrationEventRepository();
        var integrationEvent = new IntegrationEvent { Id = Guid.NewGuid(), ExternalEventId = "1", Status = IntegrationEventStatus.Processed, CorrelationId = "corr-1", ReceivedAt = DateTime.UtcNow };
        repository.Events.Add(integrationEvent);
        var service = new IntegrationEventRetryService(repository, new FakeUnitOfWork(), NullLogger<IntegrationEventRetryService>.Instance);

        await Assert.ThrowsAsync<DomainValidationException>(() => service.RetryAsync(integrationEvent.Id));
    }

    [Fact]
    public async Task RetryAsync_Throws_WhenEventDoesNotExist()
    {
        var repository = new FakeIntegrationEventRepository();
        var service = new IntegrationEventRetryService(repository, new FakeUnitOfWork(), NullLogger<IntegrationEventRetryService>.Instance);

        await Assert.ThrowsAsync<EntityNotFoundException>(() => service.RetryAsync(Guid.NewGuid()));
    }
}
