using CloudAwesome.Xrm.Simulate.Metadata;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

public sealed class SecurityEvaluationContext
{
    public SimulatedSecurityModel SecurityModel { get; }
    public SimulatedMetadata? Metadata { get; }

    public SecurityEvaluationContext(SimulatedSecurityModel securityModel)
    {
        SecurityModel = securityModel ?? throw new ArgumentNullException(nameof(securityModel));
    }

    public SecurityEvaluationContext(SimulatedSecurityModel securityModel, SimulatedMetadata? metadata)
        : this(securityModel) => Metadata = metadata;
}
