using CloudAwesome.Xrm.Simulate.ServiceRequests;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class OrganisationServiceSimulated
{
    private readonly MockedEntityDataService _dataService;
    private readonly SimulatorAuditService _auditService;
    private readonly RequestHandlerRegistry _requestHandlerRegistry;

    public OrganisationServiceSimulated(MockedEntityDataService dataService, SimulatorAuditService auditService)
        : this(dataService, auditService, new RequestHandlerRegistry())
    {
    }

    public OrganisationServiceSimulated(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        RequestHandlerRegistry requestHandlerRegistry)
    {
        _dataService = dataService;
        _auditService = auditService;
        _requestHandlerRegistry = requestHandlerRegistry;
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
}
