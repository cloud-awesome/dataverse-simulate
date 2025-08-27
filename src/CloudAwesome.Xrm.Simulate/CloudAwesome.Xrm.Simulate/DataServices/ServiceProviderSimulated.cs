namespace CloudAwesome.Xrm.Simulate.DataServices;

public class ServiceProviderSimulated
{
    private readonly MockedEntityDataService _dataService;
    private readonly MockedLoggingService _loggingService;
    private readonly MockedTelemetryService _telemetryService;
    private readonly MockedServiceBusService _serviceBus;
    private readonly SimulatorAuditService _simulatorAuditService;

    public ServiceProviderSimulated(MockedEntityDataService dataService, MockedLoggingService loggingService, 
        MockedTelemetryService telemetryService, SimulatorAuditService simulatorAuditService, MockedServiceBusService serviceBus)
    {
        _dataService = dataService;
        _loggingService = loggingService;
        _telemetryService = telemetryService;
        _simulatorAuditService = simulatorAuditService;
        _serviceBus = serviceBus;
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
}