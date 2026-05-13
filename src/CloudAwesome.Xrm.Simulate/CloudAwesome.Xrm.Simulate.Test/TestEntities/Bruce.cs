using System;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using Microsoft.Xrm.Sdk;

namespace CloudAwesome.Xrm.Simulate.Test.TestEntities;

/// <summary>
/// Describe usage of the Siobhan persona, so that it's visible as a tool tip in tests... =D
/// </summary>
public static class Bruce
{
    /// <summary>
    /// Describe usage of the Siobhan contact, so that it's visible as a tool tip in tests... =D
    /// </summary>
    public static Contact Contact()
    {
        return new Contact
        {
            FirstName = "Bruce",
            LastName = "Purves",
            GenderCode = Contact_GenderCode.Male,
            StatusCode = Contact_StatusCode.Active,
            OverriddenCreatedOn = new DateTime(2008, 01, 06),
            NumberOfChildren = 2
        };
    }

    /// <summary>
    /// Describe usage of the Siobhan Account, so that it's visible as a tool tip in tests... =D
    /// </summary>
    public static Entity Account()
    {
        return new Entity("account", Guid.Parse("4980d1f5-486c-4e1d-b953-7c334b968ae8"))
        {
            Attributes =
            {
                ["Id"] = Guid.Parse("4980d1f5-486c-4e1d-b953-7c334b968ae8"),
                ["name"] = "Stumpy & Co."
            }
        };
    }
}