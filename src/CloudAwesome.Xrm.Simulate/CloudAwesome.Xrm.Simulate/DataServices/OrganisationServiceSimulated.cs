using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.Configuration;
using CloudAwesome.Xrm.Simulate.Queues;
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
    
    /// <summary>Applies fluent calls directly to the existing options and live services.</summary>
    /// <param name="configure">The callback for subsequent configuration.</param>
    /// <returns>This facade for configuration or inspection.</returns>
    /// <remarks>Calls apply in order. A later exception does not undo earlier changes; fixture references and captures are retained.</remarks>
    /// <exception cref="ArgumentNullException">The callback is null.</exception>
    public OrganisationServiceSimulated Configure(Action<OrganizationSimulationBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        configure(new OrganizationSimulationBuilder(new SimulationSetup(_options, _dataService, CustomOrgRequests())));
        return this;
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

    public SimulatedQueueService Queues()
    {
        return new SimulatedQueueService(_dataService, _auditService, _options);
    }
}
