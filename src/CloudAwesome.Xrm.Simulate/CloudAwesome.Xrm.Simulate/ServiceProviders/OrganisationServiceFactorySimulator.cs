using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public static class OrganisationServiceFactorySimulator
{
    public static IOrganizationServiceFactory? Create(
        MockedEntityDataService dataService,
        SimulatorAuditService auditService,
        ISimulatorOptions? options)
    {
        if (dataService.FakeServiceFailureSettings is { OrganizationServiceFactory: true })
        {
            return null;
        }
        
        var serviceFactory = Substitute.For<IOrganizationServiceFactory>();

        serviceFactory.CreateOrganizationService(Arg.Any<Guid>())
            .Returns(x => OrganisationServiceSimulator.SimulateWithExistingState(
                options,
                dataService,
                auditService));

        return serviceFactory;
    }
}
