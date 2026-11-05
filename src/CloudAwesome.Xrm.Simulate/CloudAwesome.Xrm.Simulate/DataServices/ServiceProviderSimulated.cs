using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;
using CloudAwesome.Xrm.Simulate.Configuration;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceRequests;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class ServiceProviderSimulated
{
    private readonly MockedEntityDataService _dataService;
    private readonly MockedLoggingService _loggingService;
    private readonly MockedTelemetryService _telemetryService;
    private readonly MockedServiceBusService _serviceBus;
    private readonly SimulatorAuditService _simulatorAuditService;
    private readonly ISimulatorOptions _options;
    private readonly RequestHandlerRegistry _requestHandlers;

    public ServiceProviderSimulated(
        MockedEntityDataService dataService,
        MockedLoggingService loggingService,
        MockedTelemetryService telemetryService,
        SimulatorAuditService simulatorAuditService,
        MockedServiceBusService serviceBus)
        : this(dataService, loggingService, telemetryService, simulatorAuditService, serviceBus, new SimulatorOptions())
    {
    }

    public ServiceProviderSimulated(
        MockedEntityDataService dataService,
        MockedLoggingService loggingService,
        MockedTelemetryService telemetryService,
        SimulatorAuditService simulatorAuditService,
        MockedServiceBusService serviceBus,
        ISimulatorOptions options)
        : this(dataService, loggingService, telemetryService, simulatorAuditService, serviceBus, options,
            OrganisationServiceSimulator.CreateRequestRegistry()) { }

    internal ServiceProviderSimulated(MockedEntityDataService dataService, MockedLoggingService loggingService,
        MockedTelemetryService telemetryService, SimulatorAuditService simulatorAuditService,
        MockedServiceBusService serviceBus, ISimulatorOptions options, RequestHandlerRegistry requestHandlers)
    {
        _dataService = dataService;
        _loggingService = loggingService;
        _telemetryService = telemetryService;
        _simulatorAuditService = simulatorAuditService;
        _serviceBus = serviceBus;
        _options = options;
        _requestHandlers = requestHandlers;
    }

    /// <summary>Applies fluent calls directly to the existing provider setup services.</summary>
    /// <param name="configure">Shared configuration plus plugin context and provider service availability.</param>
    /// <returns>This facade for configuration or inspection.</returns>
    /// <remarks>Changes are sequential and retain ordinary reference semantics. No rollback or execution-frame copying occurs.</remarks>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    public ServiceProviderSimulated Configure(Action<ServiceProviderSimulationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new ServiceProviderSimulationBuilder(new SimulationSetup(_options, _dataService, CustomOrgRequests())));
        return this;
    }

    /// <summary>Provides the existing metadata facade over the provider's shared options.</summary>
    /// <returns>The metadata loader and inspection facade.</returns>
    public SimulatedMetadataService Metadata() => new(_options);

    /// <summary>Provides the existing security facade for the provider's shared state.</summary>
    /// <returns>The security facade; accessing it creates a graph if absent.</returns>
    public SimulatedSecurityModelService SecurityModel() => new(_dataService, _options);

    /// <summary>Registers custom handlers shared by existing and future factory services.</summary>
    /// <returns>The existing typed request-registration facade.</returns>
    public CustomOrganizationRequestRegistry CustomOrgRequests() => new(_requestHandlers);

    public MockedEntityDataService Data()
    {
        return _dataService;
    }

    public MockedLoggingService Logs()
    {
        return _loggingService;
    }

    public MockedTelemetryService Telemetry()
    {
        return _telemetryService;
    }

    public SimulatorAuditService Audits()
    {
        return _simulatorAuditService;
    }

    public MockedServiceBusService ServiceBus()
    {
        return _serviceBus;
    }

    public SimulatedQueueService Queues()
    {
        return new SimulatedQueueService(_dataService, _simulatorAuditService, _options);
    }
}
