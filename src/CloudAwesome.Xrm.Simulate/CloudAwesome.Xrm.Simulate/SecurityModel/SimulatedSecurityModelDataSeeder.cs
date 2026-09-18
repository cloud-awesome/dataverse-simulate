using CloudAwesome.Xrm.Simulate.DataServices;

namespace CloudAwesome.Xrm.Simulate.SecurityModel;

internal static class SimulatedSecurityModelDataSeeder
{
    internal static void Seed(MockedEntityDataService dataService, SimulatedSecurityModel securityModel)
    {
        foreach (var businessUnit in securityModel.BusinessUnits)
        {
            dataService.Upsert(businessUnit.ToEntity());
        }

        foreach (var user in securityModel.Users)
        {
            dataService.Upsert(user.ToEntity());
        }

        foreach (var team in securityModel.Teams)
        {
            dataService.Upsert(team.ToEntity());
        }
    }
}
