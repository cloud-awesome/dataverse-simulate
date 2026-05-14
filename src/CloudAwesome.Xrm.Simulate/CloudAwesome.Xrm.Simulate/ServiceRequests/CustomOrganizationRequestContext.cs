using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public sealed class CustomOrganizationRequestContext
{
	internal CustomOrganizationRequestContext(
		MockedEntityDataService dataService,
		SimulatorAuditService auditService,
		ISimulatorOptions? options)
	{
		Data = dataService;
		Audit = auditService;
		Options = options;
	}

	public MockedEntityDataService Data { get; }

	public SimulatorAuditService Audit { get; }

	public ISimulatorOptions? Options { get; }
}
