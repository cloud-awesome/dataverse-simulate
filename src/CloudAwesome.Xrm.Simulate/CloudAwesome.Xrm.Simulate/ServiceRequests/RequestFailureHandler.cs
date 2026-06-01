using CloudAwesome.Xrm.Simulate.Interfaces;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal static class RequestFailureHandler
{
	internal static void Handle(ISimulatorOptions? options, string requestMessage, Guid? recordId = null)
	{
		if (options == null)
		{
			return;
		}
		
		var id = recordId ?? Guid.Empty;
		
		var failureSettings = 
			options?.FakeServiceFailureSettings?.RequestFailureSettings 
				.SingleOrDefault(y => y.OrganizationRequestName == requestMessage);
		
		if (failureSettings == null) return;
		
		var settingsException = failureSettings.Exception ?? new Exception();
		
		if (failureSettings.FailingRecords.Count == 0 || 
		    failureSettings.FailingRecords.Contains(id))
		{
			throw settingsException;
		}
	}
}