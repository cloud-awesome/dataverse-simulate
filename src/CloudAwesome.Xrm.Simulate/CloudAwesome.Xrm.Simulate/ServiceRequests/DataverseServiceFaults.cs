using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal static class DataverseServiceFaults
{
	internal const int ObjectDoesNotExistErrorCode = -2147220969;

	internal static FaultException<OrganizationServiceFault> ObjectDoesNotExist(
		string logicalName,
		Guid id,
		DataverseFaultEntityNameFormat entityNameFormat = DataverseFaultEntityNameFormat.DisplayName)
	{
		var entityName = entityNameFormat == DataverseFaultEntityNameFormat.LogicalName
			? logicalName
			: GetEntityDisplayName(logicalName);

		var message = $"Entity '{entityName}' With Id = {id} Does Not Exist";
		var fault = new OrganizationServiceFault
		{
			ErrorCode = ObjectDoesNotExistErrorCode,
			Message = message
		};

		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	private static string GetEntityDisplayName(string logicalName)
	{
		return string.IsNullOrEmpty(logicalName)
			? logicalName
			: char.ToUpperInvariant(logicalName[0]) + logicalName[1..];
	}
}

internal enum DataverseFaultEntityNameFormat
{
	DisplayName,
	LogicalName
}
