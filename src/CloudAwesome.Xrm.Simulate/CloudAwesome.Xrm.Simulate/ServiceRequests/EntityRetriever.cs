using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.Metadata;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class EntityRetriever
    (MockedEntityDataService dataService, SimulatorAuditService auditService) : IEntityRetriever
{
    private const string RequestMessage = "Retrieve";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        organizationService.Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>())
            .Returns(x =>
            {
                var entityName = x.Arg<string>();
                var id = x.Arg<Guid>();
                var columnSet = x.Arg<ColumnSet>();

                return this.Retrieve(entityName, id, columnSet, options);
            });
    }

    internal Entity Retrieve(
        string entityName,
        Guid id,
        ColumnSet columnSet,
        ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage, id);
        var entityMetadata = MetadataValidator.ValidateRetrieve(entityName, columnSet, options);
                
        if (dataService.Get(entityName).Count == 0)
        {
            throw DataverseServiceFaults.ObjectDoesNotExist(
                entityName,
                id,
                DataverseFaultEntityNameFormat.LogicalName);
        }
                
        Entity entity;
        if (columnSet.AllColumns)
        {
            var fullEntity = dataService.Get(entityName)
                         .SingleOrDefault(e => e.Id == id) 
                     ?? throw DataverseServiceFaults.ObjectDoesNotExist(
                         entityName,
                         id,
                         DataverseFaultEntityNameFormat.LogicalName);
            new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
                fullEntity,
                SecurityPrivilege.Read,
                options);
            entity = fullEntity;
        }
        else
        {
            var fullEntity = dataService.Get(entityName)
                         .SingleOrDefault(e => e.Id == id)
                     ?? throw DataverseServiceFaults.ObjectDoesNotExist(
                         entityName,
                         id,
                         DataverseFaultEntityNameFormat.LogicalName);

            new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
                fullEntity,
                SecurityPrivilege.Read,
                options);

            entity = new[] { fullEntity }
                         .Select(record =>
                         {
                             var e = new Entity(record.LogicalName) { Id = record.Id };
                             foreach (var column in columnSet.Columns)
                             {
                                 if (record.Attributes.Contains(column))
                                 {
                                     e[column] = record[column];
                                 }
                             }

                             // Always return the primary GUID, even if it's not requested
                             e[entityMetadata?.PrimaryIdAttribute ?? $"{record.LogicalName}id"] = record.Id; 
                    
                             return e;
                         })
                         .Single();
        }
                    
        auditService.Add(RequestMessage, entity.LogicalName, entity.Id);
                
        return entity;
    }
}
