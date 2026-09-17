namespace CrmIntegration.Application.Reporting;

public interface ISalesReportingService
{
    Task<SalesOverviewResponse> GetOverviewAsync(CancellationToken cancellationToken = default);

    Task<PipelineReportResponse> GetPipelineAsync(CancellationToken cancellationToken = default);

    /// <param name="from">Inclusive UTC calendar-day lower bound, "yyyy-MM-dd", or null for unbounded.</param>
    /// <param name="to">Inclusive UTC calendar-day upper bound, "yyyy-MM-dd", or null for unbounded.</param>
    /// <param name="groupBy">"day" or "month"; defaults to "month" when null.</param>
    Task<RevenueReportResponse> GetRevenueAsync(string? from, string? to, string? groupBy, CancellationToken cancellationToken = default);

    Task<OutcomeReportResponse> GetOutcomesAsync(string? from, string? to, CancellationToken cancellationToken = default);

    Task<ConversionReportResponse> GetConversionAsync(string? from, string? to, CancellationToken cancellationToken = default);

    Task<VelocityReportResponse> GetVelocityAsync(CancellationToken cancellationToken = default);

    Task<LifecycleReportResponse> GetLifecycleAsync(CancellationToken cancellationToken = default);

    Task<OnboardingReportResponse> GetOnboardingAsync(CancellationToken cancellationToken = default);

    /// <param name="limit">Defaults to 50, clamped to [1, 200].</param>
    Task<SalesActivityResponse> GetActivityAsync(int limit, CancellationToken cancellationToken = default);
}
