using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudAwesome.Xrm.Simulate.DataStores;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class MockedServiceBusService
{
    private readonly MockedServiceBusStore _serviceBus = new();
    
    /// <summary>
    /// Adds a new message to the in memory service bus
    /// </summary>
    /// <param name="message"></param>
    public void Add(string message)
    {
        _serviceBus.Messages.Add(message);
    }

    public void Add(IPluginExecutionContext pluginExecutionContext)
    {
        var contextString = SerializeContext(pluginExecutionContext);
        _serviceBus.Messages.Add(contextString);
    }
    
    /// <summary>
    /// Clears all messages from the in memory store.
    /// Call this during test set up if the test requires a fresh run. 
    /// </summary>
    public void Clear()
    {
        _serviceBus.Messages.Clear();
    }
    
    /// <summary>
    /// Returns all messages saved to the in-memory service bus.
    /// </summary>
    /// <returns></returns>
    public List<string> Get()
    {
        return _serviceBus.Messages;
    }
    
    /// <summary>
    /// Returns a list of messages matching an input string.
    /// </summary>
    /// <param name="containing">String to search for in the logs</param>
    /// <returns>List of string logs</returns>
    public List<string> Get(string containing)
    {
        var logs = _serviceBus.Messages
            .Where(x => x.Contains(containing))
            .ToList();
        
        return logs;
    }
    
    private static string SerializeContext(IPluginExecutionContext c) =>
        JsonSerializer.Serialize(new
        {
            c.Mode, c.IsolationMode, c.Depth, c.MessageName, c.PrimaryEntityName, c.RequestId,
            c.SecondaryEntityName,
            InputParameters = c.InputParameters?.ToDictionary(kv => kv.Key, kv => kv.Value),
            OutputParameters = c.OutputParameters?.ToDictionary(kv => kv.Key, kv => kv.Value),
            SharedVariables  = c.SharedVariables ?.ToDictionary(kv => kv.Key, kv => kv.Value),
            c.UserId, c.InitiatingUserId, c.BusinessUnitId, c.OrganizationId, c.OrganizationName,
            c.PrimaryEntityId, c.CorrelationId, c.IsExecutingOffline, c.IsOfflinePlayback,
            c.IsInTransaction, c.OperationId, c.OperationCreatedOn, c.Stage,
            ParentContext = (object?)null
        });
}