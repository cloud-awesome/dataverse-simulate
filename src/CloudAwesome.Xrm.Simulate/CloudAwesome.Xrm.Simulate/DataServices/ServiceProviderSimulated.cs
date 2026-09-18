using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Queues;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class ServiceProviderSimulated
{
    private readonly MockedEntityDataService _dataService;
    private readonly MockedLoggingService _loggingService;
    private readonly MockedTelemetryService _telemetryService;
    private readonly MockedServiceBusService _serviceBus;
    private readonly SimulatorAuditService _simulatorAuditService;
    private readonly ISimulatorOptions _options;

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
    {
        _dataService = dataService;
        _loggingService = loggingService;
        _telemetryService = telemetryService;
        _simulatorAuditService = simulatorAuditService;
        _serviceBus = serviceBus;
        _options = options;
    }

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
