using System.Runtime.CompilerServices;
using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate;

public static class OrganisationServiceSimulator
{
    public static IOrganizationService Simulate(this IOrganizationService organizationService, 
        ISimulatorOptions? options = null, MockedEntityDataService? dataService = null)
    {
        var localDataService = dataService ?? new MockedEntityDataService();
        var auditService = new SimulatorAuditService();
        var service = Substitute.For<IOrganizationService>();
        
        localDataService.Reinitialise();
        auditService.Clear();

        new EntityCreator(localDataService, auditService).MockRequest(service, options);
        new EntityRetriever(localDataService, auditService).MockRequest(service, options);
        new EntityMultipleRetriever(localDataService).MockRequest(service, options);
        new EntityUpdater(localDataService).MockRequest(service, options);
        new EntityDeleter(localDataService).MockRequest(service, options);
        new EntityAssociator(localDataService).MockRequest(service, options);
        new EntityDisassociator(localDataService).MockRequest(service, options);

        var organizationRequestRegistry = RegisterServiceRequests();
        new OrganisationRequestExecutor(localDataService, auditService, organizationRequestRegistry).MockRequest(service, options);
        
        SimulatorOptionsProcessor.InitialiseMockedData(localDataService, options);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(localDataService, options);
        SimulatorOptionsProcessor.ConfigureOrganization(localDataService, options);
        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(localDataService, options);
        SimulatorOptionsProcessor.SetSystemTime(localDataService, options);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(localDataService, options);
        
        RegisterSimulation(service, localDataService, auditService, organizationRequestRegistry);
        
        return service;
    }

    public static OrganisationServiceSimulated Simulated(this IOrganizationService organizationService)
    {
        return 
            !Contexts.TryGetValue(organizationService, out var context) 
                ? throw new InvalidOperationException("This IOrganizationService has not been initialised with Simulate().") 
                : new OrganisationServiceSimulated(context.DataService, context.AuditService, context.RequestHandlers);
    }

    private static RequestHandlerRegistry RegisterServiceRequests()
    {
        var handlerRegistry = new RequestHandlerRegistry();

        handlerRegistry.RegisterHandler<CreateRequest>(new CreateRequestHandler());
        handlerRegistry.RegisterHandler<AssignRequest>(new AssignRequestHandler());
        handlerRegistry.RegisterHandler<RetrieveMultipleRequest>(new RetrieveMultipleHandler());
        handlerRegistry.RegisterHandler<WhoAmIRequest>(new WhoAmIRequestHandler());
        
        return handlerRegistry;
    }
    
    private sealed class SimulationContext(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlers)
    {
        public MockedEntityDataService DataService { get; } = dataService;
        public SimulatorAuditService AuditService { get; } = auditService;
        public RequestHandlerRegistry RequestHandlers { get; } = requestHandlers;
    };

    private static readonly ConditionalWeakTable<IOrganizationService, SimulationContext> Contexts = new();

    private static void RegisterSimulation(
        IOrganizationService service,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlers)
    {
        Contexts.Remove(service);
        Contexts.Add(service, new SimulationContext(dataService, auditService, requestHandlers));
    }
}
