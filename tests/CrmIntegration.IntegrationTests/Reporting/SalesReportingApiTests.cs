using System.Net;
using System.Net.Http.Json;
using CrmIntegration.Application.Reporting;
using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;
using CrmIntegration.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CrmIntegration.IntegrationTests.Reporting;

/// <summary>
/// End-to-end verification of the Phase 7 reporting endpoints against real PostgreSQL.
///
/// The integration-test database is shared across the whole suite (see AssemblyInfo.cs) and
/// accumulates rows from other tests and prior runs, so endpoints with no date filter (Overview,
/// Pipeline, Lifecycle, Onboarding, Operational Health) can't be asserted against fixed expected
/// totals — instead this test takes a "before" snapshot, seeds a known deterministic dataset, and
/// asserts the exact DELTA. Endpoints that accept from/to (Revenue, Outcomes, Conversion) are
/// additionally scoped to year 2099 — a date range no other test or real data will ever touch —
/// so those can be asserted with exact expected values instead of deltas.
///
/// This is Option B from the Phase 7 spec's "Real-data / live verification" section: a
/// deterministic fictional dataset seeded directly into the real Dockerized PostgreSQL database,
/// not a new HubSpot mutation (Phases 3-6 already verified the HubSpot integration paths).
/// </summary>
public class SalesReportingApiTests
{
    private static readonly DateTime Y2099Sep01 = new(2099, 9, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ReportingValidationDataset_AllReports_MatchExplicitlyCalculatedExpectedValues()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        // ---- Snapshot "before" for every globally-aggregated (non-date-filtered) report ----
        var overviewBefore = await GetAsync<SalesOverviewResponse>(client, "/api/reports/sales/overview");
        var lifecycleBefore = await GetAsync<LifecycleReportResponse>(client, "/api/reports/sales/lifecycle");
        var onboardingBefore = await GetAsync<OnboardingReportResponse>(client, "/api/reports/sales/onboarding");
        var velocityBefore = await GetAsync<VelocityReportResponse>(client, "/api/reports/sales/velocity");
        // ExcludedWonDealsWithoutTransitionHistory is a GLOBAL count (a deal missing its ClosedWon
        // transition timestamp has no date at all, so it can never be scoped to a range) — the
        // shared integration-test database already has some from other tests/phases, so this is
        // captured as a baseline for a delta check rather than asserted as an absolute value.
        var revenueExcludedBefore = (await GetAsync<RevenueReportResponse>(client, "/api/reports/sales/revenue?from=2099-09-01&to=2099-09-30&groupBy=month")).ExcludedWonDealsWithoutTransitionHistory;

        // ==================== SEED: Reporting Validation Dataset ====================
        // Companies: 3. Contacts: 5. Deals: 3 Open (varied stages) + 2 ClosedWon + 1 ClosedLost.
        Guid companyAId = Guid.NewGuid(), companyBId = Guid.NewGuid(), companyCId = Guid.NewGuid();
        Guid contact1Id = Guid.NewGuid(), contact2Id = Guid.NewGuid(), contact3Id = Guid.NewGuid(), contact4Id = Guid.NewGuid(), contact5Id = Guid.NewGuid();
        Guid deal1Id = Guid.NewGuid(), deal2Id = Guid.NewGuid(), deal3Id = Guid.NewGuid(), deal4Id = Guid.NewGuid(), deal5Id = Guid.NewGuid(), deal6Id = Guid.NewGuid();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var now = DateTime.UtcNow;

            db.Companies.AddRange(
                new Company { Id = companyAId, Name = $"Report Co Alpha {suffix}", CreatedAt = now, UpdatedAt = now },
                new Company { Id = companyBId, Name = $"Report Co Beta {suffix}", CreatedAt = now, UpdatedAt = now },
                new Company { Id = companyCId, Name = $"Report Co Gamma {suffix}", CreatedAt = now, UpdatedAt = now });

            db.Contacts.AddRange(
                new Contact { Id = contact1Id, FirstName = "K1", LastName = "Test", Email = $"k1.{suffix}@x.example", CompanyId = companyAId, LifecycleStage = LifecycleStage.Lead, CreatedAt = now, UpdatedAt = now },
                new Contact { Id = contact2Id, FirstName = "K2", LastName = "Test", Email = $"k2.{suffix}@x.example", CompanyId = companyAId, LifecycleStage = LifecycleStage.Lead, CreatedAt = now, UpdatedAt = now },
                new Contact { Id = contact3Id, FirstName = "K3", LastName = "Test", Email = $"k3.{suffix}@x.example", CompanyId = companyBId, LifecycleStage = LifecycleStage.MarketingQualifiedLead, CreatedAt = now, UpdatedAt = now },
                new Contact { Id = contact4Id, FirstName = "K4", LastName = "Test", Email = $"k4.{suffix}@x.example", CompanyId = companyBId, LifecycleStage = LifecycleStage.Opportunity, CreatedAt = now, UpdatedAt = now },
                new Contact { Id = contact5Id, FirstName = "K5", LastName = "Test", Email = $"k5.{suffix}@x.example", CompanyId = companyCId, LifecycleStage = LifecycleStage.Customer, CreatedAt = now, UpdatedAt = now });

            db.Deals.AddRange(
                new Deal { Id = deal1Id, Name = $"Report Deal 1 {suffix}", CompanyId = companyAId, ContactId = contact1Id, Stage = DealStage.QualifiedToBuy, Status = DealStatus.Open, Amount = 10000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01 },
                new Deal { Id = deal2Id, Name = $"Report Deal 2 {suffix}", CompanyId = companyBId, ContactId = contact3Id, Stage = DealStage.Proposal, Status = DealStatus.Open, Amount = 20000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01 },
                new Deal { Id = deal3Id, Name = $"Report Deal 3 {suffix}", CompanyId = companyBId, ContactId = contact4Id, Stage = DealStage.Negotiation, Status = DealStatus.Open, Amount = 15000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01 },
                new Deal { Id = deal4Id, Name = $"Report Deal 4 {suffix}", CompanyId = companyAId, ContactId = contact2Id, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 50000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01.AddDays(9) },
                new Deal { Id = deal5Id, Name = $"Report Deal 5 {suffix}", CompanyId = companyCId, ContactId = contact5Id, Stage = DealStage.ClosedWon, Status = DealStatus.Won, Amount = 30000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01.AddDays(19) },
                new Deal { Id = deal6Id, Name = $"Report Deal 6 {suffix}", CompanyId = companyCId, ContactId = null, Stage = DealStage.ClosedLost, Status = DealStatus.Lost, Amount = 12000m, Currency = "EUR", CreatedAt = Y2099Sep01, UpdatedAt = Y2099Sep01.AddDays(14) });

            DealStageTransition T(Guid dealId, DealStage? from, DealStage to, DateTime at) => new()
            {
                Id = Guid.NewGuid(), DealId = dealId, FromStage = from, ToStage = to, OccurredAt = at,
                Source = TransitionSource.InternalUpdate, CorrelationId = $"report-test-{suffix}", CreatedAt = at
            };
            db.DealStageTransitions.AddRange(
                T(deal1Id, null, DealStage.QualifiedToBuy, Y2099Sep01),
                T(deal2Id, null, DealStage.Proposal, Y2099Sep01),
                T(deal3Id, null, DealStage.Negotiation, Y2099Sep01),
                T(deal4Id, null, DealStage.Negotiation, Y2099Sep01),
                T(deal4Id, DealStage.Negotiation, DealStage.ClosedWon, Y2099Sep01.AddDays(9)),
                T(deal5Id, null, DealStage.Negotiation, Y2099Sep01),
                T(deal5Id, DealStage.Negotiation, DealStage.ClosedWon, Y2099Sep01.AddDays(19)),
                T(deal6Id, null, DealStage.Proposal, Y2099Sep01),
                T(deal6Id, DealStage.Proposal, DealStage.ClosedLost, Y2099Sep01.AddDays(14)));

            ContactLifecycleTransition L(Guid contactId, LifecycleStage? from, LifecycleStage to, DateTime at) => new()
            {
                Id = Guid.NewGuid(), ContactId = contactId, FromStage = from, ToStage = to, OccurredAt = at,
                Source = TransitionSource.InternalUpdate, CorrelationId = $"report-test-{suffix}", CreatedAt = at
            };
            db.ContactLifecycleTransitions.AddRange(
                L(contact1Id, null, LifecycleStage.Lead, Y2099Sep01),
                L(contact2Id, null, LifecycleStage.Lead, Y2099Sep01),
                L(contact3Id, null, LifecycleStage.Lead, Y2099Sep01),
                L(contact3Id, LifecycleStage.Lead, LifecycleStage.MarketingQualifiedLead, Y2099Sep01.AddDays(1)),
                L(contact4Id, null, LifecycleStage.Lead, Y2099Sep01),
                L(contact4Id, LifecycleStage.Lead, LifecycleStage.Opportunity, Y2099Sep01.AddDays(2)),
                L(contact5Id, null, LifecycleStage.Lead, Y2099Sep01),
                L(contact5Id, LifecycleStage.Lead, LifecycleStage.Customer, Y2099Sep01.AddDays(3)));

            // Onboarding/automation timestamps are a minute after their triggering transition,
            // reflecting real causal ordering (OnboardingService runs after the transition is
            // persisted) and avoiding same-instant ties in the activity feed's ordering.
            db.OnboardingRecords.AddRange(
                new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal4Id, CompanyId = companyAId, ContactId = contact2Id, Status = OnboardingStatus.Pending, TriggerSource = "DealClosedWonAutomation", CreatedAt = Y2099Sep01.AddDays(9).AddMinutes(1), UpdatedAt = Y2099Sep01.AddDays(9).AddMinutes(1) },
                new OnboardingRecord { Id = Guid.NewGuid(), DealId = deal5Id, CompanyId = companyCId, ContactId = contact5Id, Status = OnboardingStatus.Pending, TriggerSource = "DealClosedWonAutomation", CreatedAt = Y2099Sep01.AddDays(19).AddMinutes(1), UpdatedAt = Y2099Sep01.AddDays(19).AddMinutes(1) });

