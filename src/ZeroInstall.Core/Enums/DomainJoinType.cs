namespace ZeroInstall.Core.Enums;

/// <summary>
/// The type of domain or directory service a machine is joined to.
/// </summary>
public enum DomainJoinType
{
    Unknown,
    Workgroup,
    ActiveDirectory,
    AzureAd,
    HybridAzureAd
}
