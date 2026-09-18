namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SecurityEvaluationContext
{
    public SimulatedSecurityModel SecurityModel { get; }

    public SecurityEvaluationContext(SimulatedSecurityModel securityModel)
    {
        SecurityModel = securityModel ?? throw new ArgumentNullException(nameof(securityModel));
    }
}
