using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public class EntityDisassociator(MockedEntityDataService dataService) : IEntityDisassociator
{
    private const string RequestMessage = "Disassociate";

    public void MockRequest(
        IOrganizationService organizationService,
        ISimulatorOptions? options = null)
    {
        organizationService.When(x =>
                x.Disassociate(
                    Arg.Any<string>(),
                    Arg.Any<Guid>(),
                    Arg.Any<Relationship>(),
                    Arg.Any<EntityReferenceCollection>()))
            .Do(callInfo =>
            {
                var entityName = callInfo.Arg<string>();
                var targetId = callInfo.Arg<Guid>();
                var relationship = callInfo.Arg<Relationship>();
                var relatedRefs = callInfo.Arg<EntityReferenceCollection>();

                this.Disassociate(entityName, targetId, relationship, relatedRefs, options);
            });
    }

    internal void Disassociate(
        string entityName,
        Guid targetId,
        Relationship relationship,
        EntityReferenceCollection relatedRefs,
        ISimulatorOptions? options = null)
    {
        RequestFailureHandler.Handle(options, RequestMessage, targetId);

        var targetEntity = dataService.Get(entityName, targetId);
        var target = targetEntity.ToEntityReference();
        var security = new SimulatedSecurityEnforcer(dataService);
        security.DemandRecordAccess(targetEntity, SecurityPrivilege.AppendTo, options);

        foreach (var relatedRef in relatedRefs)
        {
            var relatedEntity = dataService.Get(relatedRef);
            security.DemandRecordAccess(relatedEntity, SecurityPrivilege.Append, options);
        }

        dataService.Disassociate(target, relationship, relatedRefs);
    }
}
