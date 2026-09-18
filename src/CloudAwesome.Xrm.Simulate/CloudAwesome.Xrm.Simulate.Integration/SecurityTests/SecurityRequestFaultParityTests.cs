using System;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather.SecurityTests;

[TestFixture]
[Category("Parity")]
[Category("Security")]
[Category("Exceptions")]
public sealed class SecurityRequestFaultParityTests : IntegrationBaseFixture
{
    private const string AccountLogicalName = "account";
    private const string AccountNameAttribute = "name";
    private const string CurrentUserIdKey = "current-user-id";
    private const string CurrentBusinessUnitIdKey = "current-business-unit-id";
    private const string AccountIdKey = "account-id";

    private static readonly Guid MissingAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid MissingUserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid MissingTeamId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Test]
    public void GrantAccess_Missing_Target_Should_Match_Live_Dataverse_Fault()
    {
        EntityReference? currentUser = null;

        var scenario = new DataverseParityScenario<DataverseFaultShape>
        {
            Name = nameof(GrantAccess_Missing_Target_Should_Match_Live_Dataverse_Fault),
            ArrangeLive = context =>
            {
                currentUser = CurrentUser(context.Service);
                context.State.Set(CurrentUserIdKey, currentUser.Id);
            },
            Act = service => CaptureFault(service, serviceUnderTest =>
            {
                serviceUnderTest.Execute(new GrantAccessRequest
                {
                    Target = new EntityReference(AccountLogicalName, MissingAccountId),
                    PrincipalAccess = new PrincipalAccess
                    {
                        Principal = currentUser ?? CurrentUser(serviceUnderTest),
                        AccessMask = AccessRights.ReadAccess
                    }
                });
            })
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void GrantAccess_Missing_Principal_Should_Match_Live_Dataverse_Fault()
    {
        SecuritySeed? seed = null;

        var scenario = new DataverseParityScenario<DataverseFaultShape>
        {
            Name = nameof(GrantAccess_Missing_Principal_Should_Match_Live_Dataverse_Fault),
            ArrangeLive = context =>
            {
                var whoAmI = (WhoAmIResponse)context.Service.Execute(new WhoAmIRequest());
                var accountId = context.Service.Create(Account(Guid.Empty, nameof(GrantAccess_Missing_Principal_Should_Match_Live_Dataverse_Fault)));
                context.Cleanup.TrackForDelete(AccountLogicalName, accountId);

                seed = new SecuritySeed(accountId, whoAmI.UserId, whoAmI.BusinessUnitId);
                context.State.Set(AccountIdKey, accountId);
                context.State.Set(CurrentUserIdKey, whoAmI.UserId);
                context.State.Set(CurrentBusinessUnitIdKey, whoAmI.BusinessUnitId);
            },
            CreateSimulatorOptions = context =>
            {
                var userId = context.State.Get<Guid>(CurrentUserIdKey);
                var businessUnitId = context.State.Get<Guid>(CurrentBusinessUnitIdKey);

                return CreateShareEnabledOptions(userId, businessUnitId);
            },
            ArrangeSimulated = context =>
            {
                var accountId = context.State.Get<Guid>(AccountIdKey);
                var userId = context.State.Get<Guid>(CurrentUserIdKey);

                context.Simulation.Data().Add(Account(accountId, nameof(GrantAccess_Missing_Principal_Should_Match_Live_Dataverse_Fault), User(userId)));
            },
            Act = service => CaptureFault(service, serviceUnderTest =>
            {
                var arrangedSeed = seed ?? throw new InvalidOperationException("Security seed was not arranged.");
                serviceUnderTest.Execute(new GrantAccessRequest
                {
                    Target = new EntityReference(AccountLogicalName, arrangedSeed.AccountId),
                    PrincipalAccess = new PrincipalAccess
                    {
                        Principal = User(MissingUserId),
                        AccessMask = AccessRights.ReadAccess
                    }
                });
            })
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test]
    public void AddMembersTeam_Missing_Team_Should_Match_Live_Dataverse_Fault()
    {
        EntityReference? currentUser = null;

        var scenario = new DataverseParityScenario<DataverseFaultShape>
        {
            Name = nameof(AddMembersTeam_Missing_Team_Should_Match_Live_Dataverse_Fault),
            ArrangeLive = context =>
            {
                currentUser = CurrentUser(context.Service);
                context.State.Set(CurrentUserIdKey, currentUser.Id);
            },
            CreateSimulatorOptions = context =>
            {
                var userId = context.State.Get<Guid>(CurrentUserIdKey);
                var businessUnitId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");

                return new SimulatorOptions
                {
                    AuthenticatedUser = new Entity("systemuser", userId),
                    SimulatedSecurityModel = SimulatedSecurityModel.Create()
                        .WithBusinessUnit(businessUnitId, "Root")
                        .WithUser(userId, businessUnitId)
                };
            },
            Act = service => CaptureFault(service, serviceUnderTest =>
            {
                serviceUnderTest.Execute(new AddMembersTeamRequest
                {
                    TeamId = MissingTeamId,
                    MemberIds = [(currentUser ?? CurrentUser(serviceUnderTest)).Id]
                });
            })
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static SimulatorOptions CreateShareEnabledOptions(Guid userId, Guid businessUnitId)
    {
        return new SimulatorOptions
        {
            AuthenticatedUser = new Entity("systemuser", userId)
            {
                ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
            },
            SimulatedSecurityModel = SimulatedSecurityModel.Create()
                .WithBusinessUnit(businessUnitId, "Root")
                .WithUser(userId, businessUnitId)
                .WithRole("Share Account", role => role.CanShare(AccountLogicalName, PrivilegeDepthEnum.Organization))
                .AssignRoleToUser("Share Account", userId)
        };
    }

    private static EntityReference CurrentUser(IOrganizationService service)
    {
        var response = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
        return User(response.UserId);
    }

    private static DataverseFaultShape CaptureFault(
        IOrganizationService service,
        Action<IOrganizationService> act)
    {
        try
        {
            act(service);
        }
        catch (Exception exception)
        {
            return DataverseFaultShape.From(exception);
        }

        throw new AssertionException("Operation unexpectedly succeeded.");
    }

    private static Entity Account(Guid id, string testName, EntityReference? owner = null)
    {
        var account = new Entity(AccountLogicalName)
        {
            [AccountNameAttribute] = $"CASim {testName} {Guid.NewGuid():N}"
        };

        if (id != Guid.Empty)
        {
            account.Id = id;
        }

        if (owner is not null)
        {
            account["ownerid"] = owner;
        }

        return account;
    }

    private static EntityReference User(Guid userId) => new("systemuser", userId);

    private sealed record SecuritySeed(Guid AccountId, Guid UserId, Guid BusinessUnitId);

    private sealed record DataverseFaultShape(
        string ExceptionType,
        string? FaultType,
        int? ErrorCode)
    {
        public static DataverseFaultShape From(Exception exception)
        {
            if (exception is FaultException<OrganizationServiceFault> faultException)
            {
                return new DataverseFaultShape(
                    exception.GetType().FullName!,
                    faultException.Detail.GetType().FullName,
                    faultException.Detail.ErrorCode);
            }

            return new DataverseFaultShape(
                exception.GetType().FullName!,
                null,
                null);
        }
    }
}
