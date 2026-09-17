# Security model roadmap

## Goal

Phase 1 security should make access checks a first-class, optional part of the simulator pipeline.

The target is not to rebuild all Dataverse security behavior at once. The target is a loyal, testable model for the security paths most plugin and service tests depend on:

- current user identity;
- business unit hierarchy;
- owner and access teams;
- security roles and privilege depths;
- record ownership;
- explicit sharing;
- request and query enforcement.

Security must remain opt-in. Existing tests that do not configure a security model should keep the current permissive behavior.

## Current position

The current implementation has useful foundations:

- `SimulatorOptions.SimulatedSecurityModel` enables opt-in security.
- `EntityPermission` stores entity-level `Create`, `Read`, `Write`, `Delete`, `Append`, `AppendTo`, `Assign`, and `Share` depths.
- `PrivilegeDepthEnum` maps Dataverse depth concepts: none, user/basic, business unit/local, parent-child/deep, and organization/global.
- `SecurityRoleParser` can parse exported role XML files and merge privileges by taking the highest depth.
- `PermissionsCalculator.ValidateEntityPermission(entity, message, options)` blocks configured entity/action pairs where depth is `None`.

The main limitations are:

- only `Create` currently consumes the calculator;
- record-aware calculator overloads always return `true`;
- there is no simulated security graph for users, business units, teams, roles, team memberships, or shares;
- XML role parsing only produces a flat set of permissions, not role assignments to principals;
- privilege depth is treated as any non-`None` value rather than evaluated against record ownership and business-unit scope;
- retrieve/query filtering does not account for readable records;
- organization request handlers do not consistently enforce security;
- failures are generic simulator errors rather than Dataverse-shaped access faults.

## Design principles

- Opt-in by configuration: no security model means allow current behavior.
- Entity-level checks and record-level checks should be separate concepts. `Create` can be entity-scoped, but `Read`, `Write`, `Delete`, `Assign`, `Share`, `Append`, and `AppendTo` need record context.
- The calculator should be pure and deterministic. It should receive a security context, principal, privilege/action, and record references, then return a structured decision.
- Service requests should depend on a security enforcement component, not reach into model internals.
- Query security should filter rows before projection, paging, aggregation, and final materialization.
- XML import should be one input path, not the in-memory model itself.
- Metadata should improve accuracy where available, especially ownership type, relationship targets, and valid messages, but security should still work for manually seeded entities without metadata.
- The existing implementation has not been released into any production usage, so this implementation can be changed without any worries about backward compatibility.
- The security model should live in its own namespace where it makes sense to do so, to assist in code maintenance. 
- While some performance degradation is expected, all efforts should be made to keep performance as fast as possible.
- Always keep in mind that this is a unit-test-only framework. We are not replicating the dataverse platform, we only need to provide enough surface area and parity to provide a realistic test double, and nothing more.

## Proposed model

### Runtime security graph

Introduce simulator-owned runtime models:

- `SimulatedSecurityModel`
- `SimulatedBusinessUnit`
- `SimulatedUser`
- `SimulatedTeam`
- `SimulatedSecurityRole`
- `SimulatedRolePrivilege`
- `SimulatedRoleAssignment`
- `SimulatedTeamMembership`
- `SimulatedPrincipalObjectAccess`

The model should index common lookups:

- user by `systemuserid`;
- team by `teamid`;
- business unit by `businessunitid`;
- business unit parent/child relationships;
- roles assigned to users and teams;
- teams for a user;
- shares by target record and principal.

Keep the existing `ISecurityModel` surface source-compatible if practical, but treat it as a facade over richer runtime state. If the interface becomes too restrictive, add new interfaces rather than overloading `EntityPermissions` with unrelated concepts.
 
### Principals

Represent principals as users or teams:

- user principal: `systemuser`;
- team principal: `team`.

For a given authenticated user, effective privileges should come from:

- roles assigned directly to the user;
- roles assigned to owner teams the user belongs to;
- record-specific access granted through sharing to the user;
- record-specific access granted through sharing to teams the user belongs to.

Access teams should be represented for record access and membership, but should not grant table privileges unless role behavior is explicitly supported for that team type. Access Teams should be considered in the architecture but will be a post-MVP implementation.

