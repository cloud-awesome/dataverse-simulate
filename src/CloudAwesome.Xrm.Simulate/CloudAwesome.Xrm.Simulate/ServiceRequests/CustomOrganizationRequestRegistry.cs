using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public sealed class CustomOrganizationRequestRegistry
{
	private readonly RequestHandlerRegistry _requestHandlerRegistry;

	internal CustomOrganizationRequestRegistry(RequestHandlerRegistry requestHandlerRegistry)
	{
		_requestHandlerRegistry = requestHandlerRegistry;
	}

	public CustomOrganizationRequestRegistry Add<TRequest>(
		CustomOrganizationRequestHandler<TRequest> handler)
		where TRequest : OrganizationRequest
	{
		ArgumentNullException.ThrowIfNull(handler);

		_requestHandlerRegistry.RegisterCustomHandler<TRequest>(
			new CustomOrganizationRequestHandlerAdapter<TRequest>(handler));

		return this;
	}
}
