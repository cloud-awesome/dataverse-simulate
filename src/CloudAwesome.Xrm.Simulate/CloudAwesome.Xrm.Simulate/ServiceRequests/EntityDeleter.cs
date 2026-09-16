using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public sealed class EntityDeleter(MockedEntityDataService dataService) : IEntityDeleter
{
    private const string RequestMessage = "Delete";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        organizationService.When(x => 
            x.Delete(Arg.Any<string>(), Arg.Any<Guid>()))
            .Do(x =>
            {
                var entityName = x.Arg<string>();
                var id = x.Arg<Guid>();
             
                this.Delete(entityName, id, options);
            });
    }

    internal void Delete(string logicalName, Guid id, ISimulatorOptions? options)
    {
        RequestFailureHandler.Handle(options, RequestMessage, id);

        this.ValidateExists(logicalName, id);
                
        dataService.Delete(logicalName, id);
    }

    private void ValidateExists(string logicalName, Guid id)
    {
        if (dataService.Get(logicalName).Any(entity => entity.Id == id))
        {
            return;
        }

        throw DataverseServiceFaults.ObjectDoesNotExist(logicalName, id);
    }
}
