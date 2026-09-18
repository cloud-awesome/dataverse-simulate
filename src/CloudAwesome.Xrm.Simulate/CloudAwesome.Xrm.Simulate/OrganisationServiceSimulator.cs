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
        return SimulateCore(options, dataService, auditService: null, initialiseState: true);
    }

    internal static IOrganizationService SimulateWithExistingState(
        ISimulatorOptions? options,
        MockedEntityDataService dataService,
        SimulatorAuditService auditService)
    {
        return SimulateCore(options, dataService, auditService, initialiseState: false);
    }

    private static IOrganizationService SimulateCore(
        ISimulatorOptions? options,
        MockedEntityDataService? dataService,
        SimulatorAuditService? auditService,
        bool initialiseState)
    {
        var localOptions = options ?? new SimulatorOptions();
        var localDataService = dataService ?? new MockedEntityDataService();
        var localAuditService = auditService ?? new SimulatorAuditService();
        var service = Substitute.For<IOrganizationService>();

        if (initialiseState)
        {
            localDataService.Reinitialise();
            localAuditService.Clear();
        }

        new EntityCreator(localDataService, localAuditService).MockRequest(service, localOptions);
        new EntityRetriever(localDataService, localAuditService).MockRequest(service, localOptions);
        new EntityMultipleRetriever(localDataService).MockRequest(service, localOptions);
        new EntityUpdater(localDataService, localAuditService).MockRequest(service, localOptions);
        new EntityDeleter(localDataService).MockRequest(service, localOptions);
        new EntityAssociator(localDataService).MockRequest(service, localOptions);
        new EntityDisassociator(localDataService).MockRequest(service, localOptions);

        var organizationRequestRegistry = RegisterServiceRequests();
        new OrganisationRequestExecutor(localDataService, localAuditService, organizationRequestRegistry).MockRequest(service, localOptions);

        if (initialiseState)
        {
            SimulatorOptionsProcessor.InitialiseMockedData(localDataService, localOptions);
            SimulatorOptionsProcessor.InitialiseMockedRelationships(localDataService, localOptions);
            SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(localDataService, localOptions);
            SimulatorOptionsProcessor.ConfigureOrganization(localDataService, localOptions);
            SimulatorOptionsProcessor.ConfigureAuthenticatedUser(localDataService, localOptions);
            SimulatorOptionsProcessor.InitialiseSecurityModelEntities(localDataService, localOptions);
            SimulatorOptionsProcessor.SetSystemTime(localDataService, localOptions);
            SimulatorOptionsProcessor.ConfigureFiscalYearSettings(localDataService, localOptions);
            SimulatorOptionsProcessor.InitialiseQueues(localDataService, localOptions);
        }
        
        RegisterSimulation(service, localDataService, localAuditService, organizationRequestRegistry, localOptions);
        
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

        handlerRegistry.RegisterHandler<AddMembersTeamRequest>(new AddMembersTeamRequestHandler());
        handlerRegistry.RegisterHandler<CreateRequest>(new CreateRequestHandler());
        handlerRegistry.RegisterHandler<AssociateRequest>(new AssociateRequestHandler());
        handlerRegistry.RegisterHandler<AssignRequest>(new AssignRequestHandler());
        handlerRegistry.RegisterHandler<DeleteRequest>(new DeleteRequestHandler());
        handlerRegistry.RegisterHandler<DisassociateRequest>(new DisassociateRequestHandler());
        handlerRegistry.RegisterHandler<GrantAccessRequest>(new GrantAccessRequestHandler());
        handlerRegistry.RegisterHandler<ModifyAccessRequest>(new ModifyAccessRequestHandler());
        handlerRegistry.RegisterHandler<RemoveMembersTeamRequest>(new RemoveMembersTeamRequestHandler());
        handlerRegistry.RegisterHandler<RetrievePrincipalAccessRequest>(new RetrievePrincipalAccessRequestHandler());
        handlerRegistry.RegisterHandler<RetrieveRequest>(new RetrieveRequestHandler());
        handlerRegistry.RegisterHandler<RetrieveMultipleRequest>(new RetrieveMultipleHandler());
        handlerRegistry.RegisterHandler<RetrieveSharedPrincipalsAndAccessRequest>(new RetrieveSharedPrincipalsAndAccessRequestHandler());
        handlerRegistry.RegisterHandler<RevokeAccessRequest>(new RevokeAccessRequestHandler());
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