### Business units

The business unit model should support:

- root business unit;
- parent-child hierarchy;
- user business unit;
- team business unit;
- efficient descendant checks.

Privilege depth should evaluate as:

- `None`: no access from that privilege.
- `User`: records owned by the user, records owned by teams the user belongs to where applicable, or records shared to the user/team with the required right.
- `BusinessUnit`: records owned by users or teams in the same business unit as the acting principal.
- `ParentChild`: records owned by users or teams in the acting principal's business unit or descendant business units.
- `Organization`: records in the organization, subject to ownership type and table support.

Create is special: there is no existing record ownership to evaluate. Any depth above `None` should allow create for user-owned or organization-owned tables, with owner defaulting handled by the service request layer. The only exception is when code attempts to create a record in a business unit that the impersonated principal does not have access to.

### Roles and privileges

Keep `EntityPermission` as a privilege set shape, but rename or wrap it conceptually as a role privilege in the richer model.

The model should distinguish:

- a security role definition;
- privileges included in a role;
- assignment of a role to a user or team;
- the effective merged privileges for an acting user.

Merged privileges should take the highest depth per entity/action across all applicable roles, matching Dataverse behavior.

### Sharing and access rights

Add a simulated principal-object-access store that supports the common access requests:

- `GrantAccessRequest`;
- `ModifyAccessRequest`;
- `RevokeAccessRequest`;
- `RetrievePrincipalAccessRequest`;
- `RetrieveSharedPrincipalsAndAccessRequest`.

Record shares should grant access rights independently of role depth, but should not bypass the need for table-level privileges where Dataverse requires a relevant privilege. The first implementation can use the pragmatic rule that a matching explicit share grants record-level access when the principal has any non-`None` privilege for that table/action; live parity tests should refine this.

## Calculator contract

Replace boolean-only decisions with a structured result:

```csharp
public sealed record SecurityDecision(
    bool Allowed,
    string? DenialReason = null,
    PrivilegeDepthEnum EffectiveDepth = PrivilegeDepthEnum.None);
```

Recommended calculator methods:

```csharp
SecurityDecision CanPerform(
    SecurityEvaluationContext context,
    EntityReference principal,
    string entityLogicalName,
    SecurityPrivilege privilege);

SecurityDecision CanAccessRecord(
    SecurityEvaluationContext context,
    EntityReference principal,
    Entity record,
    SecurityPrivilege privilege);

IReadOnlyList<Entity> FilterReadableRecords(
    SecurityEvaluationContext context,
    EntityReference principal,
    string entityLogicalName,
    IEnumerable<Entity> records);
```

`SecurityPrivilege` should be an enum or constrained value rather than reflection over property names.

The existing `ValidateEntityPermission` methods can remain as compatibility wrappers during migration, but this is not a tight constraint as none of the security model simulation is in production use yet, so breaking changes can be made as makes sense.

## Service enforcement

Add one enforcement component used by direct methods and `OrganizationRequest` handlers:

```csharp
public interface ISecurityEnforcer
{
    void DemandTableAccess(string entityLogicalName, SecurityPrivilege privilege, ISimulatorOptions? options);
    void DemandRecordAccess(Entity record, SecurityPrivilege privilege, ISimulatorOptions? options);
    IReadOnlyList<Entity> FilterReadableRecords(string entityLogicalName, IEnumerable<Entity> records, ISimulatorOptions? options);
}
```

If no model is configured, the enforcer should allow all operations.

If a model is configured and an entity has no security configuration:

- preserve `IgnoreMissingEntities = true` as the default permissive compatibility mode;
- when `IgnoreMissingEntities = false`, deny access clearly.

### Direct method coverage

Enforce security in:

- `Create`: table-level create.
- `Retrieve`: record-level read.
- `RetrieveMultiple`: row-level read filtering.
- `Update`: record-level write.
- `Delete`: record-level delete.
- `Associate`: append on source and append-to on target, using relationship direction when metadata exists.
- `Disassociate`: append/append-to or relationship-specific behavior after live verification.

### Organization request coverage

Route request handlers through the same request services/enforcer so direct methods and `Execute` behave consistently.

