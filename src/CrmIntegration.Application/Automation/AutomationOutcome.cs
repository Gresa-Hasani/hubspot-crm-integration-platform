using CrmIntegration.Domain.Enums;

namespace CrmIntegration.Application.Automation;

/// <summary>What an automation action actually did, for the AutomationExecution/audit record.</summary>
public record AutomationOutcome(AutomationStatus Status, string? ResultSummary);
