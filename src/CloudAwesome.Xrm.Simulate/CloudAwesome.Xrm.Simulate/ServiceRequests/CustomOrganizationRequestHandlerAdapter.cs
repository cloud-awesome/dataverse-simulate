using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal sealed class CustomOrganizationRequestHandlerAdapter<TRequest>(
	CustomOrganizationRequestHandler<TRequest> handler) : IRequestHandler
	where TRequest : OrganizationRequest
{
	public OrganizationResponse Handle(
		OrganizationRequest request,
		MockedEntityDataService dataService,
		SimulatorAuditService auditService,
		ISimulatorOptions? options = null)
	{
		var context = new CustomOrganizationRequestContext(dataService, auditService, options);
		return handler((TRequest)request, context);
	}
}
