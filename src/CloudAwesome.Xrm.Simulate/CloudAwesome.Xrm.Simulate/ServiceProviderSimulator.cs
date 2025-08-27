using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate;

public static class ServiceProviderSimulator
{
    private static readonly MockedEntityDataService DataService = new();
    private static readonly MockedLoggingService LoggingService = new();
    private static readonly MockedTelemetryService TelemetryService = new();
    private static readonly MockedServiceBusService ServiceBus = new();
    private static readonly SimulatorAuditService SimulatorAuditService = new();
    
    public static IServiceProvider Simulate(this IServiceProvider serviceProvider,
        ISimulatorOptions? options = null)
    {
        DataService.Reinitialise();
        LoggingService.Clear();
        TelemetryService.Clear();
        SimulatorAuditService.Clear();
        ServiceBus.Clear();
        
        var localServiceProvider = Substitute.For<IServiceProvider>();

        
        // Execution mock will overwrite anything that came from other simulator options
        DataService.ExecutionContext = options?.PluginExecutionContextMock;
        DataService.FakeServiceFailureSettings = options?.FakeServiceFailureSettings;
        
        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(DataService, options);
        SimulatorOptionsProcessor.InitialiseMockedData(DataService, options);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(DataService, options);
        SimulatorOptionsProcessor.ConfigureOrganization(DataService, options);
        SimulatorOptionsProcessor.SetSystemTime(DataService, options);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(DataService, options);
        
        localServiceProvider.GetService(Arg.Any<Type>())
            .Returns(callInfo =>
            {
                var argType = callInfo.Arg<Type>();
                return argType switch
                {
                    _ when argType == typeof(IPluginExecutionContext) => 
                        PluginExecutionContextSimulator.Create(DataService, options),
                    _ when argType == typeof(IOrganizationServiceFactory) => 
                        OrganisationServiceFactorySimulator.Create(DataService, options),
                    _ when argType == typeof(ITracingService) => 
                        TracingServiceSimulator.Create(DataService, LoggingService, options),
                    _ when argType == typeof(ILogger) => 
                        TelemetrySimulator.Create(DataService, TelemetryService, options),
                    _ when argType == typeof(IServiceEndpointNotificationService) => 
                        ServiceEndpointNotificationSimulator.Create(DataService, ServiceBus, options),
                    // QUESTION - Has ITransactionCurrencyService been removed?
                    _ => throw new ArgumentException("Type of Service requested is not supported")
                };
            });
        
        return localServiceProvider;
    }

    public static ServiceProviderSimulated Simulated(this IServiceProvider serviceProvider)
    {
        return new ServiceProviderSimulated(DataService, LoggingService, TelemetryService, 
            SimulatorAuditService, ServiceBus);
    }
}