using System;
using System.Collections.Generic;
using System.Linq;
using CloudAwesome.Xrm.Simulate.Gather.ParityTesting;
using FluentAssertions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NUnit.Framework;

namespace CloudAwesome.Xrm.Simulate.Gather;

[TestFixture]
[Category("Parity")]
[Category("QueryExpression")]
public sealed class QueryExpressionLinkedEntityParityTests : IntegrationBaseFixture
{
    private const string AccountKey = "account";
    private const string ContactsKey = "contacts";
    private const string AccountLogicalName = "account";
    private const string AccountIdAttribute = "accountid";
    private const string AccountNameAttribute = "name";
    private const string AccountNumberAttribute = "accountnumber";
    private const string AccountCreditOnHoldAttribute = "creditonhold";
    private const string ContactLogicalName = "contact";
    private const string ContactIdAttribute = "contactid";
    private const string ContactFirstNameAttribute = "firstname";
    private const string ContactLastNameAttribute = "lastname";
    private const string ContactParentCustomerAttribute = "parentcustomerid";
    private const string AccountAlias = "account";

    [Test(Description = "A QueryExpression linked account column should be returned as an AliasedValue with Dataverse metadata and the raw value type preserved.")]
    public void QueryExpression_LinkedEntity_Should_Return_AliasedValue_With_Metadata_And_Raw_Value()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LinkedEntity_Should_Return_AliasedValue_With_Metadata_And_Raw_Value));
        var accountName = UniqueAccountName(nameof(QueryExpression_LinkedEntity_Should_Return_AliasedValue_With_Metadata_And_Raw_Value));
        const bool creditOnHold = true;

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_LinkedEntity_Should_Return_AliasedValue_With_Metadata_And_Raw_Value),
            ArrangeLive = context =>
            {
                var account = CreateLiveAccount(context, accountName, creditOnHold: creditOnHold);
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName, account.Id) };

                context.State.Set(AccountKey, account);
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => RetrieveLinkedAccountResults(
                service,
                lastName,
                new ColumnSet(ContactFirstNameAttribute),
                new ColumnSet(AccountNameAttribute, AccountCreditOnHoldAttribute))
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression link should project only the linked columns requested by LinkEntity.Columns.")]
    public void QueryExpression_LinkedEntity_Should_Only_Return_Selected_Linked_Columns()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LinkedEntity_Should_Only_Return_Selected_Linked_Columns));
        var accountName = UniqueAccountName(nameof(QueryExpression_LinkedEntity_Should_Only_Return_Selected_Linked_Columns));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_LinkedEntity_Should_Only_Return_Selected_Linked_Columns),
            ArrangeLive = context =>
            {
                var account = CreateLiveAccount(context, accountName);
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName, account.Id) };

                context.State.Set(AccountKey, account);
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => RetrieveLinkedAccountResults(
                service,
                lastName,
                new ColumnSet(ContactFirstNameAttribute),
                new ColumnSet(AccountNameAttribute))
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression link should be able to join through the base lookup even when that lookup is not selected in the base ColumnSet.")]
    public void QueryExpression_LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet));
        var accountName = UniqueAccountName(nameof(QueryExpression_LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_LinkedEntity_Should_Not_Require_Join_Column_In_Base_ColumnSet),
            ArrangeLive = context =>
            {
                var account = CreateLiveAccount(context, accountName);
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName, account.Id) };

                context.State.Set(AccountKey, account);
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => RetrieveLinkedAccountResults(
                service,
                lastName,
                new ColumnSet(ContactFirstNameAttribute),
                new ColumnSet(AccountNameAttribute))
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression link should be able to filter on a linked account column that is not projected into the result.")]
    public void QueryExpression_LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet));
        var accountName = UniqueAccountName(nameof(QueryExpression_LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet));
        var accountNumber = UniqueAccountNumber();

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_LinkedEntity_Should_Not_Require_Linked_Filter_Column_In_Linked_ColumnSet),
            ArrangeLive = context =>
            {
                var account = CreateLiveAccount(context, accountName, accountNumber);
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName, account.Id) };

                context.State.Set(AccountKey, account);
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = BuildLinkedAccountQuery(
                    lastName,
                    new ColumnSet(ContactFirstNameAttribute),
                    new ColumnSet(AccountNameAttribute));
                query.LinkEntities[0].LinkCriteria.AddCondition(
                    AccountNumberAttribute,
                    ConditionOperator.Equal,
                    accountNumber);

                return ProjectLinkedAccountResults(service.RetrieveMultiple(query).Entities);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression left outer linked account join should keep a contact that has no parent account and should not add aliased values.")]
    public void QueryExpression_LeftOuter_Link_Should_Return_Base_Row_When_No_Linked_Record_Matches()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LeftOuter_Link_Should_Return_Base_Row_When_No_Linked_Record_Matches));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_LeftOuter_Link_Should_Return_Base_Row_When_No_Linked_Record_Matches),
            ArrangeLive = context =>
            {
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName) };
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = BuildLinkedAccountQuery(
                    lastName,
                    new ColumnSet(ContactFirstNameAttribute),
                    new ColumnSet(AccountNameAttribute));
                query.LinkEntities[0].JoinOperator = JoinOperator.LeftOuter;

                return ProjectLinkedAccountResults(service.RetrieveMultiple(query).Entities);
            }
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression inner linked account join should exclude a contact that has no parent account match.")]
    public void QueryExpression_Inner_Link_Should_Exclude_Base_Row_When_No_Linked_Record_Matches()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_Inner_Link_Should_Exclude_Base_Row_When_No_Linked_Record_Matches));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(QueryExpression_Inner_Link_Should_Exclude_Base_Row_When_No_Linked_Record_Matches),
            ArrangeLive = context =>
            {
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName) };
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => RetrieveLinkedAccountResults(
                service,
                lastName,
                new ColumnSet(ContactFirstNameAttribute),
                new ColumnSet(AccountNameAttribute))
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "A QueryExpression order declared on a LinkEntity should sort result rows by the linked account column value.")]
    public void QueryExpression_LinkedEntity_Order_Should_Sort_By_Linked_Column_Value()
    {
        var lastName = UniqueLastName(nameof(QueryExpression_LinkedEntity_Order_Should_Sort_By_Linked_Column_Value));

        var scenario = new DataverseParityScenario<IReadOnlyList<string?>>
        {
            Name = nameof(QueryExpression_LinkedEntity_Order_Should_Sort_By_Linked_Column_Value),
            ArrangeLive = context =>
            {
                var zuluAccount = CreateLiveAccount(context, $"Zulu {UniqueAccountNumber()}");
                var alphaAccount = CreateLiveAccount(context, $"Alpha {UniqueAccountNumber()}");
                var contacts = new[]
                {
                    CreateLiveContact(context, "Zulu", lastName, zuluAccount.Id),
                    CreateLiveContact(context, "Alpha", lastName, alphaAccount.Id)
                };

                context.State.Set(AccountKey, new[] { zuluAccount, alphaAccount });
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service =>
            {
                var query = BuildLinkedAccountQuery(
                    lastName,
                    new ColumnSet(ContactFirstNameAttribute),
                    new ColumnSet(AccountNameAttribute));
                query.LinkEntities[0].Orders.Add(new OrderExpression(AccountNameAttribute, OrderType.Ascending));

                return service.RetrieveMultiple(query)
                    .Entities
                    .Select(entity => entity.GetAttributeValue<string>(ContactFirstNameAttribute))
                    .ToList();
            },
            AssertEquivalent = (live, simulated) => simulated.Should().Equal(live)
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "FetchXML linked account attributes should be returned as AliasedValue results through the shared QueryExpression execution path.")]
    public void FetchXml_LinkedEntity_Should_Return_AliasedValue()
    {
        var lastName = UniqueLastName(nameof(FetchXml_LinkedEntity_Should_Return_AliasedValue));
        var accountName = UniqueAccountName(nameof(FetchXml_LinkedEntity_Should_Return_AliasedValue));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(FetchXml_LinkedEntity_Should_Return_AliasedValue),
            ArrangeLive = context =>
            {
                var account = CreateLiveAccount(context, accountName);
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName, account.Id) };

                context.State.Set(AccountKey, account);
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => ProjectLinkedAccountResults(
                service.RetrieveMultiple(new FetchExpression(LinkedAccountFetchXml(lastName))).Entities)
        };

        DataverseParityHarness.Execute(scenario);
    }

    [Test(Description = "FetchXML link-type='outer' should behave like JoinOperator.LeftOuter and keep a contact that has no parent account.")]
    public void FetchXml_Outer_Link_Should_Map_To_LeftOuter_Join()
    {
        var lastName = UniqueLastName(nameof(FetchXml_Outer_Link_Should_Map_To_LeftOuter_Join));

        var scenario = new DataverseParityScenario<IReadOnlyList<ContactLinkedAccountResult>>
        {
            Name = nameof(FetchXml_Outer_Link_Should_Map_To_LeftOuter_Join),
            ArrangeLive = context =>
            {
                var contacts = new[] { CreateLiveContact(context, "Ada", lastName) };
                context.State.Set(ContactsKey, contacts);
            },
            ArrangeSimulated = SeedSimulationFromState,
            Act = service => ProjectLinkedAccountResults(
                service.RetrieveMultiple(new FetchExpression(LinkedAccountFetchXml(lastName, "outer"))).Entities)
        };

        DataverseParityHarness.Execute(scenario);
    }

    private static IReadOnlyList<ContactLinkedAccountResult> RetrieveLinkedAccountResults(
        IOrganizationService service,
        string lastName,
        ColumnSet contactColumns,
        ColumnSet accountColumns)
    {
        var query = BuildLinkedAccountQuery(lastName, contactColumns, accountColumns);

        return ProjectLinkedAccountResults(service.RetrieveMultiple(query).Entities);
    }

    private static QueryExpression BuildLinkedAccountQuery(
        string lastName,
        ColumnSet contactColumns,
        ColumnSet accountColumns)
    {
        var query = new QueryExpression(ContactLogicalName)
        {
            ColumnSet = contactColumns,
            Criteria = new FilterExpression(LogicalOperator.And)
        };

        query.Criteria.AddCondition(ContactLastNameAttribute, ConditionOperator.Equal, lastName);
        query.Orders.Add(new OrderExpression(ContactFirstNameAttribute, OrderType.Ascending));
        query.LinkEntities.Add(new LinkEntity
        {
            LinkFromEntityName = ContactLogicalName,
            LinkFromAttributeName = ContactParentCustomerAttribute,
            LinkToEntityName = AccountLogicalName,
            LinkToAttributeName = AccountIdAttribute,
            EntityAlias = AccountAlias,
            JoinOperator = JoinOperator.Inner,
            Columns = accountColumns
        });

        return query;
    }

    private static string LinkedAccountFetchXml(string lastName, string linkType = "inner")
    {
        return $"""
                <fetch version='1.0' mapping='logical'>
                  <entity name='{ContactLogicalName}'>
                    <attribute name='{ContactFirstNameAttribute}' />
                    <filter type='and'>
                      <condition attribute='{ContactLastNameAttribute}' operator='eq' value='{lastName}' />
                    </filter>
                    <order attribute='{ContactFirstNameAttribute}' descending='false' />
                    <link-entity name='{AccountLogicalName}' from='{AccountIdAttribute}' to='{ContactParentCustomerAttribute}' link-type='{linkType}' alias='{AccountAlias}'>
                      <attribute name='{AccountNameAttribute}' />
                    </link-entity>
                  </entity>
                </fetch>
                """;
    }

    private static IReadOnlyList<ContactLinkedAccountResult> ProjectLinkedAccountResults(
        IEnumerable<Entity> entities)
    {
        return entities
            .Select(entity => new ContactLinkedAccountResult(
                entity.GetAttributeValue<string>(ContactFirstNameAttribute),
                entity.Contains(ContactParentCustomerAttribute),
                ProjectAliasedValues(entity)))
            .ToList();
    }

    private static IReadOnlyList<AliasedAttributeResult> ProjectAliasedValues(Entity entity)
    {
        return entity.Attributes
            .Where(attribute => attribute.Value is AliasedValue)
            .OrderBy(attribute => attribute.Key, StringComparer.Ordinal)
            .Select(attribute =>
            {
                var aliasedValue = (AliasedValue)attribute.Value;
                var value = ProjectValue(aliasedValue.Value);

                return new AliasedAttributeResult(
                    attribute.Key,
                    aliasedValue.EntityLogicalName,
                    aliasedValue.AttributeLogicalName,
                    value?.GetType().FullName,
                    value);
            })
            .ToList();
    }

    private static object? ProjectValue(object? value)
    {
        return value switch
        {
            Money money => money.Value,
            OptionSetValue optionSetValue => optionSetValue.Value,
            EntityReference entityReference => new EntityReferenceResult(
                entityReference.LogicalName,
                entityReference.Id,
                entityReference.Name),
            AliasedValue aliasedValue => ProjectValue(aliasedValue.Value),
            _ => value
        };
    }

    private static void SeedSimulationFromState(SimulatedDataverseScenarioContext context)
    {
        foreach (var account in GetMany<AccountSeed>(context.State, AccountKey))
        {
            context.Simulation.Data().Add(Account(account));
        }

        foreach (var contact in context.State.Get<IReadOnlyList<ContactSeed>>(ContactsKey))
        {
            context.Simulation.Data().Add(Contact(contact));
        }
    }

    private static IReadOnlyList<T> GetMany<T>(DataverseScenarioState state, string key)
    {
        try
        {
            return state.Get<IReadOnlyList<T>>(key);
        }
        catch (KeyNotFoundException)
        {
            return Array.Empty<T>();
        }
        catch (InvalidCastException)
        {
            return new[] { state.Get<T>(key) };
        }
    }

    private static AccountSeed CreateLiveAccount(
        LiveDataverseScenarioContext context,
        string name,
        string? accountNumber = null,
        bool? creditOnHold = null)
    {
        var account = new Entity(AccountLogicalName)
        {
            Attributes =
            {
                [AccountNameAttribute] = name,
                [AccountNumberAttribute] = accountNumber ?? UniqueAccountNumber()
            }
        };

        if (creditOnHold.HasValue)
        {
            account[AccountCreditOnHoldAttribute] = creditOnHold.Value;
        }

        var id = context.Service.Create(account);
        context.Cleanup.TrackForDelete(AccountLogicalName, id);

        return new AccountSeed(
            id,
            name,
            account.GetAttributeValue<string>(AccountNumberAttribute),
            creditOnHold);
    }

    private static ContactSeed CreateLiveContact(
        LiveDataverseScenarioContext context,
        string firstName,
        string lastName,
        Guid? parentAccountId = null)
    {
        var contact = new Entity(ContactLogicalName)
        {
            Attributes =
            {
                [ContactFirstNameAttribute] = firstName,
                [ContactLastNameAttribute] = lastName
            }
        };

        if (parentAccountId.HasValue)
        {
            contact[ContactParentCustomerAttribute] = new EntityReference(AccountLogicalName, parentAccountId.Value);
        }

        var id = context.Service.Create(contact);
        context.Cleanup.TrackForDelete(ContactLogicalName, id);

        return new ContactSeed(id, firstName, lastName, parentAccountId);
    }

    private static Entity Account(AccountSeed seed)
    {
        var account = new Entity(AccountLogicalName) { Id = seed.Id };
        account[AccountIdAttribute] = seed.Id;
        account[AccountNameAttribute] = seed.Name;
        account[AccountNumberAttribute] = seed.AccountNumber;

        if (seed.CreditOnHold.HasValue)
        {
            account[AccountCreditOnHoldAttribute] = seed.CreditOnHold.Value;
        }

        return account;
    }

    private static Entity Contact(ContactSeed seed)
    {
        var contact = new Entity(ContactLogicalName) { Id = seed.Id };
        contact[ContactIdAttribute] = seed.Id;
        contact[ContactFirstNameAttribute] = seed.FirstName;
        contact[ContactLastNameAttribute] = seed.LastName;

        if (seed.ParentAccountId.HasValue)
        {
            contact[ContactParentCustomerAttribute] = new EntityReference(AccountLogicalName, seed.ParentAccountId.Value);
        }

        return contact;
    }

    private static string UniqueLastName(string testName)
    {
        return $"CASim {Guid.NewGuid():N}";
    }

    private static string UniqueAccountName(string testName)
    {
        return $"CASim {testName} {Guid.NewGuid():N}";
    }

    private static string UniqueAccountNumber()
    {
        return $"CASIM-{Guid.NewGuid():N}"[..19];
    }

    private sealed record AccountSeed(Guid Id, string Name, string AccountNumber, bool? CreditOnHold);

    private sealed record ContactSeed(Guid Id, string FirstName, string LastName, Guid? ParentAccountId);

    private sealed record ContactLinkedAccountResult(
        string? FirstName,
        bool ContainsParentCustomerId,
        IReadOnlyList<AliasedAttributeResult> AliasedAttributes);

    private sealed record AliasedAttributeResult(
        string Key,
        string? EntityLogicalName,
        string? AttributeLogicalName,
        string? ValueTypeName,
        object? Value);

    private sealed record EntityReferenceResult(string? LogicalName, Guid Id, string? Name);
}
