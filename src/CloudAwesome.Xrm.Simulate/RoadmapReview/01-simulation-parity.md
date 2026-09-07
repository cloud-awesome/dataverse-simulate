# Simulation parity roadmap

## Goal

The goal should be "Dataverse-compatible enough that a failing simulated test is a useful predictor of a failing live operation." That requires exact behavior in common paths and explicit documentation for unsupported paths.

## Existing strengths

- Direct `IOrganizationService` methods exist for `Create`, `Retrieve`, `RetrieveMultiple`, `Update`, `Delete`, `Associate`, and `Disassociate`.
- `Execute` has a registry pattern and custom organization request extension point.
- Query support already covers `QueryExpression`, `FetchExpression`, `QueryByAttribute`, many condition operators, links, ordering, distinct, top count, and some aggregates.
- Entity processors allow users to simulate dependent business logic.
- Security role XML parsing exists and can merge role privileges.
- Service-provider mocks cover plugin execution context, tracing, organization service factory, plugin telemetry logger, and service endpoint notification service.

## Critical correctness fixes

### CRUD and direct methods

- `EntityRetriever` has a parity bug for partial-column retrieves. The all-columns branch filters by id, but the selected-column branch projects all rows and then returns `FirstOrDefault()`, so it can return the wrong record.
- `EntityUpdater` currently finds the existing entity, removes it, sets `modifiedon` on the incoming entity, then adds the old entity back. This means updates do not persist incoming attributes. It also uses `ProcessorMessage.Create` rather than update and can dereference a missing entity before throwing the intended error.
- `EntityDisassociator` is not implemented beyond failure injection.
- `EntityCreator` needs duplicate id validation, required system field behavior, state/status defaults, relationship handling, and real Dataverse exception behavior.
- `EntityDeleter` needs missing-table and missing-row behavior that matches live Dataverse and should account for cascading behavior once metadata exists.
- `Associate` currently stores resolved related entities in `RelatedEntities`; Dataverse does not simply mutate the target entity payload this way. The roadmap should move relationships into a relationship store driven by relationship metadata.

### OrganizationRequest execution

Only these built-in request handlers are registered today:

- `CreateRequest`
- `AssignRequest`
- `RetrieveMultipleRequest`
- `WhoAmIRequest`

This leaves a large surface uncovered. Priority requests:

- Direct-method equivalents: `RetrieveRequest`, `UpdateRequest`, `DeleteRequest`, `AssociateRequest`, `DisassociateRequest`.
- Batch/transaction: `ExecuteMultipleRequest`, `ExecuteTransactionRequest`.
- Upsert/key behavior: `UpsertRequest`, `UpsertMultipleRequest` where SDK support is available, alternate key resolution.
- State and ownership: `SetStateRequest`, `AssignRequest` hardening.
- Access/security: `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`, `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest`.
- Teams: `AddMembersTeamRequest`, `RemoveMembersTeamRequest`, owner/access-team scenarios.
- Queues and activities: `AddToQueueRequest`, `RemoveFromQueueRequest`, `PickFromQueueRequest`, `ReleaseToQueueRequest`, `SendEmailRequest`, close/cancel activity requests.
- Metadata: `RetrieveEntityRequest`, `RetrieveAttributeRequest`, `RetrieveAllEntitiesRequest`, `RetrieveOptionSetRequest`, `RetrieveRelationshipRequest`.
- Common platform helpers: `CalculateRollupFieldRequest`, `InitializeFromRequest`, duplicate detection requests, and environment/user requests used by plugins.

Unsupported requests should not fall through to a raw dictionary lookup. Add one of these behaviors:

- Default: throw a clear `NotSupportedException` with request type and request name.
- Optional strict-live mode: throw a Dataverse-shaped `OrganizationServiceFault` where known.
- Optional test escape hatch: allow unknown requests to be configured to fail, no-op, or route to a custom handler.

## QueryExpression and FetchXML parity

### Pipeline ordering

The current `QueryExpressionParser` applies:

1. filter
2. columns
3. linked entities
4. aggregates
5. order
6. distinct
7. top count

This is a source of parity bugs. Projection should not remove attributes needed by filters, joins, ordering, or aggregation. The skipped tests already call this out for filters and link-entities when attributes are not in the column set.

Recommended rework:

- Normalize `QueryExpression`, `FetchExpression`, and `QueryByAttribute` into a shared internal query model.
- Execute against full stored entities.
- Apply base filters and link filters using full data.
- Materialize aliases as `AliasedValue`, not raw attribute values.
- Apply projection only at the end.
- Implement paging and total-count behavior before exposing the final `EntityCollection`.

