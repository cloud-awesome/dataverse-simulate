using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public static class ServiceEndpointNotificationSimulator
{
    public static IServiceEndpointNotificationService? Create(MockedEntityDataService dataService, 
        MockedServiceBusService serviceBus, ISimulatorOptions? options)
    {
        if (dataService.FakeServiceFailureSettings is { ServiceEndpointNotificationService: true })
        {
            return null;
        }
        
        var notificationSimulator = Substitute.For<IServiceEndpointNotificationService>();

        notificationSimulator.Execute(Arg.Any<EntityReference>(), Arg.Any<IPluginExecutionContext>())
            .Returns(x =>
            {
                if (x.Arg<EntityReference>().LogicalName != "serviceendpoint")
                {
                    // TODO - Verify what dataverse returns in this case
                    throw new InvalidPluginExecutionException("EntityReference provided to " +
                                                              "IServiceEndpointNotificationService.Execute " +
                                                              "must be of type 'serviceendpoint'");
                }
                
                serviceBus.Add(x.Arg<IPluginExecutionContext>());
                
                // TODO - Only a two-way or REST listener will return a string back to the caller
                return Guid.NewGuid().ToString();
            });

        return notificationSimulator;
    } 
}