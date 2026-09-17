using CrmIntegration.Domain.Entities;
using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

/// <summary>
/// Owns AutomationExecution lifecycle (Pending -> Running -> Succeeded/Skipped/Failed), the
/// database-level idempotency check, and audit logging — mirrors Phase 4's ISyncJobExecutor so
/// automation services (like Phase 4's entity sync services) only implement their actual
/// business logic once.
/// </summary>
public interface IAutomationExecutor
{
    /// <returns>The persisted execution. If an execution with the same idempotencyKey already
    /// completed (Succeeded/Skipped/Failed), the existing row is returned unchanged and
    /// <paramref name="action"/> is never invoked — see docs/SALES_AUTOMATION.md "Idempotency".</returns>
    Task<AutomationExecution> ExecuteAsync(
        AutomationType automationType,
        EntityType entityType,
        Guid entityId,
        string idempotencyKey,
        string correlationId,
        Func<CancellationToken, Task<AutomationOutcome>> action,
        CancellationToken cancellationToken = default);
}
