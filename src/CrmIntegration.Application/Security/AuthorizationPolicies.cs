namespace CrmIntegration.Application.Security;

/// <summary>
/// Central policy-name constants and role mapping — controllers reference these names, never raw
/// role strings. See docs/SECURITY.md "RBAC matrix" and "Authorization policies" for the full
/// rationale behind each mapping.
/// </summary>
public static class AuthorizationPolicies
{
    public const string CanReadCrm = "CanReadCrm";
    public const string CanWriteCrm = "CanWriteCrm";
    public const string CanManageIntegrations = "CanManageIntegrations";
    public const string CanManageAutomations = "CanManageAutomations";
    public const string CanViewSalesReports = "CanViewSalesReports";
    public const string CanViewOperationalHealth = "CanViewOperationalHealth";
    public const string CanManageUsers = "CanManageUsers";
}
