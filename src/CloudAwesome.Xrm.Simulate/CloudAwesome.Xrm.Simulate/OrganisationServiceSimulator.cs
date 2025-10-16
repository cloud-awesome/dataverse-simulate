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
    private static MockedEntityDataService _dataService = new();
    private static readonly SimulatorAuditService AuditService = new();
    
    private static readonly IOrganizationService Service = Substitute.For<IOrganizationService>();
    
    public static IOrganizationService Simulate(this IOrganizationService organizationService, 
        ISimulatorOptions? options = null, MockedEntityDataService? dataService = null)
    {
        PassThroughDataService(dataService);
        
        _dataService.Reinitialise();
        AuditService.Clear();

        new EntityCreator(_dataService, AuditService).MockRequest(Service, options);
        new EntityRetriever(_dataService, AuditService).MockRequest(Service, options);
        new EntityMultipleRetriever(_dataService).MockRequest(Service, options);
        new EntityUpdater(_dataService).MockRequest(Service, options);
        new EntityDeleter(_dataService).MockRequest(Service, options);
        new EntityAssociator(_dataService).MockRequest(Service, options);
        new EntityDisassociator(_dataService).MockRequest(Service, options);

        var organizationRequestRegistry = RegisterServiceRequests();
        new OrganisationRequestExecutor(_dataService, AuditService, organizationRequestRegistry).MockRequest(Service, options);
        
        SimulatorOptionsProcessor.InitialiseMockedData(_dataService, options);
        SimulatorOptionsProcessor.ConfigureUsersBusinessUnit(_dataService, options);
        SimulatorOptionsProcessor.ConfigureOrganization(_dataService, options);
        SimulatorOptionsProcessor.ConfigureAuthenticatedUser(_dataService, options);
        SimulatorOptionsProcessor.SetSystemTime(_dataService, options);
        SimulatorOptionsProcessor.ConfigureFiscalYearSettings(_dataService, options);
        
        return Service;
    }

    public static OrganisationServiceSimulated Simulated(this IOrganizationService organizationService)
    {
        return new OrganisationServiceSimulated(_dataService, AuditService);
    }

    private static void PassThroughDataService(MockedEntityDataService? dataService)
    {
        if (dataService is not null)
        {
            _dataService = dataService;
        }
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
}