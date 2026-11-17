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

/// <summary>Provides fluent conveniences on existing simulation facades.</summary>
/// <remarks>Each call delegates to existing options or setup helpers. Calls apply sequentially without rollback.</remarks>
public static class SimulationConfigurationExtensions
{
	/// <summary>Configures user using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The User value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes connection/default plugin identity without granting security roles or rebinding previously created factory callers. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithUser(this OrganisationServiceSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithUser(value));
	}

	/// <summary>Configures businessunit using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The BusinessUnit value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes the default unit without automatically reconciling the security graph. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithBusinessUnit(this OrganisationServiceSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithBusinessUnit(value));
	}

	/// <summary>Configures organization using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Organization value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes the organization fixture and reference through the existing data service. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithOrganization(this OrganisationServiceSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithOrganization(value));
	}

	/// <summary>Configures clock using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Clock value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Samples the clock when this live call is applied; existing record timestamps are unchanged. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithClock(this OrganisationServiceSimulated simulated, IClockSimulator value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithClock(value));
	}

	/// <summary>Configures clock using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Clock value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Samples the clock when this live call is applied; existing record timestamps are unchanged. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithClock(this OrganisationServiceSimulated simulated, DateTime value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithClock(value));
	}

	/// <summary>Configures fiscalyearsettings using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The FiscalYearSettings value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Retains the supplied fiscal settings by reference and updates the existing data service. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithFiscalYearSettings(this OrganisationServiceSimulated simulated, FiscalYearSettings value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithFiscalYearSettings(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithData(this OrganisationServiceSimulated simulated, params Entity[] value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithData(this OrganisationServiceSimulated simulated, IEnumerable<Entity> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithData(this OrganisationServiceSimulated simulated, Dictionary<string, List<Entity>> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithData(this OrganisationServiceSimulated simulated, Action<SimulationDataBuilder> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithMetadata(this OrganisationServiceSimulated simulated, string value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithMetadata(this OrganisationServiceSimulated simulated, SimulatedMetadata value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithMetadata(this OrganisationServiceSimulated simulated, Action<SimulatedMetadataService> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures security using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Security value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing security facade/model. Define graph units/users and role assignments explicitly; live validation does not roll back earlier changes. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithSecurity(this OrganisationServiceSimulated simulated, SimulatedSecurityModel value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithSecurity(value));
	}

	/// <summary>Configures security using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Security value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing security facade/model. Define graph units/users and role assignments explicitly; live validation does not roll back earlier changes. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithSecurity(this OrganisationServiceSimulated simulated, Action<SimulatedSecurityModelService> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithSecurity(value));
	}

	/// <summary>Configures services using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Services value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses existing handler registration, processor options, and request failures. The provider also exposes the five existing missing-service flags. Earlier calls remain applied if a later call fails.</remarks>
	public static OrganisationServiceSimulated WithServices(this OrganisationServiceSimulated simulated, Action<SimulationServicesBuilder> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithServices(value));
	}

	/// <summary>Disables optional security enforcement.</summary>
	/// <param name="simulated">The initialized facade.</param>
	/// <returns>This facade for further configuration.</returns>
	public static OrganisationServiceSimulated WithoutSecurity(this OrganisationServiceSimulated simulated)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithoutSecurity());
	}

	/// <summary>Registers a custom handler for the simulation family.</summary>
	/// <param name="simulated">The initialized facade.</param>
	/// <param name="handler">The existing typed custom handler.</param>
	/// <typeparam name="TRequest">The concrete organization request type.</typeparam>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Built-in and duplicate replacement checks come from the existing registry.</remarks>
	public static OrganisationServiceSimulated WithRequestHandler<TRequest>(this OrganisationServiceSimulated simulated, CustomOrganizationRequestHandler<TRequest> handler) where TRequest : OrganizationRequest
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithRequestHandler(handler));
	}

	/// <summary>Configures user using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The User value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes connection/default plugin identity without granting security roles or rebinding previously created factory callers. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithUser(this ServiceProviderSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithUser(value));
	}

	/// <summary>Configures businessunit using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The BusinessUnit value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes the default unit without automatically reconciling the security graph. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithBusinessUnit(this ServiceProviderSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithBusinessUnit(value));
	}

	/// <summary>Configures organization using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Organization value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Changes the organization fixture and reference through the existing data service. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithOrganization(this ServiceProviderSimulated simulated, Entity value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithOrganization(value));
	}

	/// <summary>Configures clock using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Clock value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Samples the clock when this live call is applied; existing record timestamps are unchanged. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithClock(this ServiceProviderSimulated simulated, IClockSimulator value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithClock(value));
	}

	/// <summary>Configures clock using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Clock value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Samples the clock when this live call is applied; existing record timestamps are unchanged. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithClock(this ServiceProviderSimulated simulated, DateTime value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithClock(value));
	}

	/// <summary>Configures fiscalyearsettings using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The FiscalYearSettings value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Retains the supplied fiscal settings by reference and updates the existing data service. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithFiscalYearSettings(this ServiceProviderSimulated simulated, FiscalYearSettings value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithFiscalYearSettings(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithData(this ServiceProviderSimulated simulated, params Entity[] value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithData(this ServiceProviderSimulated simulated, IEnumerable<Entity> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithData(this ServiceProviderSimulated simulated, Dictionary<string, List<Entity>> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures data using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Data value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Appends fixtures by reference; no SDK Create, permission check, processor, or audit is executed. Fluent dictionary data appends rather than replacing the store. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithData(this ServiceProviderSimulated simulated, Action<SimulationDataBuilder> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithData(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithMetadata(this ServiceProviderSimulated simulated, string value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithMetadata(this ServiceProviderSimulated simulated, SimulatedMetadata value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures metadata using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Metadata value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Loads or sets a complete model by reference. No merge, metadata removal, copying, or retroactive row validation is performed. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithMetadata(this ServiceProviderSimulated simulated, Action<SimulatedMetadataService> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithMetadata(value));
	}

	/// <summary>Configures security using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Security value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing security facade/model. Define graph units/users and role assignments explicitly; live validation does not roll back earlier changes. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithSecurity(this ServiceProviderSimulated simulated, SimulatedSecurityModel value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithSecurity(value));
	}

	/// <summary>Configures security using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Security value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing security facade/model. Define graph units/users and role assignments explicitly; live validation does not roll back earlier changes. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithSecurity(this ServiceProviderSimulated simulated, Action<SimulatedSecurityModelService> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithSecurity(value));
	}

	/// <summary>Configures services using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The Services value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses existing handler registration, processor options, and request failures. The provider also exposes the five existing missing-service flags. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithServices(this ServiceProviderSimulated simulated, Action<ProviderSimulationServicesBuilder> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithServices(value));
	}

	/// <summary>Configures plugincontext using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The PluginContext value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing mock by reference. Subsequent context resolution reads that mock, and parameter/image collections retain ordinary mutable reference semantics. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithPluginContext(this ServiceProviderSimulated simulated, PluginExecutionContextMock value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithPluginContext(value));
	}

	/// <summary>Configures plugincontext using the existing simulation helpers.</summary>
	/// <param name="simulated">The initialized simulation facade.</param>
	/// <param name="value">The PluginContext value or configuration callback.</param>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Uses the existing mock by reference. Subsequent context resolution reads that mock, and parameter/image collections retain ordinary mutable reference semantics. Earlier calls remain applied if a later call fails.</remarks>
	public static ServiceProviderSimulated WithPluginContext(this ServiceProviderSimulated simulated, Action<PluginExecutionContextMock> value)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithPluginContext(value));
	}

	/// <summary>Disables optional security enforcement.</summary>
	/// <param name="simulated">The initialized facade.</param>
	/// <returns>This facade for further configuration.</returns>
	public static ServiceProviderSimulated WithoutSecurity(this ServiceProviderSimulated simulated)
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithoutSecurity());
	}

	/// <summary>Registers a custom handler for the simulation family.</summary>
	/// <param name="simulated">The initialized facade.</param>
	/// <param name="handler">The existing typed custom handler.</param>
	/// <typeparam name="TRequest">The concrete organization request type.</typeparam>
	/// <returns>This facade for further configuration.</returns>
	/// <remarks>Built-in and duplicate replacement checks come from the existing registry.</remarks>
	public static ServiceProviderSimulated WithRequestHandler<TRequest>(this ServiceProviderSimulated simulated, CustomOrganizationRequestHandler<TRequest> handler) where TRequest : OrganizationRequest
	{
        ArgumentNullException.ThrowIfNull(simulated);
        return simulated.Configure(builder => builder.WithRequestHandler(handler));
	}

}
