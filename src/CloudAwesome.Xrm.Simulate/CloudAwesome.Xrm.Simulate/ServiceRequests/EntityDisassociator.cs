using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class EntityDisassociator(MockedEntityDataService dataService): IEntityDisassociator
{
    private const string RequestMessage = "Disassociate";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        organizationService.When(x => 
            x.Disassociate(Arg.Any<string>(), Arg.Any<Guid>(), 
                Arg.Any<Relationship>(), Arg.Any<EntityReferenceCollection>()))
            .Do(callInfo =>
            {
                var entityName = callInfo.Arg<string>();
                var targetId = callInfo.Arg<Guid>();
                var relationship = callInfo.Arg<Relationship>();
                var relatedRefs = callInfo.Arg<EntityReferenceCollection>();
                
                this.Disassociate(entityName, targetId, relationship, relatedRefs, options);
            });
    }

    internal void Disassociate(string entityName, Guid targetId,
        Relationship relationship, EntityReferenceCollection relatedRefs,
        ISimulatorOptions? options = null)
    {
        RequestFailureHandler.Handle(options, RequestMessage, targetId);

        var target = dataService.Get(entityName, targetId).ToEntityReference();

        foreach (var relatedRef in relatedRefs)
        {
            dataService.Get(relatedRef);
        }

        dataService.Disassociate(target, relationship, relatedRefs);
    }
}
