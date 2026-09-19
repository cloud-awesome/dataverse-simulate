using System;
using System.Linq;
using System.ServiceModel;
using CloudAwesome.Xrm.Simulate.SecurityModel;
using CloudAwesome.Xrm.Simulate.Test.EarlyBoundEntities;
using FluentAssertions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Test.ServiceRequestsTests.OrganizationRequestsTests;

[TestFixture]
public class CoreOrganizationRequestTests
{
    private IOrganizationService _organizationService = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = _organizationService.Simulate();
    }

    [Test]
    public void Execute_UpsertRequest_Creates_Record_When_Target_Does_Not_Exist()
    {
        var accountId = Guid.NewGuid();

        var response = (UpsertResponse)_organizationService.Execute(new UpsertRequest
        {
            Target = new Account(accountId)
            {
                Name = "Created by upsert"
            }
        });

        var stored = _organizationService.Simulated().Data().Get<Account>(accountId);

        response.ResponseName.Should().Be("Upsert");
        response.RecordCreated.Should().BeTrue();
        response.Target.Should().Be(new EntityReference(Account.EntityLogicalName, accountId));
        stored.Name.Should().Be("Created by upsert");
    }

    [Test]
    public void Execute_UpsertRequest_Updates_Record_When_Target_Already_Exists()
    {
        var accountId = Guid.NewGuid();
        _organizationService.Simulated().Data().Add(new Account(accountId)
        {
            Name = "Original",
            AccountNumber = "A-001"
        });

        var response = (UpsertResponse)_organizationService.Execute(new UpsertRequest
        {
            Target = new Account(accountId)
            {
                Name = "Updated by upsert"
            }
        });

        var stored = _organizationService.Simulated().Data().Get<Account>(accountId);

        response.ResponseName.Should().Be("Upsert");
        response.RecordCreated.Should().BeFalse();
        response.Target.Should().Be(new EntityReference(Account.EntityLogicalName, accountId));
        stored.Name.Should().Be("Updated by upsert");
        stored.AccountNumber.Should().Be("A-001");
    }

    [Test]
    public void Execute_UpsertRequest_Updates_Record_Matched_By_Key_Attributes()
    {
        var accountId = Guid.NewGuid();
        _organizationService.Simulated().Data().Add(new Account(accountId)
        {
            Name = "Original",
            AccountNumber = "A-001"
        });

        var target = new Account(new KeyAttributeCollection
        {
            [Account.Fields.AccountNumber] = "A-001"
        })
        {
            Name = "Updated by key"
        };

        var response = (UpsertResponse)_organizationService.Execute(new UpsertRequest
        {
            Target = target
        });

        var stored = _organizationService.Simulated().Data().Get<Account>(accountId);

        response.RecordCreated.Should().BeFalse();
        response.Target.Should().Be(new EntityReference(Account.EntityLogicalName, accountId));
        stored.Name.Should().Be("Updated by key");
    }

    [Test]
    public void Execute_SetStateRequest_Updates_State_And_Status()
    {
        var accountId = Guid.NewGuid();
        _organizationService.Simulated().Data().Add(new Account(accountId)
        {
            Name = "Stateful Account",
            StateCode = Account_StateCode.Active,
            StatusCode = Account_StatusCode.Active
        });

        var response = (SetStateResponse)_organizationService.Execute(new SetStateRequest
        {
            EntityMoniker = new EntityReference(Account.EntityLogicalName, accountId),
            State = new OptionSetValue((int)Account_StateCode.Inactive),
            Status = new OptionSetValue((int)Account_StatusCode.Inactive)
        });

        var stored = _organizationService.Simulated().Data().Get<Account>(accountId);

        response.ResponseName.Should().Be("SetState");
        stored.GetAttributeValue<OptionSetValue>(Account.Fields.StateCode).Value
            .Should()
            .Be((int)Account_StateCode.Inactive);
        stored.GetAttributeValue<OptionSetValue>(Account.Fields.StatusCode).Value
            .Should()
            .Be((int)Account_StatusCode.Inactive);
    }

    [Test]
    public void Execute_ExecuteMultipleRequest_Returns_Responses_And_Continues_When_Configured()
    {
        var existingAccountId = Guid.NewGuid();
        var missingAccountId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        _organizationService.Simulated().Data().Add(new Account(existingAccountId)
        {
            Name = "Original"
        });

        var response = (ExecuteMultipleResponse)_organizationService.Execute(new ExecuteMultipleRequest
        {
            Settings = new ExecuteMultipleSettings
            {
                ContinueOnError = true,
                ReturnResponses = true
            },
            Requests =
            [
                new UpdateRequest
                {
                    Target = new Account(existingAccountId)
                    {
                        Name = "Updated"
                    }
                },
                new UpdateRequest
                {
                    Target = new Account(missingAccountId)
                    {
                        Name = "Missing"
                    }
                },
                new CreateRequest
                {
                    Target = new Contact
                    {
                        FirstName = "Ada",
                        LastName = "Batch"
                    }
                }
            ]
        });

        response.ResponseName.Should().Be("ExecuteMultiple");
        response.IsFaulted.Should().BeTrue();
        response.Responses.Should().HaveCount(3);
        response.Responses[0].RequestIndex.Should().Be(0);
        response.Responses[0].Response.Should().BeOfType<UpdateResponse>();
        response.Responses[1].RequestIndex.Should().Be(1);
        response.Responses[1].Fault.ErrorCode.Should().Be(-2147220969);
        response.Responses[2].RequestIndex.Should().Be(2);
        response.Responses[2].Response.Should().BeOfType<CreateResponse>();
        _organizationService.Simulated().Data().Get<Account>(existingAccountId).Name.Should().Be("Updated");
        _organizationService.Simulated().Data().Get<Contact>().Should().ContainSingle();
    }

    [Test]
    public void Execute_ExecuteTransactionRequest_Rolls_Back_All_Changes_When_Any_Request_Fails()
    {
        var existingAccountId = Guid.NewGuid();
        var missingAccountId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        _organizationService.Simulated().Data().Add(new Account(existingAccountId)
        {
            Name = "Original"
        });

        var transaction = () => _organizationService.Execute(new ExecuteTransactionRequest
        {
            Requests =
            [
                new UpdateRequest
                {
                    Target = new Account(existingAccountId)
                    {
                        Name = "Should roll back"
                    }
                },
                new UpdateRequest
                {
                    Target = new Account(missingAccountId)
                    {
                        Name = "Missing"
                    }
                }
            ],
            ReturnResponses = true
        });

        transaction.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147220969);
        _organizationService.Simulated().Data().Get<Account>(existingAccountId).Name.Should().Be("Original");
    }

    [Test]
    public void Execute_ExecuteTransactionRequest_Rolls_Back_Security_Model_Changes_When_Request_Fails()
    {
        var fixture = CreateSecurityFixture(role => role.CanShare(Account.EntityLogicalName, PrivilegeDepthEnum.Organization));
        var account = fixture.AddAccount(fixture.OtherUser);

        var transaction = () => fixture.Service.Execute(new ExecuteTransactionRequest
        {
            Requests =
            [
                new GrantAccessRequest
                {
                    Target = account.ToEntityReference(),
                    PrincipalAccess = new PrincipalAccess
                    {
                        Principal = fixture.ActingUser,
                        AccessMask = AccessRights.ReadAccess
                    }
                },
                new UpdateRequest
                {
                    Target = new Account(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"))
                    {
                        Name = "Missing"
                    }
                }
            ]
        });

        transaction.Should().Throw<FaultException<OrganizationServiceFault>>();
        fixture.Security.GetPrincipalAccess(account.ToEntityReference(), fixture.ActingUser)
            .Should()
            .Be(default(AccessRights));
    }

    [Test]
    public void Execute_UpsertRequest_Update_Denies_When_User_Lacks_Write_Access()
    {
        var fixture = CreateSecurityFixture(role => role.CanWrite(Account.EntityLogicalName, PrivilegeDepthEnum.User));
        var account = fixture.AddAccount(fixture.OtherUser);

        var upsert = () => fixture.Service.Execute(new UpsertRequest
        {
            Target = new Account(account.Id)
            {
                Name = "Denied"
            }
        });

        upsert.Should()
            .Throw<FaultException<OrganizationServiceFault>>()
            .Which.Detail.ErrorCode.Should().Be(-2147187962);
    }

    private static SecurityFixture CreateSecurityFixture(Action<SimulatedSecurityRole> configureRole)
    {
        var businessUnitId = Guid.NewGuid();
        var actingUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var authenticatedUser = new Entity("systemuser", actingUserId)
        {
            ["businessunitid"] = new EntityReference("businessunit", businessUnitId)
        };
        var security = SimulatedSecurityModel.Create()
            .WithBusinessUnit(businessUnitId, "Root")
            .WithUser(actingUserId, businessUnitId)
            .WithUser(otherUserId, businessUnitId)
            .WithRole("Test Role", configureRole)
            .AssignRoleToUser("Test Role", actingUserId);

        var service = ((IOrganizationService)null!).Simulate(new SimulatorOptions
        {
            AuthenticatedUser = authenticatedUser,
            SimulatedSecurityModel = security
        });

        return new SecurityFixture(
            service,
            security,
            new EntityReference("systemuser", actingUserId),
            new EntityReference("systemuser", otherUserId));
    }

    private sealed record SecurityFixture(
        IOrganizationService Service,
        SimulatedSecurityModel Security,
        EntityReference ActingUser,
        EntityReference OtherUser)
    {
        public Entity AddAccount(EntityReference owner)
        {
            var record = new Account(Guid.NewGuid())
            {
                Name = "Secured Account",
                OwnerId = owner
            };

            Service.Simulated().Data().Add(record);
            return record;
        }
    }
}
