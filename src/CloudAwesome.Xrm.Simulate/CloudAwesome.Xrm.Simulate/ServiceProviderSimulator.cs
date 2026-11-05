using System.Runtime.CompilerServices;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Configuration;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.PluginTelemetry;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate;

public static class ServiceProviderSimulator
{
    /// <summary>Creates a provider using the retained legacy CLR signature.</summary>
    /// <param name="serviceProvider">The extension receiver.</param>
    /// <param name="options">The existing live options, or null for defaults.</param>
    /// <returns>The simulated service provider.</returns>
    public static IServiceProvider Simulate(this IServiceProvider serviceProvider, ISimulatorOptions? options)
        => Simulate(serviceProvider, options, null);

    /// <summary>Configures existing options and initializes the provider through the existing simulator.</summary>
    /// <param name="serviceProvider">The extension receiver; retain the returned provider.</param>
    /// <param name="options">The existing options object, retained by reference, or null for defaults.</param>
    /// <param name="configure">The optional callback for shared setup and plugin context.</param>
    /// <returns>The provider to pass explicitly to the plugin under test.</returns>
    /// <remarks>Configuration uses existing fixture/reference semantics. No rollback, snapshot lifecycle, or dependency container is introduced.</remarks>
    /// <example><code>provider = provider.Simulate(configure: s => s.WithPluginContext(p => p.WithMessage("Create")));</code></example>
    public static IServiceProvider Simulate(this IServiceProvider serviceProvider,
        ISimulatorOptions? options = null, Action<ServiceProviderSimulationBuilder>? configure = null)
    {
        var localOptions = options ?? new SimulatorOptions();
        var setup = new SimulationSetup(localOptions);
        configure?.Invoke(new ServiceProviderSimulationBuilder(setup));
        var context = new SimulationContext();
        context.Options = localOptions;

        context.DataService.Reinitialise();
        context.LoggingService.Clear();
        context.TelemetryService.Clear();
        context.SimulatorAuditService.Clear();
        context.ServiceBus.Clear();

        var localServiceProvider = Substitute.For<IServiceProvider>();

        context.DataService.ExecutionContext = localOptions.PluginExecutionContextMock;
        context.DataService.FakeServiceFailureSettings = localOptions.FakeServiceFailureSettings;

        SimulatorOptionsProcessor.InitialiseMockedData(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureOrganization(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(context.DataService, localOptions);
        SimulatorOptionsProcessor.InitialiseSecurityModelEntities(context.DataService, localOptions);
        SimulatorOptionsProcessor.InitialiseMockedRelationships(context.DataService, localOptions);
        SimulatorOptionsProcessor.SetSystemTime(context.DataService, localOptions);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(context.DataService, localOptions);
        SimulatorOptionsProcessor.InitialiseQueues(context.DataService, localOptions);

        localServiceProvider.GetService(Arg.Any<Type>())
            .Returns(callInfo =>
            {
                var argType = callInfo.Arg<Type>();
                return argType switch
                {
                    _ when argType == typeof(IPluginExecutionContext) =>
                        PluginExecutionContextSimulator.Create(context.DataService, localOptions),
                    _ when argType == typeof(IOrganizationServiceFactory) =>
                        OrganisationServiceFactorySimulator.Create(
                            context.DataService,
                            context.SimulatorAuditService,
                            localOptions, context.RequestHandlers),
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

        setup.RegisterInitialRequests(new CustomOrganizationRequestRegistry(context.RequestHandlers));
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
                    context.SimulatorAuditService, context.ServiceBus, context.Options, context.RequestHandlers);
    }

    private sealed class SimulationContext
    {
        public MockedEntityDataService DataService { get; } = new();
        public MockedLoggingService LoggingService { get; } = new();
        public MockedTelemetryService TelemetryService { get; } = new();
        public MockedServiceBusService ServiceBus { get; } = new();
        public SimulatorAuditService SimulatorAuditService { get; } = new();
        public ISimulatorOptions Options { get; set; } = new SimulatorOptions();
        public RequestHandlerRegistry RequestHandlers { get; } = OrganisationServiceSimulator.CreateRequestRegistry();
    }

    private static readonly ConditionalWeakTable<IServiceProvider, SimulationContext> Contexts = new();

    private static void RegisterSimulation(IServiceProvider serviceProvider, SimulationContext context)
    {
        Contexts.Remove(serviceProvider);
        Contexts.Add(serviceProvider, context);
    }
}
