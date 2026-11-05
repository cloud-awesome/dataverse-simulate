using CloudAwesome.Xrm.Simulate.Configuration;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

/// <summary>Configures existing request handlers, entity processors, and request failures on either mock.</summary>
/// <typeparam name="TSelf">The concrete services builder preserved through fluent calls.</typeparam>
public abstract class SimulationServicesBuilder<TSelf> where TSelf : SimulationServicesBuilder<TSelf>
{
	internal readonly SimulationSetup Setup;
	internal SimulationServicesBuilder(SimulationSetup setup) => Setup = setup;
	private TSelf Self => (TSelf)this;

	/// <summary>Registers a custom request handler through CustomOrgRequests.</summary>
	/// <param name="handler">The existing typed handler delegate.</param>
	/// <typeparam name="TRequest">The concrete organization request type.</typeparam>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Initial registration is deferred until initialization; live registration is immediate.</remarks>
	public TSelf WithRequestHandler<TRequest>(CustomOrganizationRequestHandler<TRequest> handler) where TRequest : OrganizationRequest
	{
        Setup.Register(handler);
        return Self;
	}

	/// <summary>Sets a processor in the existing options dictionary.</summary>
	/// <param name="type">The entity logical name and CRUD message.</param>
	/// <param name="processor">The existing processor retained by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Uses ordinary dictionary replacement for the same key; no separate processor registry is introduced.</remarks>
	public TSelf WithProcessor(ProcessorType type, IEntityProcessor processor)
	{
        ArgumentNullException.ThrowIfNull(type); ArgumentNullException.ThrowIfNull(processor);
        (Setup.Options.EntityProcessors ??= [])[type] = processor;
        return Self;
	}

	/// <summary>Adds a rule to the existing failure settings.</summary>
	/// <param name="failure">The existing request-name and optional record-id failure rule.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Request failure matching and exceptions remain owned by the existing request handlers.</remarks>
	public TSelf WithFailure(RequestFailureSetting failure)
	{
        ArgumentNullException.ThrowIfNull(failure);
        (Setup.Options.FakeServiceFailureSettings ??= Setup.Data?.FakeServiceFailureSettings ?? new()).RequestFailureSettings.Add(failure);
        return Self;
	}

}

/// <summary>Provides shared request configuration for an organization service.</summary>
public sealed class SimulationServicesBuilder : SimulationServicesBuilder<SimulationServicesBuilder>
{
	internal SimulationServicesBuilder(SimulationSetup setup) : base(setup) { }
}

/// <summary>Adds provider SDK service availability to the shared request configuration.</summary>
public sealed class ProviderSimulationServicesBuilder : SimulationServicesBuilder<ProviderSimulationServicesBuilder>
{
	internal ProviderSimulationServicesBuilder(SimulationSetup setup) : base(setup) { }

	/// <summary>Sets an existing provider missing-service flag.</summary>
	/// <param name="serviceType">One of the five simulator-managed SDK service interfaces.</param>
	/// <param name="missing">True returns null from subsequent resolution; false restores normal resolution.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Existing references are not revoked; this only affects subsequent service resolution.</remarks>
	public ProviderSimulationServicesBuilder WithMissingService(Type serviceType, bool missing = true)
	{
        ArgumentNullException.ThrowIfNull(serviceType);
        var flags = Setup.Options.FakeServiceFailureSettings ??= Setup.Data?.FakeServiceFailureSettings ?? new();
        if (serviceType == typeof(IPluginExecutionContext)) flags.PluginExecutionContext = missing;
        else if (serviceType == typeof(IOrganizationServiceFactory)) flags.OrganizationServiceFactory = missing;
        else if (serviceType == typeof(ITracingService)) flags.TracingService = missing;
        else if (serviceType == typeof(Microsoft.Xrm.Sdk.PluginTelemetry.ILogger)) flags.TelemetryService = missing;
        else if (serviceType == typeof(IServiceEndpointNotificationService)) flags.ServiceEndpointNotificationService = missing;
        else throw new ArgumentException("Expected a simulator-managed SDK service interface.", nameof(serviceType));
        if (Setup.Data is { } data) data.FakeServiceFailureSettings = flags;
        return this;
	}

}
