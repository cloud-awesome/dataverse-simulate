using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.ServiceRequests;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class OrganisationServiceSimulated
{
    private readonly MockedEntityDataService _dataService;
    private readonly SimulatorAuditService _auditService;
    private readonly RequestHandlerRegistry _requestHandlerRegistry;
    private readonly ISimulatorOptions _options;

    public OrganisationServiceSimulated(MockedEntityDataService dataService, SimulatorAuditService auditService)
        : this(dataService, auditService, new RequestHandlerRegistry(), new SimulatorOptions())
    {
    }

    public OrganisationServiceSimulated(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlerRegistry)
        : this(dataService, auditService, requestHandlerRegistry, new SimulatorOptions())
    {
    }

    public OrganisationServiceSimulated(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlerRegistry,
        ISimulatorOptions options)
    {
        _dataService = dataService;
        _auditService = auditService;
        _requestHandlerRegistry = requestHandlerRegistry;
        _options = options;
    }
    
    public MockedEntityDataService Data()
    {
        return _dataService;
    }

    public SimulatorAuditService Audit()
    {
        return _auditService;
    }

    public CustomOrganizationRequestRegistry CustomOrgRequests()
    {
        return new CustomOrganizationRequestRegistry(_requestHandlerRegistry);
    }

    public SimulatedMetadataService Metadata()
    {
        return new SimulatedMetadataService(_options);
    }

    public SimulatedSecurityModelService SecurityModel()
    {
        return new SimulatedSecurityModelService(_dataService, _options);
    }
}
