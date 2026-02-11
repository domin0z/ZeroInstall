using System.Text.Json.Serialization;
using ZeroInstall.Core.Enums;

namespace ZeroInstall.Core.Models;

/// <summary>
/// Domain/workgroup/Azure AD status information for a machine.
/// </summary>
public class DomainInfo
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public DomainJoinType JoinType { get; set; }

    /// <summary>
    /// The domain name (for AD) or workgroup name.
    /// </summary>
    public string DomainOrWorkgroup { get; set; } = string.Empty;

    /// <summary>
    /// True if the machine is joined to an Active Directory or Hybrid Azure AD domain.
    /// </summary>
    public bool IsDomainJoined => JoinType is DomainJoinType.ActiveDirectory or DomainJoinType.HybridAzureAd;

    /// <summary>
    /// Azure AD tenant name, if Azure AD or Hybrid joined.
    /// </summary>
    public string? AzureAdTenantName { get; set; }

    /// <summary>
    /// Azure AD tenant ID, if Azure AD or Hybrid joined.
    /// </summary>
    public string? AzureAdTenantId { get; set; }

    /// <summary>
    /// Azure AD device ID, if Azure AD or Hybrid joined.
    /// </summary>
    public string? AzureAdDeviceId { get; set; }

    /// <summary>
    /// The domain controller name, if AD-joined.
    /// </summary>
    public string? DomainController { get; set; }

    /// <summary>
    /// Raw diagnostic output for troubleshooting.
    /// </summary>
    [JsonIgnore]
    public string RawOutput { get; set; } = string.Empty;
}
