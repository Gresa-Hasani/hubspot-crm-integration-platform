using CrmIntegration.Application.Reporting;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Operational Sales Health reporting — safe aggregate counts over SyncJob/IntegrationEvent/
/// AutomationExecution. This is reporting, not Phase 9 observability: no tokens, payloads, stack
/// traces, or raw exception details are ever returned. See docs/REPORTING.md.
///
/// SECURITY NOTE (temporary, development-only): unauthenticated, matching every other endpoint
/// in this project today (Phase 8 scope).
/// </summary>
[ApiController]
[Route("api/reports/operations")]
public class OperationsReportsController : ControllerBase
{
    private readonly IOperationsReportingService _reportingService;

    public OperationsReportsController(IOperationsReportingService reportingService)
    {
        _reportingService = reportingService;
    }

    [HttpGet("health")]
    public async Task<ActionResult<OperationalHealthResponse>> GetHealth(CancellationToken cancellationToken) =>
        Ok(await _reportingService.GetHealthAsync(cancellationToken));
}
