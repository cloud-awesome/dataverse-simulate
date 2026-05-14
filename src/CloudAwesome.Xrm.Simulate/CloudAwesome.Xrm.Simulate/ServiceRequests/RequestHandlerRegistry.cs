using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class RequestHandlerRegistry
{
	private readonly Dictionary<Type, IRequestHandler> _handlers = new();

	public void RegisterHandler<TRequest>(IRequestHandler handler) where TRequest : OrganizationRequest
	{
		_handlers[typeof(TRequest)] = handler;
	}

	public void RegisterCustomHandler<TRequest>(IRequestHandler handler) where TRequest : OrganizationRequest
	{
		var requestType = typeof(TRequest);

		if (_handlers.ContainsKey(requestType))
		{
			throw new InvalidOperationException(
				$"A handler for '{requestType.Name}' is already registered and cannot be replaced by a custom organization request handler.");
		}

		_handlers[requestType] = handler;
	}

	public IRequestHandler GetHandler(OrganizationRequest request)
	{
		return _handlers[request.GetType()];
	}
}
