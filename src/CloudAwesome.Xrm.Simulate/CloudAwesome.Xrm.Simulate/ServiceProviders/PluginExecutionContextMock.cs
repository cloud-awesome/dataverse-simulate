using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public class PluginExecutionContextMock: IPluginExecutionContext
{
    private readonly MockedEntityDataService _dataService = new MockedEntityDataService();
    
    public int Mode { get; set; }
    public int IsolationMode { get; set; }
    public int Depth { get; set; } = 0;
    public string MessageName { get; set; } = "Not Set";
    public string PrimaryEntityName { get; set; } = "Not Set";
    public Guid? RequestId { get; set; } = Guid.NewGuid();
    public string SecondaryEntityName { get; set; } = "Not Set";
    public ParameterCollection InputParameters { get; set; } = new ParameterCollection();
    public ParameterCollection OutputParameters { get; set; } = new ParameterCollection();
    public ParameterCollection SharedVariables { get; set; } = new ParameterCollection();
    public Guid UserId => _dataService.AuthenticatedUser.Id;
    public Guid InitiatingUserId { get; set; }
    public Guid BusinessUnitId => _dataService.BusinessUnit.Id;
    public Guid OrganizationId => _dataService.Organization.Id;
    public string OrganizationName => _dataService.Organization.Name;
    public Guid PrimaryEntityId { get; set; } = Guid.NewGuid();
    public EntityImageCollection PreEntityImages { get; set; } = new EntityImageCollection();
    public EntityImageCollection PostEntityImages { get; set; } = new EntityImageCollection();
    public EntityReference OwningExtension { get; set; } = new EntityReference();
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public bool IsExecutingOffline { get; set; } = false;
    public bool IsOfflinePlayback { get; set; } = false;
    public bool IsInTransaction { get; set; } = false;
    public Guid OperationId { get; set; } = Guid.NewGuid();
    private DateTime? _operationCreatedOn;
    internal DateTime? ConfiguredOperationCreatedOn => _operationCreatedOn;

    /// <summary>Explicit operation time; unresolved fixture reads retain the existing default time.</summary>
    public DateTime OperationCreatedOn { get => _operationCreatedOn ?? _dataService.SystemTime; set => _operationCreatedOn = value; }
    public int Stage { get; set; } = 20; // Defaults to PreOperation
    public IPluginExecutionContext ParentContext { get; set; } = null!;

}