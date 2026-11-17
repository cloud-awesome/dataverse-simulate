using CloudAwesome.Xrm.Simulate.ServiceProviders;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate;

/// <summary>Fluent setters on the existing plugin context mock.</summary>
/// <remarks>Values and collections retain the mock's ordinary mutable reference semantics.</remarks>
public static class PluginExecutionContextExtensions
{
    /// <summary>Sets MessageName on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The MessageName fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithMessage(this PluginExecutionContextMock context, string value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.MessageName = value;
        return context;
    }

    /// <summary>Sets Stage on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The Stage fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithStage(this PluginExecutionContextMock context, int value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Stage = value;
        return context;
    }

    /// <summary>Sets Mode on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The Mode fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithMode(this PluginExecutionContextMock context, int value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Mode = value;
        return context;
    }

    /// <summary>Sets IsolationMode on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The IsolationMode fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithIsolationMode(this PluginExecutionContextMock context, int value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.IsolationMode = value;
        return context;
    }

    /// <summary>Sets Depth on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The Depth fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithDepth(this PluginExecutionContextMock context, int value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Depth = value;
        return context;
    }

    /// <summary>Sets SecondaryEntityName on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The SecondaryEntityName fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithSecondaryEntity(this PluginExecutionContextMock context, string value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.SecondaryEntityName = value;
        return context;
    }

    /// <summary>Sets InitiatingUserId on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The InitiatingUserId fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithInitiatingUser(this PluginExecutionContextMock context, Guid value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.InitiatingUserId = value;
        return context;
    }

    /// <summary>Sets RequestId on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The RequestId fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithRequestId(this PluginExecutionContextMock context, Guid? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.RequestId = value;
        return context;
    }

    /// <summary>Sets CorrelationId on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The CorrelationId fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithCorrelationId(this PluginExecutionContextMock context, Guid value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.CorrelationId = value;
        return context;
    }

    /// <summary>Sets OperationId on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The OperationId fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithOperationId(this PluginExecutionContextMock context, Guid value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.OperationId = value;
        return context;
    }

    /// <summary>Sets OperationCreatedOn on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The OperationCreatedOn fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithOperationCreatedOn(this PluginExecutionContextMock context, DateTime value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.OperationCreatedOn = value;
        return context;
    }

    /// <summary>Sets IsExecutingOffline on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The IsExecutingOffline fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithExecutingOffline(this PluginExecutionContextMock context, bool value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.IsExecutingOffline = value;
        return context;
    }

    /// <summary>Sets IsOfflinePlayback on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The IsOfflinePlayback fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithOfflinePlayback(this PluginExecutionContextMock context, bool value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.IsOfflinePlayback = value;
        return context;
    }

    /// <summary>Sets IsInTransaction on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The IsInTransaction fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithInTransaction(this PluginExecutionContextMock context, bool value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.IsInTransaction = value;
        return context;
    }

    /// <summary>Sets OwningExtension on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The OwningExtension fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithOwningExtension(this PluginExecutionContextMock context, EntityReference value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.OwningExtension = value;
        return context;
    }

    /// <summary>Sets ParentContext on the existing mock.</summary>
    /// <param name="context">The existing mock to configure.</param>
    /// <param name="value">The ParentContext fixture value.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithParentContext(this PluginExecutionContextMock context, IPluginExecutionContext value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.ParentContext = value;
        return context;
    }

    /// <summary>Sets the primary entity name and id on the existing mock.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="logicalName">The primary table logical name.</param>
    /// <param name="id">The primary record id.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithPrimaryEntity(this PluginExecutionContextMock context, string logicalName, Guid id)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.PrimaryEntityName = logicalName;
        context.PrimaryEntityId = id;
        return context;
    }

    /// <summary>Sets a named value in the mock's InputParameters collection.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="name">The parameter, variable, or image name.</param>
    /// <param name="value">The value retained by reference.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithInputParameter(this PluginExecutionContextMock context, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.InputParameters[name] = value;
        return context;
    }

    /// <summary>Sets a named value in the mock's OutputParameters collection.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="name">The parameter, variable, or image name.</param>
    /// <param name="value">The value retained by reference.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithOutputParameter(this PluginExecutionContextMock context, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.OutputParameters[name] = value;
        return context;
    }

    /// <summary>Sets a named value in the mock's SharedVariables collection.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="name">The parameter, variable, or image name.</param>
    /// <param name="value">The value retained by reference.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithSharedVariable(this PluginExecutionContextMock context, string name, object? value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.SharedVariables[name] = value;
        return context;
    }

    /// <summary>Sets a named value in the mock's PreEntityImages collection.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="name">The parameter, variable, or image name.</param>
    /// <param name="value">The value retained by reference.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithPreImage(this PluginExecutionContextMock context, string name, Entity value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.PreEntityImages[name] = value;
        return context;
    }

    /// <summary>Sets a named value in the mock's PostEntityImages collection.</summary>
    /// <param name="context">The existing mock.</param>
    /// <param name="name">The parameter, variable, or image name.</param>
    /// <param name="value">The value retained by reference.</param>
    /// <returns>The same mock for further configuration.</returns>
    public static PluginExecutionContextMock WithPostImage(this PluginExecutionContextMock context, string name, Entity value)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.PostEntityImages[name] = value;
        return context;
    }

}
