using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public sealed class EntityCreator
    (MockedEntityDataService dataService, SimulatorAuditService auditService) : IEntityCreator
{
    private const string RequestMessage = "Create";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        organizationService.Create(Arg.Any<Entity>())
            .Returns(x =>
            {
                var entity = x.Arg<Entity>();
                return this.Create(entity, options);
            });
    }

    internal Guid Create(Entity e, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage);
        
        if (!PermissionsCalculator.ValidateEntityPermission(e.LogicalName, RequestMessage, options))
        {
            // Exactly which type of error is thrown by crm?
            throw new Exception("Tester...");
        }
        
        var entityMetadata = MetadataValidator.ValidateCreate(e, options);

        // Pre-process
        e = this.PreProcess(e, entityMetadata);
        MetadataValidator.ApplyCreateDefaults(e, entityMetadata);

        this.ValidateDuplicateId(e);
        
        // Custom processing
        var processorType = new ProcessorType(e.LogicalName, ProcessorMessage.Create);
        if (options?.EntityProcessors?.TryGetValue(processorType, out var processor) == true)
        {
            e = processor.Process(e);
        }

        // Submit to data store
        dataService.Add(e);
        auditService.Add(RequestMessage, e.LogicalName, e.Id);
        
        return e.Id;
    }
    
    internal Entity PreProcess(Entity e, SimulatedEntityMetadata? entityMetadata = null)
    {
        var primaryIdAttribute = entityMetadata?.PrimaryIdAttribute ?? $"{e.LogicalName}id";
        e.SetAttributeIfEmpty(primaryIdAttribute, e.Id != Guid.Empty ? e.Id : Guid.NewGuid());
        e.Id = (Guid)e.Attributes[primaryIdAttribute];
        
        e.SetAttributeIfEmpty(EntityConstants.CreatedOn, dataService.SystemTime);
        e.SetAttributeFromSourceIfPopulated(EntityConstants.CreatedOn, 
            EntityConstants.OverridenCreatedOn);
        e.SetAttributeIfEmpty(EntityConstants.ModifiedOn, dataService.SystemTime);
        
        e.SetAttributeIfEmpty(EntityConstants.CreatedBy, dataService.AuthenticatedUser);
        e.SetAttributeIfEmpty(EntityConstants.ModifiedBy, dataService.AuthenticatedUser);
        e.SetAttributeIfEmpty(EntityConstants.OwnerId, dataService.AuthenticatedUser);

        return e;
    }

    private void ValidateDuplicateId(Entity e)
    {
        if (dataService.Get(e.LogicalName).Any(existing => existing.Id == e.Id))
        {
            throw new InvalidOperationException(
                $"A record with id '{e.Id}' already exists for entity '{e.LogicalName}'.");
        }
    }
}
