using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Reporting;

/// <summary>
/// Internal read-model rows passed from the reporting repositories to the reporting services.
/// These are not API-facing — see SalesReportingResponses.cs/OperationsReportingResponses.cs for
/// the actual DTOs returned by endpoints. Keeping these separate lets the repositories stay
/// thin (project straight out of PostgreSQL) while the services own all formula/formatting logic.
/// </summary>
public record DealStatusCountRow(DealStatus Status, int Count);

public record CurrencyAverageRow(string Currency, decimal Average);

public record DealStageAmountRow(DealStage Stage, string Currency, int Count, decimal TotalAmount, decimal AverageAmount);

/// <summary>One closed (Won or Lost) Deal. ClosedAt is the latest DealStageTransition.OccurredAt where ToStage matches the relevant closed stage — null when no such transition exists (data predates Phase 6, or was seeded directly without going through the automation pipeline).</summary>
public record ClosedDealRow(Guid DealId, string DealName, string Currency, decimal Amount, DateTime CreatedAtUtc, DateTime? ClosedAtUtc);

public record DealTransitionCountRow(DealStage? FromStage, DealStage ToStage, int Count);

/// <summary>One raw DealStageTransition row, ordered by (DealId, OccurredAt) by the repository, for stage-duration calculation.</summary>
public record DealTransitionSequenceRow(Guid DealId, DealStage? FromStage, DealStage ToStage, DateTime OccurredAtUtc);

public record LifecycleStageCountRow(LifecycleStage Stage, int Count);

public record LifecycleTransitionCountRow(LifecycleStage? FromStage, LifecycleStage ToStage, int Count);

public record RecentLifecycleChangeRow(Guid ContactId, string FirstName, string LastName, LifecycleStage? FromStage, LifecycleStage ToStage, DateTime OccurredAtUtc);

public record OnboardingStatusCountRow(OnboardingStatus Status, int Count);

public record RecentOnboardingHandoffRow(
    Guid OnboardingRecordId, Guid DealId, string DealName, decimal DealAmount, string DealCurrency,
    string CompanyName, string? ContactFirstName, string? ContactLastName, OnboardingStatus Status, DateTime CreatedAtUtc);

public record DealStageTransitionActivityRow(Guid DealId, string DealName, DealStage? FromStage, DealStage ToStage, DateTime OccurredAtUtc);

public record ContactLifecycleTransitionActivityRow(Guid ContactId, string FirstName, string LastName, LifecycleStage? FromStage, LifecycleStage ToStage, DateTime OccurredAtUtc);

public record AutomationExecutionActivityRow(Guid Id, AutomationType AutomationType, EntityType EntityType, Guid EntityId, AutomationStatus Status, string? ResultSummary, DateTime OccurredAtUtc);

public record OnboardingActivityRow(Guid OnboardingRecordId, Guid DealId, string DealName, DateTime OccurredAtUtc);
