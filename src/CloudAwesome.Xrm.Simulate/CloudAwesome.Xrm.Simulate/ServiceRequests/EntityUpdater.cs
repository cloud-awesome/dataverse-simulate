using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class EntityUpdater(MockedEntityDataService dataService) : IEntityUpdater
{
    private const string RequestMessage = "Update";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        /*
         * - Also need to set other system generated fields, such as modifiedon, modifiedby etc...
         * - Decide on what to set as modifiedby..? Initial set up of a system user in the data..?
         *      - Might be nice to support some sort of persona testing..?
         * -  Anything required with entity.RowVersion?
         * - How about entity.FormattedValues? And ExtensionData? KeyAttributes?
         */

        organizationService.When(x =>
            x.Update(Arg.Any<Entity>()))
            .Do(x =>
            {
                var entity = x.Arg<Entity>();
                
                var e = dataService.Get(entity.LogicalName)
                    .SingleOrDefault(z => z.Id == entity.Id);
                
                RequestFailureHandler.Handle(options, RequestMessage, entity.Id);
                
                var processorType = new ProcessorType(e.LogicalName, ProcessorMessage.Create);
                if (options?.EntityProcessors?.TryGetValue(processorType, out var processor) == true)
                {
                    e = processor.Process(e);
                }
                
                if (e != null)
                {
                    dataService.Delete(e);
                }
                else
                {
                    // TODO - Handle if the entity doesn't exist in memory
                    //      - Check the exact exception that would be thrown in .gather
                    throw new InvalidOperationException("Record not found in database ...");
                }
                
                entity.Attributes[EntityConstants.ModifiedOn] = dataService.SystemTime;
                
                // TODO - This won't work, just deleting and re-creating.
                //      - Need to loop through those attributes passed through,
                //      - As only 1 attribute could be updated in the message...  
                // Also need to handle createdon date etc... 
                dataService.Add(e);
            });
        
    }
}