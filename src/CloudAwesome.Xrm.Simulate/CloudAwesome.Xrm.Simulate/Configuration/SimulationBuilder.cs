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

/// <summary>Provides fluent access to existing options and simulation services.</summary>
/// <typeparam name="TSelf">The concrete builder type preserved through chaining.</typeparam>
/// <remarks>Options and fixtures retain their existing reference semantics. Live changes apply in order without rollback.</remarks>
public abstract class SimulationBuilder<TSelf> where TSelf : SimulationBuilder<TSelf>
{
	internal readonly SimulationSetup Setup;
	internal SimulationBuilder(SimulationSetup setup) => Setup = setup;
	private TSelf Self => (TSelf)this;

	/// <summary>Selects the user fixture.</summary>
	/// <param name="value">The systemuser fixture to use by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Selecting a user does not define security privileges. Existing factory service callers remain bound to their identities.</remarks>
	public TSelf WithUser(Entity value)
	{
        ArgumentNullException.ThrowIfNull(value);
        if (value.LogicalName != "systemuser") throw new ArgumentException("Expected a systemuser entity.", nameof(value));
        Setup.Options.AuthenticatedUser = value;
        if (Setup.Data is { } data)
        {
            data.Upsert(value);
            data.ForConnection().AuthenticatedUser = value.ToEntityReference();
        }
        return Self;
	}

	/// <summary>Selects the businessunit fixture.</summary>
	/// <param name="value">The businessunit fixture to use by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Selecting a user does not define security privileges. Existing factory service callers remain bound to their identities.</remarks>
	public TSelf WithBusinessUnit(Entity value)
	{
        ArgumentNullException.ThrowIfNull(value);
        if (value.LogicalName != "businessunit") throw new ArgumentException("Expected a businessunit entity.", nameof(value));
        Setup.Options.BusinessUnit = value;
        if (Setup.Data is { } data)
        {
            data.Upsert(value);
            data.ForConnection().BusinessUnit = value.ToEntityReference();
        }
        return Self;
	}

	/// <summary>Selects the organization fixture.</summary>
	/// <param name="value">The organization fixture to use by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Selecting a user does not define security privileges. Existing factory service callers remain bound to their identities.</remarks>
	public TSelf WithOrganization(Entity value)
	{
        ArgumentNullException.ThrowIfNull(value);
        if (value.LogicalName != "organization") throw new ArgumentException("Expected a organization entity.", nameof(value));
        Setup.Options.Organization = value;
        if (Setup.Data is { } data)
        {
            data.Upsert(value);
            data.ForConnection().Organization = value.ToEntityReference();
        }
        return Self;
	}

	/// <summary>Sets simulated time through the existing clock option.</summary>
	/// <param name="clock">The clock sampled during initialization or when this live call is applied.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Existing record timestamps are retained. Changing the clock object alone does not advance stored time.</remarks>
	public TSelf WithClock(IClockSimulator clock)
	{
        ArgumentNullException.ThrowIfNull(clock);
        Setup.Options.ClockSimulator = clock;
        if (Setup.Data is { } data) SimulatorOptionsProcessor.SetSystemTime(data, Setup.Options);
        return Self;
	}

	/// <summary>Sets fixed simulated time.</summary>
	/// <param name="now">The fixed simulated time; UTC is recommended.</param>
	/// <returns>This builder for further configuration.</returns>
	public TSelf WithClock(DateTime now)
	{
        return WithClock(new MockSystemTime(now));
	}

	/// <summary>Configures the fiscal calendar.</summary>
	/// <param name="settings">The existing fiscal settings object, retained by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	public TSelf WithFiscalYearSettings(FiscalYearSettings settings)
	{
        ArgumentNullException.ThrowIfNull(settings);
        Setup.Options.FiscalYearSettings = settings;
        if (Setup.Data is { } data) SimulatorOptionsProcessor.ConfigureFiscalYearSettings(data, Setup.Options);
        return Self;
	}

	/// <summary>Appends fixture rows without invoking SDK Create.</summary>
	/// <param name="entities">Fixture rows to append using the existing data service.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Rows are retained by reference, as with Data().Add. This does not generate ids, validate CRUD fields, or audit seeding.</remarks>
	public TSelf WithData(params Entity[] entities)
	{
        new SimulationDataBuilder(Setup).WithEntities(entities);
        return Self;
	}

	/// <summary>Appends fixture rows without invoking SDK Create.</summary>
	/// <param name="entities">Fixture rows to append using the existing data service.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Rows are retained by reference, as with Data().Add. This does not generate ids, validate CRUD fields, or audit seeding.</remarks>
	public TSelf WithData(IEnumerable<Entity> entities)
	{
        new SimulationDataBuilder(Setup).WithEntities(entities);
        return Self;
	}

	/// <summary>Appends fixture rows without invoking SDK Create.</summary>
	/// <param name="entities">Fixture rows to append using the existing data service.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Rows are retained by reference, as with Data().Add. This does not generate ids, validate CRUD fields, or audit seeding.</remarks>
	public TSelf WithData(Dictionary<string, List<Entity>> entities)
	{
        new SimulationDataBuilder(Setup).WithEntities(entities);
        return Self;
	}

	/// <summary>Configures fixture data using existing data and queue helpers.</summary>
	/// <param name="configure">Fixture data, relationships, and queue setup.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Live calls apply in order. Referenced records must exist when a live relationship or queue item is added.</remarks>
	public TSelf WithData(Action<SimulationDataBuilder> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        configure(new SimulationDataBuilder(Setup));
        return Self;
	}

