using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.DataServices;

public class MockedEntityDataService
{
    private readonly MockedEntityDataStore _dataStore = new();
    
    /// <summary>
    /// Initialise the in memory store with multiple entities and records.
    /// This overwrites any previously set data
    /// </summary>
    /// <param name="data"></param>
    public void Add(Dictionary<string, List<Entity>> data)
    {
        _dataStore.Set(data);
    }
    
    /// <summary>
    /// Add a single entity record to the in memory store
    /// </summary>
    /// <param name="entity"></param>
    public void Add(Entity entity)
    {
        if (_dataStore.Data
            .TryGetValue(entity.LogicalName, out var value))
        {
            value.Add(entity);
        }
        else
        {
            _dataStore.Data.Add(entity.LogicalName, new List<Entity>{ entity });
        }
    }

    /// <summary>
    /// Get all data currently saved in the in memory store
    /// </summary>
    /// <returns></returns>
    public Dictionary<string, List<Entity>> Get()
    {
        return _dataStore.Data;
    }

    /// <summary>
    /// Get all data for a specific entity
    /// </summary>
    /// <param name="logicalName">The schema name of the entity's data to retrieve</param>
    /// <returns></returns>
    public List<Entity> Get(string logicalName)
    {
        _dataStore.Data.TryGetValue(logicalName, out var entities);
        return entities ?? new List<Entity>();
    }

    public Entity Get(string logicalName, Guid id)
    {
        var entities = this.Get(logicalName);
        if (entities.Count == 0)
        {
            throw new Exception("Entity collection not found");
        }
        
        var entity = entities.SingleOrDefault(x => x.Id == id);

        if (entity is null)
        {
            throw new Exception("Entity not found");
        }

        return entity;
    }

    public Entity Get(EntityReference entityReference)
    {
        return this.Get(entityReference.LogicalName, entityReference.Id);
    }

    public void Delete(Entity entity)
    {
        _dataStore.Data[entity.LogicalName].Remove(entity);
    }
    
    public void Delete(string logicalName, Guid id)
    {
        var entity = _dataStore.Data[logicalName].SingleOrDefault(x => x.Id == id);

        if (entity == null)
        {
            // TODO - Handle if the entity doesn't exist in memory
            //      - Check the exact exception that would be thrown in .gather
            throw new InvalidOperationException("Record not found in database ...");
        }
        
        _dataStore.Data[logicalName].Remove(entity);
    }

    public void Update(EntityReference entityReference, Entity entity)
    {
        this.Update(entity);
    }

    public void Update(Entity entity)
    {
        if (_dataStore.Data.TryGetValue(entity.LogicalName, out var entities))
        {
            var existingEntity = entities.FirstOrDefault(e => e.Id == entity.Id);
            if (existingEntity != null)
            {
                foreach (var attribute in entity.Attributes)
                {
                    existingEntity[attribute.Key] = attribute.Value;
                }

                existingEntity["modifiedon"] = _dataStore.SystemTime;
                existingEntity["modifiedby"] = _dataStore.AuthenticatedUser;
            }
            else
            {
                throw new Exception("Entity not found.");
            }
        }
        else
        {
            throw new Exception("Entity collection not found.");
        }
    }

    /// <summary>
    /// Clears all data created, updated or deleted in a test and resets the in memory database to an empty set
    /// </summary>
    public void Reinitialise()
    {
        _dataStore.Data.Clear();
        
        this.FiscalYearSettings = new FiscalYearSettings();
        this.ExecutionContext = new PluginExecutionContextMock();
        this.FakeServiceFailureSettings = new FakeServiceFailureSettings();
    }
    
    /// <summary>
    /// Clears data created, updated or deleted in a test and resets to the original configuration,
    /// including reference data set up, such as Business Unit, Fiscal Periods, etc. 
    /// </summary>
    /// <param name="options"></param>
    public void Reinitialise(ISimulatorOptions options)
    {
        /*
         * TODO - Clear and then reload stuff we want to keep
         *  - Reference to a user
         *  - BUs, Teams, Fiscal Periods, etc...
         */
        
        _dataStore.Data.Clear();
    }

    /// <summary>
    /// Resets the plugin execution context while maintaining all in memory data and configuration
    /// </summary>
    /// <param name="executionContextMock">Mocked plugin execution context, e.g. Target entity, Entity images</param>
    public void Reinitialise(PluginExecutionContextMock executionContextMock)
    {
        // TODO - What else needs doing to trigger this? It needs to get into the service provider/context mock
        _dataStore.ExecutionContextMock = executionContextMock;
    }

    /// <summary>
    /// Contains a reference to the mocked SystemUser authenticated in to Dynamics.
    /// This will be used as the reference for metadata such as CreatedBy, ModifiedBy, etc.
    /// </summary>
    public EntityReference AuthenticatedUser
    {
        get => _dataStore.AuthenticatedUser;
        set => _dataStore.AuthenticatedUser = value;
    }

    /// <summary>
    /// Stores a mocked DateTime used as the Dynamics system time.
    /// This will be used for CreatedOn, ModifiedOn, etc. 
    /// as well as the basis for datetime filters and queries (e.g. Today, LastYear)
    /// </summary>
    public DateTime SystemTime
    {
        get => _dataStore.SystemTime;
        internal set => _dataStore.SystemTime = value;
    }

    public PluginExecutionContextMock? ExecutionContext
    {
        get => _dataStore.ExecutionContextMock;
        set => _dataStore.ExecutionContextMock = value?? new PluginExecutionContextMock();
    }

    public EntityReference BusinessUnit
    {
        get => _dataStore.BusinessUnit;
        set => _dataStore.BusinessUnit = value;
    }

    public EntityReference Organization
    {
        get => _dataStore.Organization;
        set => _dataStore.Organization = value;
    }

    public FakeServiceFailureSettings? FakeServiceFailureSettings
    {
        get => _dataStore.FakeServiceFailureSettings;
        set => _dataStore.FakeServiceFailureSettings = value?? new FakeServiceFailureSettings();
    }

    public FiscalYearSettings? FiscalYearSettings
    {
        get => _dataStore.FiscalYearSettings;
        set => _dataStore.FiscalYearSettings = value ?? new FiscalYearSettings();
    }
}