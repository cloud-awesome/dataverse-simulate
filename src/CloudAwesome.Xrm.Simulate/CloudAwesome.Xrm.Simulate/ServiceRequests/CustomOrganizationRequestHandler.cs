using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public delegate OrganizationResponse CustomOrganizationRequestHandler<in TRequest>(
	TRequest request,
	CustomOrganizationRequestContext context)
	where TRequest : OrganizationRequest;
