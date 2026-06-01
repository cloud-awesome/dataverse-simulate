namespace CloudAwesome.Xrm.Simulate.DataStores;

public class RequestFailureSetting(string organizationRequestName)
{
	/// <summary>
	/// OrganizationRequest to configure failure for, e.g. SetStateRequest
	/// </summary>
	public string OrganizationRequestName { get; set; } = organizationRequestName;

	/// <summary>
	/// Optional list of record ids to fail. If empty, all records will fail
	/// </summary>
	public List<Guid> FailingRecords { get; set; } = [];
	
	/// <summary>
	/// Optional exception to throw when failing. If empty, a default exception will be thrown
	/// </summary>
	public Exception? Exception { get; set; }
}