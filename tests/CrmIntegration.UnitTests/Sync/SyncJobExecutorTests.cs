using CrmIntegration.Application.Common;
using CrmIntegration.Application.Integrations.HubSpot;
using CrmIntegration.Application.Sync;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CrmIntegration.UnitTests.Sync;

public class SyncJobExecutorTests
{
    private static SyncJobExecutor CreateExecutor(
        FakeSyncJobRepository jobRepo, FakeAuditLogRepository auditRepo, FakeUnitOfWork unitOfWork,
        InstantRetryDelayProvider? delayProvider = null, SyncRetryOptions? retryOptions = null)
    {
        var executor = new HubSpotRetryExecutor(
            delayProvider ?? new InstantRetryDelayProvider(),
            Options.Create(retryOptions ?? new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }),
            NullLogger<HubSpotRetryExecutor>.Instance);

        return new SyncJobExecutor(
            jobRepo, auditRepo, executor, unitOfWork,
            new FixedTimeProvider(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            Options.Create(retryOptions ?? new SyncRetryOptions { MaxAttempts = 3, BaseDelay = TimeSpan.FromMilliseconds(1) }),
            NullLogger<SyncJobExecutor>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsSucceeded_OnSuccess()
    {
        var jobRepo = new FakeSyncJobRepository();
        var auditRepo = new FakeAuditLogRepository();
        var executor = CreateExecutor(jobRepo, auditRepo, new FakeUnitOfWork());
        var internalId = Guid.NewGuid();

        var job = await executor.ExecuteAsync(
            EntityType.Contact, SyncDirection.InternalToHubSpot, internalId, null, "corr-1",
            _ => Task.FromResult(new SyncActionResult(SyncResultKind.Created, internalId, "999")));

        Assert.Equal(SyncStatus.Succeeded, job.Status);
        Assert.Equal("999", job.ExternalId);
        Assert.Equal(1, job.AttemptCount);
        Assert.NotNull(job.CompletedAt);
        Assert.Contains(auditRepo.Entries, e => e.Action == "SyncSucceeded:Created");
    }

    [Fact]
    public async Task ExecuteAsync_MarksFailed_ForDeterministicException_AndRethrows()
    {
        var jobRepo = new FakeSyncJobRepository();
        var auditRepo = new FakeAuditLogRepository();
        var executor = CreateExecutor(jobRepo, auditRepo, new FakeUnitOfWork());

        await Assert.ThrowsAsync<HubSpotBadRequestException>(() => executor.ExecuteAsync(
            EntityType.Contact, SyncDirection.InternalToHubSpot, Guid.NewGuid(), null, "corr-2",
            Task<SyncActionResult> (_) => throw new HubSpotBadRequestException("bad", null)));

        var job = Assert.Single(jobRepo.Jobs);
        Assert.Equal(SyncStatus.Failed, job.Status);
        Assert.Equal("BadRequest", job.FailureCategory);
        Assert.Equal(1, job.AttemptCount);
        Assert.Contains(auditRepo.Entries, e => e.Action == "SyncFailed");
    }

    [Fact]
    public async Task ExecuteAsync_MarksDeadLettered_WhenRetryableFailureExhaustsAttempts()
    {
        var jobRepo = new FakeSyncJobRepository();
        var auditRepo = new FakeAuditLogRepository();
        var executor = CreateExecutor(jobRepo, auditRepo, new FakeUnitOfWork(), retryOptions: new SyncRetryOptions { MaxAttempts = 2, BaseDelay = TimeSpan.FromMilliseconds(1) });

        var job = await executor.ExecuteAsync(
            EntityType.Deal, SyncDirection.InternalToHubSpot, Guid.NewGuid(), null, "corr-3",
            Task<SyncActionResult> (_) => throw new HubSpotServerException("down", System.Net.HttpStatusCode.ServiceUnavailable, null));

        Assert.Equal(SyncStatus.DeadLettered, job.Status);
        Assert.Equal("ServerError", job.FailureCategory);
        Assert.Equal(2, job.AttemptCount);
        Assert.Contains(auditRepo.Entries, e => e.Action == "SyncDeadLettered");
    }

    [Fact]
    public async Task ExecuteAsync_MarksFailed_ForAmbiguousMatch_NotDeadLettered()
    {
        var jobRepo = new FakeSyncJobRepository();
        var auditRepo = new FakeAuditLogRepository();
        var executor = CreateExecutor(jobRepo, auditRepo, new FakeUnitOfWork());

        await Assert.ThrowsAsync<SyncAmbiguousMatchException>(() => executor.ExecuteAsync(
            EntityType.Company, SyncDirection.InternalToHubSpot, Guid.NewGuid(), null, "corr-4",
            Task<SyncActionResult> (_) => throw new SyncAmbiguousMatchException(EntityType.Company, "ambiguous")));

        var job = Assert.Single(jobRepo.Jobs);
        Assert.Equal(SyncStatus.Failed, job.Status);
        Assert.Equal("AmbiguousMatch", job.FailureCategory);
    }
}