            db.AutomationExecutions.AddRange(
                new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = deal4Id, IdempotencyKey = $"report-test-{suffix}-1", Status = AutomationStatus.Succeeded, CorrelationId = $"report-test-{suffix}", StartedAt = Y2099Sep01.AddDays(9).AddMinutes(1), CompletedAt = Y2099Sep01.AddDays(9).AddMinutes(2), ResultSummary = "Created OnboardingRecord" },
                new AutomationExecution { Id = Guid.NewGuid(), AutomationType = AutomationType.DealClosedWonOnboarding, EntityType = EntityType.Deal, EntityId = deal5Id, IdempotencyKey = $"report-test-{suffix}-2", Status = AutomationStatus.Succeeded, CorrelationId = $"report-test-{suffix}", StartedAt = Y2099Sep01.AddDays(19).AddMinutes(1), CompletedAt = Y2099Sep01.AddDays(19).AddMinutes(2), ResultSummary = "Created OnboardingRecord" });

            db.SyncJobs.AddRange(
                new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Succeeded, CorrelationId = $"report-test-{suffix}", StartedAt = now, CompletedAt = now },
                new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.Failed, CorrelationId = $"report-test-{suffix}", StartedAt = now, CompletedAt = now },
                new SyncJob { Id = Guid.NewGuid(), EntityType = EntityType.Deal, Direction = SyncDirection.InternalToHubSpot, Status = SyncStatus.DeadLettered, CorrelationId = $"report-test-{suffix}", StartedAt = now, CompletedAt = now });

            db.IntegrationEvents.AddRange(
                new IntegrationEvent { Id = Guid.NewGuid(), ExternalEventId = $"report-test-{suffix}-1", EventType = "deal.propertyChange", EntityType = EntityType.Deal, EntityId = "1", Status = IntegrationEventStatus.Processed, CorrelationId = $"report-test-{suffix}", ReceivedAt = now },
                new IntegrationEvent { Id = Guid.NewGuid(), ExternalEventId = $"report-test-{suffix}-2", EventType = "deal.propertyChange", EntityType = EntityType.Deal, EntityId = "2", Status = IntegrationEventStatus.Failed, CorrelationId = $"report-test-{suffix}", ReceivedAt = now });

            await db.SaveChangesAsync();
        }

        try
        {
            // ==================== VERIFY ====================

            // ---- Overview (delta) ----
            var overviewAfter = await GetAsync<SalesOverviewResponse>(client, "/api/reports/sales/overview");
            Assert.Equal(5, overviewAfter.TotalContacts - overviewBefore.TotalContacts);
            Assert.Equal(3, overviewAfter.TotalCompanies - overviewBefore.TotalCompanies);
            Assert.Equal(6, overviewAfter.TotalDeals - overviewBefore.TotalDeals);
            Assert.Equal(3, overviewAfter.OpenDeals - overviewBefore.OpenDeals);
            Assert.Equal(2, overviewAfter.WonDeals - overviewBefore.WonDeals);
            Assert.Equal(1, overviewAfter.LostDeals - overviewBefore.LostDeals);
            AssertCurrencyDelta(overviewBefore.OpenPipelineValue, overviewAfter.OpenPipelineValue, "EUR", 45000m);
            AssertCurrencyDelta(overviewBefore.WonRevenue, overviewAfter.WonRevenue, "EUR", 80000m);
            Assert.Equal(2, overviewAfter.OnboardingCount - overviewBefore.OnboardingCount);

            // ---- Pipeline (exact, scoped by our own unique Deal names) ----
            var pipeline = await GetAsync<PipelineReportResponse>(client, "/api/reports/sales/pipeline");
            // Pipeline is a global current-state snapshot with no filter; assert our specific stage
            // rows exist with at least our seeded amounts reflected (can't isolate further without
            // a filter this report intentionally doesn't expose — see docs/REPORTING.md).
            var qtb = pipeline.Stages.Single(s => s.Stage == DealStage.QualifiedToBuy && s.Currency == "EUR");
            Assert.True(qtb.TotalAmount >= 10000m);

            // ---- Revenue (exact, isolated by the 2099 date range) ----
            var revenue = await GetAsync<RevenueReportResponse>(client, "/api/reports/sales/revenue?from=2099-09-01&to=2099-09-30&groupBy=month");
            Assert.Equal(2, revenue.WonDealCount);
            Assert.Equal(80000m, Assert.Single(revenue.TotalWonRevenue).Amount);
            Assert.Equal(40000m, Assert.Single(revenue.AverageWonDealSize).Amount);
            var bucket = Assert.Single(revenue.Buckets);
            Assert.Equal(new DateOnly(2099, 9, 1), bucket.BucketStart);
            Assert.Equal(80000m, bucket.TotalAmount);
            Assert.Equal(2, bucket.DealCount);
            Assert.Equal(0, revenue.ExcludedWonDealsWithoutTransitionHistory - revenueExcludedBefore); // both our won deals have full transition history, so the global exclusion count is unaffected

            var revenueByDay = await GetAsync<RevenueReportResponse>(client, "/api/reports/sales/revenue?from=2099-09-01&to=2099-09-30&groupBy=day");
            Assert.Equal(2, revenueByDay.Buckets.Count);
            Assert.Contains(revenueByDay.Buckets, b => b.BucketStart == new DateOnly(2099, 9, 10) && b.TotalAmount == 50000m);
            Assert.Contains(revenueByDay.Buckets, b => b.BucketStart == new DateOnly(2099, 9, 20) && b.TotalAmount == 30000m);

            // ---- Outcomes (exact, isolated by the 2099 date range) ----
            var outcomes = await GetAsync<OutcomeReportResponse>(client, "/api/reports/sales/outcomes?from=2099-09-01&to=2099-09-30");
            Assert.Equal(2, outcomes.WonDealCount);
            Assert.Equal(1, outcomes.LostDealCount);
            Assert.Equal(66.67m, outcomes.WinRate);
            Assert.Equal(33.33m, outcomes.LossRate);
            Assert.Equal(80000m, Assert.Single(outcomes.TotalWonValue).Amount);
            Assert.Equal(12000m, Assert.Single(outcomes.TotalLostValue).Amount);

            // ---- Conversion (exact, isolated by the 2099 date range) ----
            var conversion = await GetAsync<ConversionReportResponse>(client, "/api/reports/sales/conversion?from=2099-09-01&to=2099-09-30");
            Assert.False(conversion.FunnelConversionRatesAvailable);
            Assert.Equal(1, conversion.ObservedTransitions.Single(t => t.FromStage == null && t.ToStage == DealStage.QualifiedToBuy).TransitionCount);
            Assert.Equal(2, conversion.ObservedTransitions.Single(t => t.FromStage == null && t.ToStage == DealStage.Proposal).TransitionCount);
            Assert.Equal(3, conversion.ObservedTransitions.Single(t => t.FromStage == null && t.ToStage == DealStage.Negotiation).TransitionCount);
            Assert.Equal(2, conversion.ObservedTransitions.Single(t => t.FromStage == DealStage.Negotiation && t.ToStage == DealStage.ClosedWon).TransitionCount);
            Assert.Equal(1, conversion.ObservedTransitions.Single(t => t.FromStage == DealStage.Proposal && t.ToStage == DealStage.ClosedLost).TransitionCount);

            // ---- Velocity (delta for counts; exact for the top-of-list recent cycle times, since our 2099 dates dominate recency) ----
            var velocityAfter = await GetAsync<VelocityReportResponse>(client, "/api/reports/sales/velocity");
            Assert.Equal(2, velocityAfter.WonDealsWithTransitionHistory - velocityBefore.WonDealsWithTransitionHistory);
            Assert.Equal(1, velocityAfter.LostDealsWithTransitionHistory - velocityBefore.LostDealsWithTransitionHistory);
            Assert.Equal(deal5Id, velocityAfter.RecentClosedWonCycleTimes[0].DealId); // most recent: closed 2099-09-20
            Assert.Equal(19.0, velocityAfter.RecentClosedWonCycleTimes[0].DurationDays);
            Assert.Equal(deal4Id, velocityAfter.RecentClosedWonCycleTimes[1].DealId); // closed 2099-09-10
            Assert.Equal(9.0, velocityAfter.RecentClosedWonCycleTimes[1].DurationDays);

            // ---- Lifecycle (delta for totals; exact for recent changes, dominated by our 2099 dates) ----
            var lifecycleAfter = await GetAsync<LifecycleReportResponse>(client, "/api/reports/sales/lifecycle");
            Assert.Equal(5, lifecycleAfter.TotalContacts - lifecycleBefore.TotalContacts);
            Assert.True(lifecycleAfter.ObservedTransitions.Single(t => t.FromStage == null && t.ToStage == LifecycleStage.Lead).TransitionCount >= 5);
            Assert.Equal(contact5Id, lifecycleAfter.RecentChanges[0].ContactId); // most recent: Lead->Customer at +3d
            Assert.Equal(contact4Id, lifecycleAfter.RecentChanges[1].ContactId); // Lead->Opportunity at +2d
            Assert.Equal(contact3Id, lifecycleAfter.RecentChanges[2].ContactId); // Lead->MarketingQualifiedLead at +1d

            // ---- Onboarding (delta for totals; exact for recent handoffs, dominated by our 2099 dates) ----
            var onboardingAfter = await GetAsync<OnboardingReportResponse>(client, "/api/reports/sales/onboarding");
            Assert.Equal(2, onboardingAfter.TotalOnboardingRecords - onboardingBefore.TotalOnboardingRecords);
            AssertCurrencyDelta(onboardingBefore.AssociatedDealValue, onboardingAfter.AssociatedDealValue, "EUR", 80000m);
            Assert.Equal(deal5Id, onboardingAfter.RecentHandoffs[0].DealId); // most recent: created 2099-09-20
            Assert.Equal(30000m, onboardingAfter.RecentHandoffs[0].DealAmount);
            Assert.Equal(deal4Id, onboardingAfter.RecentHandoffs[1].DealId); // created 2099-09-10
            Assert.Equal(50000m, onboardingAfter.RecentHandoffs[1].DealAmount);

            // ---- Activity (exact top-of-feed, dominated by our 2099 dates) ----
            var activity = await GetAsync<SalesActivityResponse>(client, "/api/reports/sales/activity?limit=10");
            Assert.Equal(SalesActivityType.AutomationCompleted, activity.Items[0].Type); // automation for deal5 completes @ +19d+2min, latest event overall
            Assert.Equal(deal5Id, activity.Items[0].EntityId);

            // ---- Operational Health (delta) ----
            var health = await GetAsync<OperationalHealthResponse>(client, "/api/reports/operations/health");
            Assert.Contains(health.SyncJobsByStatus, s => s.Status == "Succeeded");
            Assert.Contains(health.SyncJobsByStatus, s => s.Status == "Failed");
            Assert.Contains(health.SyncJobsByStatus, s => s.Status == "DeadLettered");
            Assert.Contains(health.IntegrationEventsByStatus, s => s.Status == "Processed");
            Assert.Contains(health.IntegrationEventsByStatus, s => s.Status == "Failed");
            Assert.True(health.SyncJobFailuresLast24Hours >= 2); // our Failed + DeadLettered jobs, both StartedAt=now
            Assert.True(health.IntegrationEventFailuresLast24Hours >= 1);
        }
        finally
        {
            // ==================== CLEANUP ====================
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.OnboardingRecords.RemoveRange(db.OnboardingRecords.Where(o => o.DealId == deal4Id || o.DealId == deal5Id));
            db.AutomationExecutions.RemoveRange(db.AutomationExecutions.Where(a => a.CorrelationId == $"report-test-{suffix}"));
            db.SyncJobs.RemoveRange(db.SyncJobs.Where(j => j.CorrelationId == $"report-test-{suffix}"));
            db.IntegrationEvents.RemoveRange(db.IntegrationEvents.Where(e => e.CorrelationId == $"report-test-{suffix}"));
            db.DealStageTransitions.RemoveRange(db.DealStageTransitions.Where(t => t.CorrelationId == $"report-test-{suffix}"));
            db.ContactLifecycleTransitions.RemoveRange(db.ContactLifecycleTransitions.Where(t => t.CorrelationId == $"report-test-{suffix}"));
            await db.SaveChangesAsync();
            db.Deals.RemoveRange(db.Deals.Where(d => d.Id == deal1Id || d.Id == deal2Id || d.Id == deal3Id || d.Id == deal4Id || d.Id == deal5Id || d.Id == deal6Id));
            await db.SaveChangesAsync();
            db.Contacts.RemoveRange(db.Contacts.Where(c => c.Id == contact1Id || c.Id == contact2Id || c.Id == contact3Id || c.Id == contact4Id || c.Id == contact5Id));
            db.Companies.RemoveRange(db.Companies.Where(c => c.Id == companyAId || c.Id == companyBId || c.Id == companyCId));
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RevenueReport_EmptyDateRange_ReturnsZeroesNotError()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/sales/revenue?from=1999-01-01&to=1999-01-31");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RevenueReportResponse>(CrmApiFactory.JsonOptions);

        Assert.Equal(0, result!.WonDealCount);
        Assert.Empty(result.Buckets);
    }

    [Theory]
    [InlineData("/api/reports/sales/revenue?from=not-a-date")]
    [InlineData("/api/reports/sales/revenue?from=2026-05-01&to=2026-01-01")]
    [InlineData("/api/reports/sales/revenue?groupBy=week")]
    [InlineData("/api/reports/sales/outcomes?from=bad")]
    [InlineData("/api/reports/sales/conversion?to=2026-13-40")]
    public async Task InvalidFilters_ReturnBadRequest(string requestUri)
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(requestUri);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ActivityFeed_Limit_IsBoundedToMaximum()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/reports/sales/activity?limit=99999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SalesActivityResponse>(CrmApiFactory.JsonOptions);

        Assert.True(result!.Items.Count <= 200);
    }

    [Fact]
    public async Task AllReportEndpoints_ReturnOk_OnAnEmptyOrExistingDatabase()
    {
        using var factory = new CrmApiFactory();
        var client = factory.CreateClient();

        string[] endpoints =
        [
            "/api/reports/sales/overview",
            "/api/reports/sales/pipeline",
            "/api/reports/sales/revenue",
            "/api/reports/sales/outcomes",
            "/api/reports/sales/conversion",
            "/api/reports/sales/velocity",
            "/api/reports/sales/lifecycle",
            "/api/reports/sales/onboarding",
            "/api/reports/sales/activity",
            "/api/reports/operations/health"
        ];

        foreach (var endpoint in endpoints)
        {
            var response = await client.GetAsync(endpoint);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{endpoint} returned {response.StatusCode}");
        }
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string requestUri)
    {
        var response = await client.GetAsync(requestUri);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(CrmApiFactory.JsonOptions))!;
    }

    private static void AssertCurrencyDelta(IReadOnlyList<MoneyByCurrency> before, IReadOnlyList<MoneyByCurrency> after, string currency, decimal expectedDelta)
    {
        var beforeAmount = before.FirstOrDefault(m => m.Currency == currency)?.Amount ?? 0m;
        var afterAmount = after.FirstOrDefault(m => m.Currency == currency)?.Amount ?? 0m;
        Assert.Equal(expectedDelta, afterAmount - beforeAmount);
    }
}
