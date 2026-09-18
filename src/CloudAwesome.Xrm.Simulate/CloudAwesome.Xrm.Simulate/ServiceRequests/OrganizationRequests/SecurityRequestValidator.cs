using CloudAwesome.Xrm.Simulate.DataServices;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.ServiceRequests.OrganizationRequests;

internal static class SecurityRequestValidator
{
    internal static void DemandPrincipalExists(
        MockedEntityDataService dataService,
        EntityReference principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        dataService.Get(principal.LogicalName, principal.Id);
    }

    internal static void DemandTeamExists(
        MockedEntityDataService dataService,
        Guid teamId)
    {
        dataService.Get("team", teamId);
    }

    internal static void DemandUserExists(
        MockedEntityDataService dataService,
        Guid userId)
    {
        dataService.Get("systemuser", userId);
    }
}
