using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public sealed class SimulatedDataverseScenarioContext(
    IOrganizationService service,
    OrganisationServiceSimulated simulation,
    DataverseScenarioState state)
{
    public IOrganizationService Service { get; } = service;

    public OrganisationServiceSimulated Simulation { get; } = simulation;

    public DataverseScenarioState State { get; } = state;
}