### Link entity behavior

High-priority gaps:

- Outer join and null semantics.
- Aliased values.
- `JoinOperator` variants, including `LeftOuter`, `Exists`, `In`, `MatchFirstRowUsingCrossApply`, and any/all style operators available in the SDK version.
- Link criteria with alias references.
- Multi-level links without mutating source entities.
- Link ordering and projection behavior.
- Handling missing attributes without throwing LINQ/key exceptions unless Dataverse would do the same.

### Condition operators

Implemented condition operators are useful but incomplete. Priority missing areas:

- `In` and `NotIn`.
- User/team/business-unit scoped operators such as current user, current user teams, current business unit, child business units, and hierarchy variants.
- Fiscal period and fiscal year operators.
- Multi-select option set operators: contains values and does not contain values.
- Mask operators.
- Lookup, money, option set, bool, date-time behavior across all comparison operators.
- Null/missing attribute semantics by type.

### FetchXML

FetchXML is currently converted into `QueryExpression`. That is pragmatic, but mature parity will require FetchXML-specific behavior:

- Full operator mapping.
- Multiple `<value>` nodes.
- `uiname`, `uitype`, and lookup value behavior.
- Aggregate distinct count.
- Aggregate limits and aggregate-limit error behavior.
- `dategrouping` behavior, user timezone behavior, fiscal settings, and aliases.
- Fetch paging, page/count/cookie behavior.
- `no-lock`, `returntotalrecordcount`, and distinct id behavior.

### Paging and limits

Current retrieve multiple handling takes up to `5000` rows and optionally sets total count. Mature behavior should cover:

- `PageInfo.PageNumber`, `Count`, `PagingCookie`, `MoreRecords`.
- Default max page size and caller-requested page size.
- Total record count limit exceeded behavior.
- Aggregate record limits, including the default aggregate limit and explicit aggregate limit.
- Different behavior for `QueryExpression` and `FetchExpression` where Dataverse differs.

## Security parity

Security is currently a guard for basic entity permission and only `Create` calls it. The record-specific overloads return true.

Priority model:

- Apply read security to `Retrieve` and `RetrieveMultiple`.
- Apply write/delete/assign/share/append/append-to checks to direct methods and request handlers.
- Implement owner, business unit, parent-child business unit, organization, and none depth behavior.
- Model team membership, owner teams, access teams, and sharing.
- Model field-level security as a later, explicit feature.
- Use real `AccessRights`, principal access, and share semantics where possible.
- Ensure security failures throw the same exception type, error code, and message shape as live Dataverse.

## Metadata and validation

Parity will be limited until metadata exists. Add a metadata model that can be seeded manually, loaded from generated early-bound metadata, or captured from integration tests.

Priority metadata features:

- Entity primary id and primary name.
- Attribute types, required level, max length, precision, min/max, targets, and valid option values.
- State/status pairs.
- Relationships, cascade configuration, intersect entities, and many-to-many metadata.
- Alternate keys.
- Ownership type.
- Activity/table capabilities.

Use metadata to validate:

- Unknown entities and attributes.
- Missing required values.
- Invalid lookups.
- Invalid option/status/state values.
- Duplicate primary ids and alternate keys.
- Unsupported messages for table type.

## Plugin pipeline parity

The current service-provider simulation helps isolated plugin tests, but it does not simulate the Dataverse plugin pipeline.

Maturity roadmap:

- Step registration model: message, primary entity, stage, mode, filtering attributes, rank, impersonating user, and images.
- Pipeline execution for create/update/delete/associate/disassociate and request handlers.
- Depth and parent context.
- Shared variables propagation.
- Pre and post entity images.
- Transaction scope and rollback behavior.
- Async plugin behavior as recordable simulated output, with live parity optional.

(N.B. To clarify, the "plugin pipeline parity" all still relates to testing of distinct code/plugin units. This framework does not and will never implement a plugin/event in-memory registration mechanism mirroring the dataverse event pipeline. This parity is useful when (for example), code under test validates data passed through from the `ExecutionContext`, but, intentionally, we will never support plugin a (which calls Account Create) automatically triggering plugin b because it is registered on the Account Create message.)

## Parity matrix

Add a generated or manually maintained matrix with statuses:

- `Not implemented`
- `Implemented, unverified`
- `Implemented, live-verified`
- `Partially implemented`
- `Unsupported by design`

Track the matrix by:

- Direct `IOrganizationService` methods.
- Organization requests.
- Query operators and FetchXML features.
- Security features.
- Service-provider/plugin features.
- Metadata validation features.

