using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
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

                RequestFailureHandler.Handle(options, RequestMessage, id);
                
                if (dataService.Get(entityName).Count == 0)
                {
                    // TODO - Confirm the exception thrown by live CRM
                    throw new InvalidOperationException("No data for this entity");
                }
                
                Entity entity;
                if (columnSet.AllColumns)
                {
                    // TODO - Confirm the exception thrown by live CRM when record not found
                    entity = dataService.Get(entityName)
                                 .SingleOrDefault(e => e.Id == id) 
                             ?? throw new InvalidOperationException("No data for this entity");
                }
                else
                {
                    // TODO - Confirm the exception thrown by live CRM when record not found
                    entity = dataService.Get(entityName)
                                 .Where(e => e.Id == id)
                                 .Select(record =>
                                 {
                                     var e = new Entity(record.LogicalName) { Id = record.Id };
                                     foreach (var column in columnSet.Columns)
                                     {
                                         e[column] = record[column];
                                     }

                                     // Always return the primary GUID, even if it's not requested
                                     e[$"{record.LogicalName}id"] = record.Id; 
                    
                                     return e;
                                 })
                                 .SingleOrDefault() 
                             ?? throw new InvalidOperationException("No data for this entity");
                }
                    
                auditService.Add(RequestMessage, entity.LogicalName, entity.Id);
                
                return entity;
            });
    }
}
