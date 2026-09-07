using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public sealed class LiveDataverseScenarioContext(
    IOrganizationService service,
    DataverseScenarioState state,
    LiveDataverseCleanupScope cleanup)
{
    public IOrganizationService Service { get; } = service;

    public DataverseScenarioState State { get; } = state;

    public LiveDataverseCleanupScope Cleanup { get; } = cleanup;
}

