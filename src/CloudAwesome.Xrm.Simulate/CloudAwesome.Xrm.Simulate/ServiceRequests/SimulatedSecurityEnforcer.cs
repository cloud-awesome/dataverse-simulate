using CloudAwesome.Xrm.Simulate.DataServices;
using CloudAwesome.Xrm.Simulate.Interfaces;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests;

internal sealed class SimulatedSecurityEnforcer(MockedEntityDataService dataService)
{
    internal void DemandTableAccess(
        string entityLogicalName,
        SecurityPrivilege privilege,
        ISimulatorOptions? options)
    {
        if (!TryGetContext(options, out var context))
            return;

        var decision = PermissionsCalculator.CanPerform(
            context,
            dataService.AuthenticatedUser,
            entityLogicalName,
            privilege);

        Demand(decision);
    }

    internal void DemandRecordAccess(
        Entity record,
        SecurityPrivilege privilege,
        ISimulatorOptions? options)
    {
        if (!TryGetContext(options, out var context))
            return;

        var decision = PermissionsCalculator.CanAccessRecord(
            context,
            dataService.AuthenticatedUser,
            record,
            privilege);

        Demand(decision);
    }

    internal IReadOnlyList<Entity> FilterReadableRecords(
        string entityLogicalName,
        IEnumerable<Entity> records,
        ISimulatorOptions? options)
    {
        if (!TryGetContext(options, out var context))
            return records.ToList();

        return PermissionsCalculator.FilterReadableRecords(
            context,
            dataService.AuthenticatedUser,
            entityLogicalName,
            records);
    }

    private static bool TryGetContext(
        ISimulatorOptions? options,
        out SecurityEvaluationContext context)
    {
        if (options?.SimulatedSecurityModel is SimulatedSecurityModel securityModel)
        {
            context = new SecurityEvaluationContext(securityModel);
            return true;
        }

        context = null!;
        return false;
    }

    private static void Demand(SecurityDecision decision)
    {
        if (!decision.Allowed)
        {
            throw DataverseServiceFaults.AccessDenied(decision.DenialReason);
        }
    }
}