	/// <summary>Configures metadata using the existing metadata service.</summary>
	/// <param name="configure">The existing metadata facade; Load replaces the complete model.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>No metadata merge, removal, or automatic graph copying is introduced.</remarks>
	public TSelf WithMetadata(Action<SimulatedMetadataService> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        configure(new SimulatedMetadataService(Setup.Options));
        return Self;
	}

	/// <summary>Loads and replaces metadata through the existing loader.</summary>
	/// <param name="path">The inline metadata document or split-file index.</param>
	/// <returns>This builder for further configuration.</returns>
	public TSelf WithMetadata(string path)
	{
        return WithMetadata(metadata => metadata.Load(path));
	}

	/// <summary>Sets the complete metadata model.</summary>
	/// <param name="model">The metadata model retained by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>The model is not merged, cloned, or used to retroactively validate fixture rows.</remarks>
	public TSelf WithMetadata(SimulatedMetadata model)
	{
        ArgumentNullException.ThrowIfNull(model);
        Setup.Options.Metadata = model;
        return Self;
	}

	/// <summary>Configures security through the existing security service.</summary>
	/// <param name="configure">The existing security facade for principals, roles, and explicit assignments.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Define business units and graph users explicitly. Initial setup is validated by the existing initializer; live setup is validated after the callback without rollback.</remarks>
	public TSelf WithSecurity(Action<SimulatedSecurityModelService> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        var security = new SimulatedSecurityModelService(Setup.Data, Setup.Options);
        configure(security);
        if (Setup.Data is not null) security.Validate();
        return Self;
	}

	/// <summary>Sets and seeds the existing security model.</summary>
	/// <param name="model">The typed security model retained by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>This does not remove previously seeded fixture principal rows or automatically grant roles.</remarks>
	public TSelf WithSecurity(SimulatedSecurityModel model)
	{
        ArgumentNullException.ThrowIfNull(model);
        Setup.Options.SimulatedSecurityModel = model;
        if (Setup.Data is { } data) SimulatorOptionsProcessor.InitialiseSecurityModelEntities(data, Setup.Options);
        return Self;
	}

	/// <summary>Disables optional privilege enforcement.</summary>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Fixture principal rows and metadata validation are retained.</remarks>
	public TSelf WithoutSecurity()
	{
        Setup.Options.SimulatedSecurityModel = null;
        return Self;
	}

	/// <summary>Configures existing request behavior on either mock.</summary>
	/// <param name="configure">Shared organization request handlers, processors, and request failures.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Handlers registered during initial setup are added after initialization. Live registrations are immediate; built-in replacement is rejected.</remarks>
	public TSelf WithServices(Action<SimulationServicesBuilder> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        configure(new SimulationServicesBuilder(Setup));
        return Self;
	}

	/// <summary>Registers a typed custom organization request handler.</summary>
	/// <param name="handler">The handler invoked with the executing caller context.</param>
	/// <typeparam name="TRequest">The concrete organization request type.</typeparam>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Registration uses the existing CustomOrgRequests facade. Duplicate and built-in handler checks are retained.</remarks>
	public TSelf WithRequestHandler<TRequest>(CustomOrganizationRequestHandler<TRequest> handler) where TRequest : OrganizationRequest
	{
        Setup.Register(handler);
        return Self;
	}

}

/// <summary>Configures an organization service using existing options and live helpers.</summary>
public sealed class OrganizationSimulationBuilder : SimulationBuilder<OrganizationSimulationBuilder>
{
	internal OrganizationSimulationBuilder(SimulationSetup setup) : base(setup) { }
}

/// <summary>Configures a provider, its shared requests, and the existing plugin context mock.</summary>
public sealed class ServiceProviderSimulationBuilder : SimulationBuilder<ServiceProviderSimulationBuilder>
{
	internal ServiceProviderSimulationBuilder(SimulationSetup setup) : base(setup) { }

	/// <summary>Configures shared requests and provider service availability.</summary>
	/// <param name="configure">Shared request configuration plus existing provider missing-service flags.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Application dependency factories and custom service lifetimes are outside this first pass.</remarks>
	public ServiceProviderSimulationBuilder WithServices(Action<ProviderSimulationServicesBuilder> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        configure(new ProviderSimulationServicesBuilder(Setup));
        return this;
	}

	/// <summary>Selects the existing plugin context fixture.</summary>
	/// <param name="context">The existing context mock retained by reference.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>Subsequent GetService calls use the selected mock. Parameters and images retain their normal mutable reference semantics.</remarks>
	public ServiceProviderSimulationBuilder WithPluginContext(PluginExecutionContextMock context)
	{
        ArgumentNullException.ThrowIfNull(context);
        Setup.Options.PluginExecutionContextMock = context;
        Setup.Data?.Reinitialise(context);
        return this;
	}

	/// <summary>Configures a plugin fixture using the existing mock type.</summary>
	/// <param name="configure">A callback setting properties or calling fluent extensions on a new mock.</param>
	/// <returns>This builder for further configuration.</returns>
	/// <remarks>No separate execution-frame model or deep-copy lifecycle is introduced. Plugin execution remains explicit.</remarks>
	public ServiceProviderSimulationBuilder WithPluginContext(Action<PluginExecutionContextMock> configure)
	{
        ArgumentNullException.ThrowIfNull(configure);
        var context = new PluginExecutionContextMock();
        configure(context);
        return WithPluginContext(context);
	}

}
