using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.DataStores;
using CloudAwesome.Xrm.Simulate.Interfaces;
using Microsoft.Xrm.Sdk;
using NSubstitute;

namespace CloudAwesome.Xrm.Simulate.ServiceProviders;

public static class PluginExecutionContextSimulator
{
    public static IPluginExecutionContext? Create(MockedEntityDataService dataService, ISimulatorOptions? options)
    {
        if (dataService.FakeServiceFailureSettings is { PluginExecutionContext: true })
        {
            return null;
        }
        
        var pluginExecutionContext = Substitute.For<IPluginExecutionContext>();
        
        pluginExecutionContext.UserId.Returns(x => 
            options?.AuthenticatedUser?.Id ?? 
            dataService.AuthenticatedUser.Id);
        pluginExecutionContext.InitiatingUserId.Returns(x => 
            dataService.ExecutionContext?.InitiatingUserId is { } initiatingUserId && initiatingUserId != Guid.Empty
                ? initiatingUserId
                : dataService.AuthenticatedUser.Id);
        pluginExecutionContext.BusinessUnitId.Returns(dataService.BusinessUnit.Id);
        pluginExecutionContext.OrganizationId.Returns(dataService.Organization.Id);
        pluginExecutionContext.OperationCreatedOn.Returns(dataService.ExecutionContext?.ConfiguredOperationCreatedOn ?? dataService.SystemTime);
        pluginExecutionContext.OrganizationName.Returns(
            dataService.Get("organization").FirstOrDefault(e => e.Id == dataService.Organization.Id)?.GetAttributeValue<string>("name")
            ?? string.Empty);
        
        if (dataService.ExecutionContext is null) return pluginExecutionContext;
        
        var context = dataService.ExecutionContext;
 
        pluginExecutionContext.PrimaryEntityId.Returns(context.PrimaryEntityId);
        pluginExecutionContext.PrimaryEntityName.Returns(context.PrimaryEntityName);

        pluginExecutionContext.MessageName.Returns(context.MessageName);
        
        pluginExecutionContext.Mode.Returns(context.Mode);
        pluginExecutionContext.IsolationMode.Returns(context.IsolationMode);
        pluginExecutionContext.RequestId.Returns(context.RequestId);
        pluginExecutionContext.SecondaryEntityName.Returns(context.SecondaryEntityName);
        pluginExecutionContext.OwningExtension.Returns(context.OwningExtension);
        pluginExecutionContext.CorrelationId.Returns(context.CorrelationId);
        pluginExecutionContext.IsExecutingOffline.Returns(context.IsExecutingOffline);
        pluginExecutionContext.IsOfflinePlayback.Returns(context.IsOfflinePlayback);
        pluginExecutionContext.IsInTransaction.Returns(context.IsInTransaction);
        pluginExecutionContext.OperationId.Returns(context.OperationId);
        pluginExecutionContext.ParentContext.Returns(context.ParentContext);

        pluginExecutionContext.Depth.Returns(context.Depth);
        pluginExecutionContext.Stage.Returns(context.Stage);
        
        pluginExecutionContext.InputParameters.Returns(context.InputParameters);
        pluginExecutionContext.OutputParameters.Returns(context.OutputParameters);
        pluginExecutionContext.SharedVariables.Returns(context.SharedVariables);
        
        pluginExecutionContext.PreEntityImages.Returns(context.PreEntityImages);
        pluginExecutionContext.PostEntityImages.Returns(context.PostEntityImages);
        
        return pluginExecutionContext;
    }
}