Priority request security:

- `CreateRequest`, `RetrieveRequest`, `RetrieveMultipleRequest`, `UpdateRequest`, `DeleteRequest`;
- `AssociateRequest`, `DisassociateRequest`;
- `AssignRequest`;
- `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`;
- `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest`;
- `AddMembersTeamRequest`, `RemoveMembersTeamRequest`.

## Data population APIs

Support manual setup with low ceremony:

```csharp
var security = new SimulatedSecurityModel()
    .WithBusinessUnit(rootBuId, "Root")
    .WithBusinessUnit(childBuId, "Sales", rootBuId)
    .WithUser(userId, childBuId)
    .WithOwnerTeam(teamId, childBuId)
    .WithTeamMember(teamId, userId)
    .WithRole("Salesperson", role => role
        .CanRead("account", PrivilegeDepthEnum.BusinessUnit)
        .CanCreate("contact", PrivilegeDepthEnum.User))
    .AssignRoleToUser("Salesperson", userId)
    .AssignRoleToTeam("Salesperson", teamId);
```

The simulated org service should remain the entry point for all set up (e.g. `orgService.Simulated().SecurityModel()...` or using `SimulatorOptions.SecurityModel` etc...)

Also support object initializer setup for users who prefer explicit data.

The model should provide helpers for common Dataverse entities:

- create `systemuser` entity from `SimulatedUser`;
- create `businessunit` entity from `SimulatedBusinessUnit`;
- create `team` entity from `SimulatedTeam`;
- optionally seed those entities into `InitialiseData`.

Do not require users to populate both simulator security objects and full Dataverse system-table rows unless their test asserts on those rows. When creating users/business units/teams/etc. they should automatically be upserted into the simulator entity store without the user having to write to both places.

## XML and generated input

Keep the current role XML parser, but evolve it into role import:

