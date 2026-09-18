namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed record SecurityDecision(
    bool Allowed,
    string? DenialReason = null,
    PrivilegeDepthEnum EffectiveDepth = PrivilegeDepthEnum.None)
{
    public static SecurityDecision Allow(PrivilegeDepthEnum effectiveDepth) =>
        new(true, EffectiveDepth: effectiveDepth);

    public static SecurityDecision Deny(string reason, PrivilegeDepthEnum effectiveDepth = PrivilegeDepthEnum.None) =>
        new(false, reason, effectiveDepth);
}
