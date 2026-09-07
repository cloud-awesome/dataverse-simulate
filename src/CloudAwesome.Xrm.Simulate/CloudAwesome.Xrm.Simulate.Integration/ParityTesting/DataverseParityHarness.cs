using System;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public static class DataverseParityHarness
{
    private static readonly IOrganizationService ServiceToSimulate = null!;

    public static void Execute<TResult>(DataverseParityScenario<TResult> scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        var liveService = DataverseConnectionManager.Instance.GetConnection();
        var state = new DataverseScenarioState();

        using var cleanup = new LiveDataverseCleanupScope(liveService);
        var liveContext = new LiveDataverseScenarioContext(liveService, state, cleanup);

        scenario.ArrangeLive(liveContext);

        var simulatorOptions = scenario.CreateSimulatorOptions(liveContext);
        var simulatedService = ServiceToSimulate.Simulate(simulatorOptions);
        var simulatedContext = new SimulatedDataverseScenarioContext(
            simulatedService,
            simulatedService.Simulated(),
            state);

        scenario.ArrangeSimulated(simulatedContext);

        var liveResult = scenario.Act(liveService);
        scenario.AfterLiveAct(liveContext, liveResult);

        var simulatedResult = scenario.Act(simulatedService);

        scenario.AssertEquivalent(
            scenario.Normalize(liveResult),
            scenario.Normalize(simulatedResult));
    }
}
