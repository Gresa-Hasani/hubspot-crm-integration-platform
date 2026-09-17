using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.UnitTests.TestDoubles;
using Xunit;

namespace CrmIntegration.UnitTests.Reporting;

public class ConversionReportTests
{
    private static DealStageTransition Transition(DealStage? from, DealStage to, DateTime occurredAt) => new()
    {
        Id = Guid.NewGuid(), DealId = Guid.NewGuid(), FromStage = from, ToStage = to,
        OccurredAt = occurredAt, Source = TransitionSource.InternalUpdate, CorrelationId = "c", CreatedAt = occurredAt
    };

    [Fact]
    public async Task GetConversionAsync_NoTransitions_ReturnsEmptyList_NotAnError()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetConversionAsync(null, null);

        Assert.Empty(result.ObservedTransitions);
    }

    [Fact]
    public async Task GetConversionAsync_NeverFabricatesFunnelConversionRates()
    {
        var repo = new FakeSalesReportingRepository();
        var service = new SalesReportingService(repo);

        var result = await service.GetConversionAsync(null, null);

        Assert.False(result.FunnelConversionRatesAvailable);
    }

    [Fact]
    public async Task GetConversionAsync_CountsObservedTransitions_IncludingRepeatedAndSkippedStages()
    {
        var repo = new FakeSalesReportingRepository();
        // Repeated transition (QualifiedToBuy -> Proposal happens twice for different deals).
        repo.DealStageTransitions.Add(Transition(DealStage.QualifiedToBuy, DealStage.Proposal, DateTime.UtcNow));
        repo.DealStageTransitions.Add(Transition(DealStage.QualifiedToBuy, DealStage.Proposal, DateTime.UtcNow));
        // Skipped stage: QualifiedToBuy straight to ClosedWon, bypassing Proposal/Negotiation.
        repo.DealStageTransitions.Add(Transition(DealStage.QualifiedToBuy, DealStage.ClosedWon, DateTime.UtcNow));
        // Re-entry: ClosedWon back to Negotiation.
        repo.DealStageTransitions.Add(Transition(DealStage.ClosedWon, DealStage.Negotiation, DateTime.UtcNow));
        var service = new SalesReportingService(repo);

        var result = await service.GetConversionAsync(null, null);

        Assert.Equal(3, result.ObservedTransitions.Count);
        var repeated = result.ObservedTransitions.Single(t => t.FromStage == DealStage.QualifiedToBuy && t.ToStage == DealStage.Proposal);
        Assert.Equal(2, repeated.TransitionCount);
        Assert.Contains(result.ObservedTransitions, t => t.FromStage == DealStage.QualifiedToBuy && t.ToStage == DealStage.ClosedWon);
        Assert.Contains(result.ObservedTransitions, t => t.FromStage == DealStage.ClosedWon && t.ToStage == DealStage.Negotiation);
    }

    [Fact]
    public async Task GetConversionAsync_FiltersByDateRange()
    {
        var repo = new FakeSalesReportingRepository();
        repo.DealStageTransitions.Add(Transition(DealStage.QualifiedToBuy, DealStage.Proposal, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
        repo.DealStageTransitions.Add(Transition(DealStage.QualifiedToBuy, DealStage.Proposal, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)));
        var service = new SalesReportingService(repo);

        var result = await service.GetConversionAsync("2026-05-01", "2026-06-30");

        var metric = Assert.Single(result.ObservedTransitions);
        Assert.Equal(1, metric.TransitionCount);
    }
}
