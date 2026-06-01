namespace CloudAwesome.Xrm.Simulate.DataStores;

public class FakeServiceFailureSettings
{
    public bool ServiceEndpointNotificationService { get; set; } = false;

    public bool TracingService { get; set; } = false;

    public bool TelemetryService { get; set; } = false;

    public bool PluginExecutionContext { get; set; } = false;

    public bool OrganizationServiceFactory { get; set; } = false;
    
    public List<RequestFailureSetting> RequestFailureSettings { get; set; } = new();

}