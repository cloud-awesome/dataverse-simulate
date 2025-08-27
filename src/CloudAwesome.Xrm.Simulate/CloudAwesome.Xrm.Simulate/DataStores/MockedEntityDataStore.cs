using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataStores;

internal class MockedEntityDataStore
{
    public Dictionary<string, List<Entity>> Data { get; private set; }

    public EntityReference AuthenticatedUser { get; internal set; } = new EntityReference("systemuser", Guid.NewGuid());

    public EntityReference BusinessUnit { get; internal set; } = new EntityReference("businessunit", Guid.NewGuid());

    public EntityReference Organization { get; internal set; } = new EntityReference("organization", Guid.NewGuid());
    
    public DateTime SystemTime { get; internal set; } = DateTime.Now;

    public PluginExecutionContextMock ExecutionContextMock { get; internal set; }
    
    public FakeServiceFailureSettings FakeServiceFailureSettings { get; internal set; }

    public FiscalYearSettings FiscalYearSettings { get; internal set; }
    
    internal MockedEntityDataStore()
    {
        Data = new Dictionary<string, List<Entity>>();
    }

    internal void Set(Dictionary<string, List<Entity>> data)
    {
        Data = data;
    }
}
