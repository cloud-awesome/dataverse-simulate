using System;
using CloudAwesome.Xrm.Simulate.Interfaces;
using FluentAssertions;

namespace CloudAwesome.Xrm.Simulate.Gather.ParityTesting;

public sealed class DataverseParityScenario<TResult>
{
    public required string Name { get; init; }

    public Action<LiveDataverseScenarioContext> ArrangeLive { get; init; } = _ => { };

    public Func<LiveDataverseScenarioContext, ISimulatorOptions?> CreateSimulatorOptions { get; init; } = _ => null;

    public Action<SimulatedDataverseScenarioContext> ArrangeSimulated { get; init; } = _ => { };

    public required Func<Microsoft.Xrm.Sdk.IOrganizationService, TResult> Act { get; init; }

    public Action<LiveDataverseScenarioContext, TResult> AfterLiveAct { get; init; } = (_, _) => { };

    public Func<TResult, TResult> Normalize { get; init; } = static result => result;

    public Action<TResult, TResult> AssertEquivalent { get; init; } =
        static (live, simulated) => simulated.Should().BeEquivalentTo(live);
}
