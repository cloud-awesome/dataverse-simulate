using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

public sealed class EntityDeleter(MockedEntityDataService dataService) : IEntityDeleter
{
    private const string RequestMessage = "Delete";

    public void MockRequest(
        IOrganizationService organizationService,
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

        var entity = this.GetExisting(logicalName, id);

        new SimulatedSecurityEnforcer(dataService).DemandRecordAccess(
            entity,
            SecurityPrivilege.Delete,
            options);

        dataService.Delete(logicalName, id);
    }

    private Entity GetExisting(string logicalName, Guid id)
    {
        var entity = dataService.Get(logicalName).SingleOrDefault(entity => entity.Id == id);
        if (entity is not null)
        {
            return entity;
        }

        throw DataverseServiceFaults.ObjectDoesNotExist(logicalName, id);
    }
}
