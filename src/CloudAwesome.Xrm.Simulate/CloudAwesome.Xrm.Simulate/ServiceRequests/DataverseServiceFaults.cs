using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal static class DataverseServiceFaults
{
	internal const int ObjectDoesNotExistErrorCode = -2147220969;
	internal const int DuplicateKeyErrorCode = -2147220937;
	internal const int AccessDeniedErrorCode = -2147187962;
	internal const int AggregateQueryRecordLimitExceededErrorCode = -2147164125;

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

	internal static FaultException<OrganizationServiceFault> DuplicateKey()
	{
		const string message = "Cannot insert duplicate key.";
		var fault = new OrganizationServiceFault
		{
			ErrorCode = DuplicateKeyErrorCode,
			Message = message
		};

		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	internal static FaultException<OrganizationServiceFault> AccessDenied(string? message = null)
	{
		message ??= "Access is denied.";
		var fault = new OrganizationServiceFault
		{
			ErrorCode = AccessDeniedErrorCode,
			Message = message
		};

		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	internal static FaultException<OrganizationServiceFault> AggregateQueryRecordLimitExceeded()
	{
		const string message = "AggregateQueryRecordLimit exceeded. Cannot perform this operation.";
		var fault = new OrganizationServiceFault
		{
			ErrorCode = AggregateQueryRecordLimitExceededErrorCode,
			Message = message
		};

		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	internal static FaultException<OrganizationServiceFault> InvalidAggregateLimit()
	{
		const string message = "Expected value between 1 and 50000 inclusive.";
		var fault = new OrganizationServiceFault { ErrorCode = -2147220989, Message = message };
		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	internal static FaultException<OrganizationServiceFault> InvalidPageNumber(int page)
	{
		var message = $"A non valid page number was received: {page}";
		var fault = new OrganizationServiceFault { ErrorCode = -2147220989, Message = message };
		return new FaultException<OrganizationServiceFault>(fault, new FaultReason(message));
	}

	internal static FaultException<OrganizationServiceFault> TopCountWithPaging(int top)
	{
		var message = $"The Top.Count = {top} can't be specified with pagingInfo";
		var fault = new OrganizationServiceFault { ErrorCode = -2147220891, Message = message };
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
