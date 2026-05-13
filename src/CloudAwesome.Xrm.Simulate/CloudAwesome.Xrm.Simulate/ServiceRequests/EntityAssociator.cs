using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class EntityAssociator(MockedEntityDataService dataService): IEntityAssociator
{
    private const string RequestMessage = "Associate";
    
    public void MockRequest(IOrganizationService organizationService, 
        ISimulatorOptions? options = null)
    {
        organizationService.When(x => 
            x.Associate(Arg.Any<string>(), Arg.Any<Guid>(),
                Arg.Any<Relationship>(), Arg.Any<EntityReferenceCollection>()))
            .Do(callInfo =>
            {
                var entityName = callInfo.Arg<string>();
                var targetId = callInfo.Arg<Guid>();
                var relationship = callInfo.Arg<Relationship>();
                var relatedRefs = callInfo.Arg<EntityReferenceCollection>();

                // Retrieve target (will throw if entity set or record does not exist)
                var target = dataService.Get(entityName, targetId);

                // Ensure a collection exists for this relationship on the target
                if (!target.RelatedEntities.Contains(relationship))
                {
                    target.RelatedEntities.Add(relationship, new EntityCollection());
                }

                var relatedCollection = target.RelatedEntities[relationship];

                // For each related reference, resolve entity and add if not already present
                foreach (var er in relatedRefs)
                {
                    // Will throw if related entity not found
                    var relatedEntity = dataService.Get(er);

                    // Avoid duplicates by Id
                    if (!relatedCollection.Entities.Any(e => e.Id == relatedEntity.Id))
                    {
                        relatedCollection.Entities.Add(relatedEntity);
                    }
                }
            });
    }
}