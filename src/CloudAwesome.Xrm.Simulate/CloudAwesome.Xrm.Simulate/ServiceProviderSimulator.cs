using System.Runtime.CompilerServices;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate;

public static class ServiceProviderSimulator
{
    public static IServiceProvider Simulate(this IServiceProvider serviceProvider,
        ISimulatorOptions? options = null)
    {
        var localOptions = options ?? new SimulatorOptions();
        var context = new SimulationContext();

        context.DataService.Reinitialise();
        context.LoggingService.Clear();
        context.TelemetryService.Clear();
        context.SimulatorAuditService.Clear();
        context.ServiceBus.Clear();

        var localServiceProvider = Substitute.For<IServiceProvider>();

        context.DataService.ExecutionContext = localOptions.PluginExecutionContextMock;
        context.DataService.FakeServiceFailureSettings = localOptions.FakeServiceFailureSettings;

        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(context.DataService, localOptions);
        SimulatorOptionsProcessor.InitialiseMockedData(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureOrganization(context.DataService, localOptions);
        SimulatorOptionsProcessor.SetSystemTime(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(context.DataService, localOptions);

        localServiceProvider.GetService(Arg.Any<Type>())
            .Returns(callInfo =>
            {
                var argType = callInfo.Arg<Type>();
                return argType switch
                {
                    _ when argType == typeof(IPluginExecutionContext) =>
                        PluginExecutionContextSimulator.Create(context.DataService, localOptions),
                    _ when argType == typeof(IOrganizationServiceFactory) =>
                        OrganisationServiceFactorySimulator.Create(context.DataService, localOptions),
                    _ when argType == typeof(ITracingService) =>
                        TracingServiceSimulator.Create(context.DataService, context.LoggingService, localOptions),
                    _ when argType == typeof(ILogger) =>
                        TelemetrySimulator.Create(context.DataService, context.TelemetryService, localOptions),
                    _ when argType == typeof(IServiceEndpointNotificationService) =>
                        ServiceEndpointNotificationSimulator.Create(context.DataService, context.ServiceBus, localOptions),
                    // QUESTION - Has ITransactionCurrencyService been removed?
                    _ => throw new ArgumentException("Type of Service requested is not supported")
                };
            });

        RegisterSimulation(localServiceProvider, context);

        if (serviceProvider is not null && !ReferenceEquals(serviceProvider, localServiceProvider))
        {
            RegisterSimulation(serviceProvider, context);
        }

        return localServiceProvider;
    }

    public static ServiceProviderSimulated Simulated(this IServiceProvider serviceProvider)
    {
        return
            serviceProvider is null || !Contexts.TryGetValue(serviceProvider, out var context)
                ? throw new InvalidOperationException("This IServiceProvider has not been initialised with Simulate().")
                : new ServiceProviderSimulated(context.DataService, context.LoggingService, context.TelemetryService,
                    context.SimulatorAuditService, context.ServiceBus);
    }

    private sealed class SimulationContext
    {
        public MockedEntityDataService DataService { get; } = new();
        public MockedLoggingService LoggingService { get; } = new();
        public MockedTelemetryService TelemetryService { get; } = new();
        public MockedServiceBusService ServiceBus { get; } = new();
        public SimulatorAuditService SimulatorAuditService { get; } = new();
    }

    private static readonly ConditionalWeakTable<IServiceProvider, SimulationContext> Contexts = new();

    private static void RegisterSimulation(IServiceProvider serviceProvider, SimulationContext context)
    {
        Contexts.Remove(serviceProvider);
        Contexts.Add(serviceProvider, context);
    }
}
