using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.ServiceRequests;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public static class OrganisationServiceFactorySimulator
{
    public static IOrganizationServiceFactory? Create(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options)
        => Create(dataService, auditService, options, OrganisationServiceSimulator.CreateRequestRegistry());

    internal static IOrganizationServiceFactory? Create(MockedEntityDataService dataService,
        SimulatorAuditService auditService, ISimulatorOptions? options, RequestHandlerRegistry requestHandlers)
    {
        if (dataService.FakeServiceFailureSettings is { OrganizationServiceFactory: true })
        {
            return null;
        }
        
        var serviceFactory = Substitute.For<IOrganizationServiceFactory>();

        serviceFactory.CreateOrganizationService(Arg.Any<Guid?>())
            .Returns(x => OrganisationServiceSimulator.SimulateWithExistingState(
                options,
                dataService.ForUser(x.Arg<Guid?>() == Guid.Empty
                    ? dataService.AuthenticatedUser.Id
                    : x.Arg<Guid?>()),
                auditService, requestHandlers));

        return serviceFactory;
    }
}
