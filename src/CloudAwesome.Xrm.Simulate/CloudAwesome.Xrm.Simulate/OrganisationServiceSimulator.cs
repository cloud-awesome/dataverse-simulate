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
        
        InitialiseMockedData(options);
        ConfigureUsersBusinessUnit(options);
        ConfigureOrganization(options);
        ConfigureAuthenticatedUser(options);
        SetSystemTime(options);
        ConfigureFiscalYearSettings(options);
        
        return Service;
    }

    public static OrganisationServiceSimulated Simulated(this IOrganizationService organizationService)
    {
        return new OrganisationServiceSimulated(_dataService, AuditService);
    }

    private static void SetSystemTime(ISimulatorOptions? options)
    {
        if (options?.ClockSimulator is not null)
        {
            _dataService.SystemTime = options.ClockSimulator.Now;
        }
    }

    private static void InitialiseMockedData(ISimulatorOptions? options)
    {
        if (options?.InitialiseData is not null)
        {
            _dataService.Add(options.InitialiseData);
        }
    }

    private static void PassThroughDataService(MockedEntityDataService? dataService)
    {
        if (dataService is not null)
        {
            _dataService = dataService;
        }
    }

    public static EntityReference ConfigureUsersBusinessUnit(ISimulatorOptions? options)
    {
        if (options?.BusinessUnit is not null)
        {
            _dataService.Add(options.BusinessUnit);
            _dataService.BusinessUnit = options.BusinessUnit.ToEntityReference();
            return options.BusinessUnit.ToEntityReference();
        }

        var businessUnit = new Entity("businessunit")
        {
            Id = Guid.NewGuid(),
            Attributes =
            {
                ["name"] = "Simulated Root Business Unit"
            }
        };
        
        _dataService.Add(businessUnit);
        _dataService.BusinessUnit = businessUnit.ToEntityReference();
        return businessUnit.ToEntityReference();
    }
    
    public static EntityReference ConfigureOrganization(ISimulatorOptions? options)
    {
        if (options?.Organization is not null)
        {
            _dataService.Add(options.Organization);
            _dataService.Organization = options.Organization.ToEntityReference();
            return options.Organization.ToEntityReference();
        }

        var organization = new Entity("organization")
        {
            Id = Guid.NewGuid(),
            Attributes =
            {
                ["name"] = "Simulated Organization"
            }
        };
        
        _dataService.Add(organization);
        _dataService.Organization = organization.ToEntityReference();
        return organization.ToEntityReference();
    }

    private static void ConfigureFiscalYearSettings(ISimulatorOptions? options)
    {
        if (options?.FiscalYearSettings is not null)
        {
            _dataService.FiscalYearSettings = options.FiscalYearSettings;
        }
    }

    private static EntityReference ConfigureAuthenticatedUser(ISimulatorOptions? options)
    {
        if (options?.AuthenticatedUser is not null)
        {
            _dataService.Add(options.AuthenticatedUser);
            _dataService.AuthenticatedUser = options.AuthenticatedUser.ToEntityReference();
            return options.AuthenticatedUser.ToEntityReference();    
        }
        
        var user = new Entity("systemuser")
        {
            Id = Guid.NewGuid(),
            Attributes =
            {
                ["fullname"] = "Simulated User",
                ["businessunitid"] = _dataService.BusinessUnit,
                ["OrganizationId"] = _dataService.Organization
            }
        };
        
        _dataService.Add(user);
        _dataService.AuthenticatedUser = user.ToEntityReference();
        return user.ToEntityReference();
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