- parse one or more role XML files into `SimulatedSecurityRole` definitions;
- ~~preserve current flat merged mode for backward compatibility;~~ (we don't care about backward compatibility in this area)
- allow assigning imported roles to users or teams;
- support logical-name overrides;
- keep highest-depth merge semantics when multiple roles are assigned.

Recommended APIs:

```csharp
var security = SimulatedSecurityModel.Create()
    .ImportRoleXml("roles/Salesperson.xml", roleName: "Salesperson")
    .WithUser(userId, businessUnitId)
    .AssignRoleToUser("Salesperson", userId);
```

Later, add JSON support aligned with the metadata roadmap:

- deterministic generated security model JSON;
- schema versioning;
- optional export from a companion CLI command;
- separate files for users/teams/business units/roles/shares if needed.

## Query integration

Security filtering must happen over full stored entities before projection.

Recommended order when a security model is configured:

1. collect candidate rows from the store;
2. apply table existence and metadata validation where configured;
3. apply query filters and joins using full data;
4. apply security readable-record filtering for the base entity;
5. apply security rules for linked entities where Dataverse would restrict rows;
6. apply ordering, distinct, paging, aggregation, and projection according to the query roadmap.

For the first implementation, prioritize base entity read filtering. Link-entity security can follow after query pipeline rework because join semantics and alias materialization are already a known parity gap.

## Exception behavior

- capture live Dataverse fault type, code, and message for missing privileges;
- make strict exception parity configurable;
- add regression tests for both direct methods and request handlers.

Avoid raw `InvalidOperationException` as the long-term behavior for denied access.

Integration parity tests should inform development throughout the implementation. 

## Phased implementation plan

### Step 1 - Model and compatibility

- Add the richer runtime model for users, business units, teams, roles, assignments, memberships, and shares.
- Add indexes and validation for duplicate ids, missing business units, invalid parent references, invalid role assignments, and invalid team memberships.
- Keep existing `EntityPermissions` behavior working as a single implicit role assigned to the authenticated user.
- Add tests for privilege merging and business unit hierarchy evaluation.

### Step 2 - Calculator

- Replace reflection-based message lookup with a typed privilege/action model.
- Implement table-level and record-level access decisions for all privilege depths.
- Implement team role and team membership contribution to effective privileges.
- Implement explicit share checks.
- Add tests for user, business unit, parent-child, organization, team-owned, and shared-record scenarios.

### Step 3 - Service request enforcement

- Add a security enforcer abstraction.
- Wire direct `Create`, `Retrieve`, `RetrieveMultiple`, `Update`, `Delete`, `Associate`, and `Disassociate`.
- Ensure equivalent organization request handlers use the same path.
- Keep security optional and preserve current behavior when no model is configured.
- Add regression tests that denied operations fail and allowed operations continue to audit/process normally.

### Step 4 - Security requests

- Implement principal-object-access storage.
- Add handlers for grant, modify, revoke, retrieve principal access, and retrieve shared principals.
- Add handlers for adding and removing team members.
- Verify response shapes against live Dataverse fixtures where possible.

### Step 5 - XML and fixture ergonomics

- Refactor XML parsing to produce named role definitions.
- Add APIs to assign imported roles to users and teams.
- Add compact fixture helpers for common user/business-unit/team setups.
- Document setup examples in README/docs once behavior stabilizes.

### Step 6 - Live parity hardening

- Capture live fault details for privilege denial, missing principal, missing business unit, missing team, and invalid share operations.
- Refine access-rights behavior for shares versus role privileges.
- Add parity fixtures for owner teams, access teams, business-unit depth, parent-child depth, and organization-owned tables.

## Initial test matrix

A red-green test approach should be followed throughout the implementation.

Minimum unit tests before broad service wiring:

- no configured security model allows all operations;
- missing entity allows or denies based on `IgnoreMissingEntities`;
- ~~flat `EntityPermissions` remains backward compatible;~~
- direct user role grants table-level create;
- `None` denies create/read/write/delete;
- user depth allows own user-owned record and denies another user's record;
- business-unit depth allows same-business-unit owned records and denies sibling-business-unit records;
- parent-child depth allows child-business-unit records and denies parent/sibling records;
- organization depth allows all records for that table;
- owner team role contributes privileges to team members;
- team-owned record is accessible through team membership and role depth;
- explicit user share grants record access;
- explicit team share grants record access to team members;
- retrieve multiple filters inaccessible rows without mutating stored data;
- direct method and equivalent `OrganizationRequest` enforce the same decision.

## Open questions

- Should the first implementation model access teams as record shares only, or should it also expose access-team template behavior?
  - A: Access teams should be a post-MVP implementation. This will be discussed after MVP is complete.
- Should organization-owned tables ignore owner and business-unit depth or treat any non-`None` depth as organization-wide for that table?
  - A: Parity with dataverse is what's important, so preference is that Organization-Owned tables only accept none or organization depth. That said, the default backup would be to treat/parse non-None depth as Organization depth. 
- Should `Append`/`AppendTo` enforcement wait for relationship metadata, or should the first pass use source/target entity checks without validating relationship direction?
  - A: Relationship metadata has been implemented. As with the entire solution, if the test has metadata initialised, use it, if not, fallback to not validating them. 
- Should `AuthenticatedUser` continue to be an `Entity` only, or should `SimulatorOptions` expose an explicit authenticated principal id?
  - A: This may be a good opportunity to revisit the `AuthenticatedUser` model. We should continue to ensure the user exists in the `systemuser` table of the data store, but it probably isn't necessary to expose the entity separately. Perhaps the best solution is to have a UX friendly solution to retrieve the entity from the data store if required. 
- Should strict Dataverse exception parity be part of the first security implementation or a follow-up after live fixtures are captured?
  - A: Parity with dataverse is what's important. This project serves little purpose without it. So it should always be the part of development, even if that makes more full-featured enhancements take longer to get to production. 

## Dependencies and sequencing

This work intersects with:

- [01-simulation-parity](01-simulation-parity.md), because security is a query and request pipeline concern.
- [05-metadata-simulation](05-metadata-simulation.md), because metadata improves ownership type, relationship, and valid-message decisions.

Recommended immediate next step:

1. implement the richer in-memory security graph and typed calculator with unit tests;
2. wire read filtering into `Retrieve` and `RetrieveMultiple`;
3. wire write/delete/associate/disassociate enforcement;
4. add access/share/team request handlers after the base calculator is stable.
