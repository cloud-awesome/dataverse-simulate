using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Configuration;

// A small bridge between initial options and the existing live facades, not a second initializer.
internal sealed class SimulationSetup(
    ISimulatorOptions options,
    MockedEntityDataService? data = null,
    CustomOrganizationRequestRegistry? requests = null)
{
    internal ISimulatorOptions Options { get; } = options;
    internal MockedEntityDataService? Data { get; } = data;
    private readonly List<Action<CustomOrganizationRequestRegistry>> _initialRequests = [];

    internal MockedEntityDataService Fixtures
    {
        get
        {
            if (Data is not null) return Data;
            // Reuse Add/Upsert against the options dictionary before the real initializer runs.
            var seeds = new MockedEntityDataService();
            seeds.Add(Options.InitialiseData ??= new Dictionary<string, List<Entity>>());
            return seeds;
        }
    }

    internal void Register<TRequest>(CustomOrganizationRequestHandler<TRequest> handler)
        where TRequest : OrganizationRequest
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (requests is not null) requests.Add(handler);
        else _initialRequests.Add(registry => registry.Add(handler));
    }

    internal void RegisterInitialRequests(CustomOrganizationRequestRegistry registry)
    {
        foreach (var registration in _initialRequests) registration(registry);
    }
}
