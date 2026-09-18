using CrmIntegration.Application.Reporting;
using CrmIntegration.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CrmIntegration.Api.Controllers;

/// <summary>
/// Operational Sales Health reporting — safe aggregate counts over SyncJob/IntegrationEvent/
/// AutomationExecution. This is reporting, not Phase 9 observability: no tokens, payloads, stack
/// traces, or raw exception details are ever returned. See docs/REPORTING.md.
///
/// RBAC (docs/SECURITY.md): Admin/Operations only — this is operational/integration health, not
/// a Sales-facing report.
/// </summary>
[ApiController]
[Route("api/reports/operations")]
[Authorize(Policy = AuthorizationPolicies.CanViewOperationalHealth)]
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
