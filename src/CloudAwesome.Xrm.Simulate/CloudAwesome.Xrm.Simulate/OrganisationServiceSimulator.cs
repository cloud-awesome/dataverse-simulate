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
        var localOptions = options ?? new SimulatorOptions();
        var localDataService = dataService ?? new MockedEntityDataService();
        var auditService = new SimulatorAuditService();
        var service = Substitute.For<IOrganizationService>();
        
        localDataService.Reinitialise();
        auditService.Clear();

        new EntityCreator(localDataService, auditService).MockRequest(service, localOptions);
        new EntityRetriever(localDataService, auditService).MockRequest(service, localOptions);
        new EntityMultipleRetriever(localDataService).MockRequest(service, localOptions);
        new EntityUpdater(localDataService).MockRequest(service, localOptions);
        new EntityDeleter(localDataService).MockRequest(service, localOptions);
        new EntityAssociator(localDataService).MockRequest(service, localOptions);
        new EntityDisassociator(localDataService).MockRequest(service, localOptions);

        var organizationRequestRegistry = RegisterServiceRequests();
        new OrganisationRequestExecutor(localDataService, auditService, organizationRequestRegistry).MockRequest(service, localOptions);
        
        SimulatorOptionsProcessor.InitialiseMockedData(localDataService, localOptions);
        SimulatorOptionsProcessor.InitialiseMockedRelationships(localDataService, localOptions);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(localDataService, localOptions);
        SimulatorOptionsProcessor.ConfigureOrganization(localDataService, localOptions);
        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(localDataService, localOptions);
        SimulatorOptionsProcessor.SetSystemTime(localDataService, localOptions);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(localDataService, localOptions);
        
        RegisterSimulation(service, localDataService, auditService, organizationRequestRegistry, localOptions);
        
        return service;
    }

    public static OrganisationServiceSimulated Simulated(this IOrganizationService organizationService)
    {
        return 
            !Contexts.TryGetValue(organizationService, out var context) 
                ? throw new InvalidOperationException("This IOrganizationService has not been initialised with Simulate().") 
                : new OrganisationServiceSimulated(context.DataService, context.AuditService, context.RequestHandlers, context.Options);
    }

    private static RequestHandlerRegistry RegisterServiceRequests()
    {
        var handlerRegistry = new RequestHandlerRegistry();

        handlerRegistry.RegisterHandler<CreateRequest>(new CreateRequestHandler());
        handlerRegistry.RegisterHandler<AssociateRequest>(new AssociateRequestHandler());
        handlerRegistry.RegisterHandler<AssignRequest>(new AssignRequestHandler());
        handlerRegistry.RegisterHandler<DeleteRequest>(new DeleteRequestHandler());
        handlerRegistry.RegisterHandler<DisassociateRequest>(new DisassociateRequestHandler());
        handlerRegistry.RegisterHandler<RetrieveRequest>(new RetrieveRequestHandler());
        handlerRegistry.RegisterHandler<RetrieveMultipleRequest>(new RetrieveMultipleHandler());
        handlerRegistry.RegisterHandler<UpdateRequest>(new UpdateRequestHandler());
        handlerRegistry.RegisterHandler<WhoAmIRequest>(new WhoAmIRequestHandler());
        
        return handlerRegistry;
    }
    
    private sealed class SimulationContext(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlers,
        ISimulatorOptions options)
    {
        public MockedEntityDataService DataService { get; } = dataService;
        public SimulatorAuditService AuditService { get; } = auditService;
        public RequestHandlerRegistry RequestHandlers { get; } = requestHandlers;
        public ISimulatorOptions Options { get; } = options;
    };

    private static readonly ConditionalWeakTable<IOrganizationService, SimulationContext> Contexts = new();

    private static void RegisterSimulation(
        IOrganizationService service,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlers,
        ISimulatorOptions options)
    {
        Contexts.Remove(service);
        Contexts.Add(service, new SimulationContext(dataService, auditService, requestHandlers, options));
    }
}
