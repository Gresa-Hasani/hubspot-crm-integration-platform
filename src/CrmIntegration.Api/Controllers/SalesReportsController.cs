using CrmIntegration.Application.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Read-only Sales Operations reporting/analytics over existing CRM, sync, webhook, and
/// automation data. See docs/REPORTING.md for KPI formulas, date/currency policy, and known
/// limitations. Returns dedicated DTOs only — never EF entities.
///
/// SECURITY NOTE (temporary, development-only): these endpoints have no authentication or
/// authorization yet, matching every other endpoint in this project today (Phase 8 scope).
/// </summary>
[ApiController]
[Route("api/reports/sales")]
public class SalesReportsController : ControllerBase
{
    private readonly ISalesReportingService _reportingService;

    public SalesReportsController(ISalesReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("overview")]
    public async Task<ActionResult<SalesOverviewResponse>> GetOverview(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetOverviewAsync(cancellationToken));

    [HttpGet("pipeline")]
    public async Task<ActionResult<PipelineReportResponse>> GetPipeline(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetPipelineAsync(cancellationToken));

    [HttpGet("revenue")]
    public async Task<ActionResult<RevenueReportResponse>> GetRevenue(
        [FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? groupBy, CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetRevenueAsync(from, to, groupBy, cancellationToken));

    [HttpGet("outcomes")]
    public async Task<ActionResult<OutcomeReportResponse>> GetOutcomes(
        [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetOutcomesAsync(from, to, cancellationToken));

    [HttpGet("conversion")]
    public async Task<ActionResult<ConversionReportResponse>> GetConversion(
        [FromQuery] string? from, [FromQuery] string? to, CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetConversionAsync(from, to, cancellationToken));

    [HttpGet("velocity")]
    public async Task<ActionResult<VelocityReportResponse>> GetVelocity(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetVelocityAsync(cancellationToken));

    [HttpGet("lifecycle")]
    public async Task<ActionResult<LifecycleReportResponse>> GetLifecycle(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetLifecycleAsync(cancellationToken));

    [HttpGet("onboarding")]
    public async Task<ActionResult<OnboardingReportResponse>> GetOnboarding(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetOnboardingAsync(cancellationToken));

    [HttpGet("activity")]
    public async Task<ActionResult<SalesActivityResponse>> GetActivity([FromQuery] int limit, CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetActivityAsync(limit == 0 ? 50 : limit, cancellationToken));
}
