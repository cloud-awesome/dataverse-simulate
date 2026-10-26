# Security model roadmap

Date reviewed: 2026-10-26; implementation update: 2026-10-23

## Current position

The first security model and its service wiring are implemented. The old description of a create-only boolean guard is obsolete. The next work is to harden the implemented model and verify real privilege/ownership behavior, not to build another graph/calculator.

Security remains opt-in. No configured model preserves permissive operation behavior. `IgnoreMissingEntities` controls missing table configuration for the concrete model. Source presence and passing local tests are distinguished from full live parity throughout this review.

The scoped security/identity pass remains delivered. The [26 October review](09-p1-closeout-review.md) identifies a separate ordinary CRUD object-isolation gap that can undermine authorization/audit assertions through caller-held references; it must be closed as P1. Wider security-depth/denial evidence remains P2. The fresh unit suite passes 913 tests; the last full live run was 158 passes on 23 October, including all twelve SecurityIdentity cases.

## Delivered model and setup

- [x] Runtime models for users, business units, owner/access teams, roles, role privileges, assignments, memberships, and principal-object-access shares.
- [x] Graph validation for duplicate identifiers, missing parents/principals/roles/memberships, business-unit hierarchy cycles, and root constraints.
- [x] Default owner teams for business units.
- [x] `SecurityPrivilege`, `SecurityEvaluationContext`, and structured `SecurityDecision` replace reflection-based decisions in the new paths.
- [x] Table access and record access evaluated for none, user, business-unit, parent-child, and organization depths.
- [x] Effective privileges merge direct user roles and owner-team roles at the highest depth.
- [x] User/team shares can grant record access when the corresponding table privilege exists.
- [x] XML import produces named roles; individual files, file lists, directories, and logical-name overrides are supported.
- [x] Manual fluent setup and `.Simulated().SecurityModel()` facade exist.
- [x] Setup seeds ordinary businessunit/systemuser/team/role/membership/role-assignment rows without requiring duplicate manual seeding.
- [x] Roles are represented in each relevant business unit with parent/root links; role assignment association/disassociation is reflected in effective privileges and entity rows.

The original model/calculator/enforcement/requests/XML setup steps are substantially delivered. Broad live hardening remains open.

## Delivered enforcement

| Surface | Current checks/behavior | Qualification |
| --- | --- | --- |
| Create | Table create privilege; valid owner with table Read; derived owning fields | Owning BU follows the owner; independent modernized BU ownership is explicitly unsupported. |
| Retrieve | Record read privilege | Uses configured acting user. |
| RetrieveMultiple | Base and linked rows filtered before joins/projection/counts/paging/aggregation | Includes nested/self links; restricted-user live fixture requires configuration. |
| Update/delete | Record write/delete privilege; owner changes share assignment validation | Field-level security remains deferred. |
| Associate/disassociate | Append-to on target, append on related rows | General metadata relationship direction is not consumed. |
| Assign | Record assign/write/read privileges, valid user/owner-team assignee with table Read, owning-field synchronization | Cascades and organization-specific ownership settings remain deferred. |
| Sharing | Grant/modify/revoke and principal/shared-access responses | Broader operation-specific privilege and access-mask parity still needs live evidence. |
| Teams | Add/remove member handlers update membership and effective permissions | Exact authorization/fault requirements need wider fixtures. |
| Queues | Target/queue read and queue-item create/write/delete checks | Current minimum policy is not a verified complete Dataverse privilege set. |
| Transactions | Security state is snapshotted/restored alongside data/audits | External custom-handler side effects are not automatically transactional. |

Implemented requests:

- [x] `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`.
- [x] `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest`.
- [x] `AddMembersTeamRequest`, `RemoveMembersTeamRequest`.
- [x] Direct-method equivalents and relevant core requests route through shared enforcement paths.

Metadata retrieval handlers intentionally do not demand table-read access. Unit tests and two integration fixtures cover selected aspects of that contract; do not add a broad record-read guard to all metadata requests.

## Scope and design decisions retained

- Preserve optional security and the framework's isolated unit-test purpose.
- Keep privilege calculation deterministic, with service operations depending on an enforcer rather than duplicating role/share logic.
- Model ownership/business-unit scope using explicit principals and records, with metadata improving supported semantics when configured.
- Continue using exported role XML as one input mechanism, not as the runtime graph itself.
- No compatibility constraint requires preserving the original unreleased security design. Compatibility wrappers exist, but retaining them is not a reason to preserve incorrect behavior.
- Full access-team templates and automatic record-specific team workflows remain deferred. Basic access-team objects, membership, and explicit team shares already exist; they do not confer owner-team role privileges.
- Field-level security, manager/position hierarchy, and external identity/Entra membership behavior are not implemented.
- Do not simulate an automatic plugin/event pipeline to support security tests.

The previous open question about authenticated-user representation has a partial answer in code: options still accept an `Entity`, the data service stores its `EntityReference`, and the user row is available in the entity store. An API redesign is optional P5 work, not a security prerequisite.

## P1 - Correctness gaps in implemented behavior

### Joined data and acting identity

- [x] Filter readable linked rows before inner/outer join matching and before aggregates/counts/paging, including nested/self links and user/team shares.
- [x] Honor `CreateOrganizationService(userId)` without resetting shared state; each service has its own caller view over the shared entity/relationship store and audit service.
- [x] Define/test null SYSTEM, `Guid.Empty` plugin-user, explicit caller, and initiating-user distinctions. Unknown explicit callers fault; service creation does not change the provider or another service's identity.
- [x] Resolve `ISecurityModel`: initialization and enforcement explicitly reject custom implementations with `NotSupportedException`, directing callers to `SimulatedSecurityModel`; no silent security bypass.

### Ownership and privilege contracts

- [x] Validate creating on behalf of another owner and reassignment to an existing user/owner team with table Read. Unsupported team/principal types and independent owning-BU changes fail explicitly.
- [x] Clear stale `owninguser`/`owningteam` and derive `owningbusinessunit` across Create, Assign, Update/UpdateRequest, and Upsert. Failed validation leaves stored rows unchanged; transaction rollback restores ownership and audits.
- [x] Use ownership metadata: organization-owned tables omit owner defaults, use table permissions without record-owner depth checks, and reject assignment/sharing and ownership-column mutation.
- [x] Document that the common `AccessDenied` code/simulator-generated message is an approximation rather than an exact denied-operation envelope. Capturing operation-specific live faults remains P2 evidence work below.

The delivered P1 items above do not close the CRUD-reference gap in the close-out review. Use the P2 fixtures below to extend evidence. General relationship direction/cascade completion belongs to P4, while demonstrated misleading authorization on a supported path should be fixed early.

## P1 implementation and verification - 23 October 2026

`RecordOwnership` is the shared ownership path. Create checks table Create and proposed-owner Read; Create is not subjected to post-create record access checks. Assign and changed `ownerid` updates require caller Assign, Write, and Read on the existing row, plus proposed-owner table Read. User/team validation occurs before storage mutation. Assignment also records an `Assign` audit. `owninguser`/`owningteam` are derived fields and cannot be written directly. With metadata absent, tables retain the default user/team ownership assumption.

The supported business-unit contract is traditional ownership: a known owner's BU determines `owningbusinessunit`. A principal row without a BU leaves that field unset. Setting a different owning BU or changing it independently throws `NotSupportedException`; modernized business units, cross-BU role anchoring, assignment cascades, group-team owners, and `AssertOwnershipAppendToAccess` are not simulated. Read scope uses an existing row's `owningbusinessunit` when supplied, while ownership/team membership continues to grant access at any nonzero privilege depth.

Factory `null` uses a simulator-local SYSTEM principal id `00000000-0000-0000-0000-000000000001` and an explicit per-service bypass flag; knowing that id does not confer elevation. It is not a claim about a live environment's SYSTEM GUID. SYSTEM bypasses privilege enforcement, not structural metadata/ownership validation. `Guid.Empty` uses the provider/plugin user, and explicit ids must identify a seeded systemuser. Default ownership, `createdby`, `modifiedby`, and permissions use the effective service caller; `InitiatingUserId` can be supplied separately and is preserved. **`WhoAmI` reports the authenticated connection user and its configured BU/organization, even while impersonating another caller.** The initial implementation incorrectly asserted the effective caller; the restricted-user live follow-up exposed and corrected that assumption in the fixture, simulator, and regressions. Custom-workflow factory semantics and automatic plugin execution remain outside this contract.

Evidence:

- `SecurityIdentityRegressionTests`: **63 new cases**, covering readable inner/outer/FetchXML/nested/self links, shares/teams, aggregate/count/top/page effects, isolated identities, SYSTEM, unsupported custom models, owning-field transitions, denial atomicity, invalid assignees, organization-owned CRUD, derived-field rejection, transaction rollback, and connection identity versus effective caller for explicit/empty/SYSTEM factory services.
- Full unit run after the impersonation correction: **669 passed, 0 skipped, 0 failed**, versus the **606 passed** pre-change baseline. Command: `dotnet test CloudAwesome.Xrm.Simulate.Test/CloudAwesome.Xrm.Simulate.Test.csproj --no-restore`.
- Four-project Release solution build succeeded. Existing compiler/nullability warnings remain; this is not a warning-free-build claim.
- Initial live `SecurityIdentity` run: **7 passed, 5 skipped, 0 failed**. Seven ownership cases compare user/team/user fields through Assign, Update, and Upsert; missing-assignee fault codes and unchanged owners for those three paths; and organization-owned currency Retrieve/RetrieveMultiple. Transient teams reuse the caller's roles and are deleted with the account fixtures. No existing user or role assignments are changed.
- The initial five restricted-caller cases skipped for missing user configuration. The configured follow-up passed **all five** QueryExpression/FetchXML inner/outer/count and aggregate cases. Each verifies the clone retains `CallerId`, `WhoAmI` still identifies the connection, the restricted user's account is retrievable, and the connecting user's account is hidden. This proves effective read impersonation without relying on `WhoAmI` as the impersonated identity. No live plugin-host null/empty factory test was run.
- Final combined follow-up: **12 passed, 0 skipped, 0 failed** using `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-restore --filter "TestCategory=SecurityIdentity"` (Debug). The existing restricted-user id was supplied only to that test process; credentials, roles, and persistent environment settings were unchanged. Rider MCP could not resolve a runnable NUnit entry point, so reproduction and verification used the native test runner rather than a debugger session.

Live command: `dotnet test CloudAwesome.Xrm.Simulate.Integration/CloudAwesome.Xrm.Simulate.Integration.csproj --no-build --no-restore --configuration Release --filter "TestCategory=SecurityIdentity"`. It uses the existing `DATAVERSE_APPSETTINGS` JSON-path mechanism. The first sandboxed attempt could not authenticate (`Need a non-empty authority`); the network-enabled run succeeded. No authentication configuration or credentials were changed.

To enable restricted joins, set `DATAVERSE_SECURITY_USER_ID` to an existing sandbox user with Basic account/contact Read and no wider account access through roles, teams, sharing, or hierarchy. The live caller must have impersonation plus fixture CRUD/ownership privileges. The fixture creates only transient accounts/contacts and clones the live client for impersonation. Incorrectly broad user permissions cause assertions to fail; absence of the setting is an explicit skip.

The chosen factory and assignment dependencies follow the [SDK factory contract](https://learn.microsoft.com/en-us/dotnet/api/microsoft.xrm.sdk.iorganizationservicefactory.createorganizationservice?view=dataverse-sdk-latest) and [documented data-operation rights](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/security-access-rights). Exact denied-operation messages/codes and environment-dependent ownership settings remain P2 evidence work.

## P2 - Live parity hardening

The original three security request fault cases and two metadata/security comparisons are now accompanied by the twelve `SecurityIdentity` cases above. All twelve have live pass evidence after configuring the restricted-user fixture and correcting the `WhoAmI` assertion. No broad live depth matrix is established by this result.

Priority cases:

- [ ] None/user/business-unit/parent-child/organization depths across own, sibling, child, and parent business units.
- [ ] Owner-team privilege inheritance and team-owned record access, including role assignment/removal through association requests.
- [ ] Explicit user/team shares with missing/available table privileges, overlapping masks, modification, revocation, and effective-access responses.
- [x] Initial restricted-user Retrieve/inner/outer/count/aggregate visibility, including connection-versus-effective identity: all five configured live cases passed.
- [ ] Extend visibility across business-unit/owner-team/share depths and restricted-caller page boundaries.
- [ ] Create/update/delete/assign/share/append authorization and exact denied faults.
- [x] Initial live ownership/missing-assignee/organization-owned comparisons, plus local factory null/empty/explicit identity regressions.
- [ ] Live plugin-host factory null/empty/explicit comparisons and wider principal/team/ownership settings.
- [ ] Queue-specific requirements and transaction rollback of memberships/shares/assignments.

Use transient role/principal fixtures and customization automation where necessary. Avoid a permanent gold dataset. See [03](03-integration-testing.md) for configuration, cleanup, and CI work.

## P4 - Metadata, relationship, and synchronization completion

- [x] Use loaded ownership metadata in security decisions for the supported user/team and organization ownership contract.
- [ ] Use relationship metadata in append/append-to direction checks.
- [ ] Decide supported cascade/share inheritance rules with live fixtures before implementation.
- [ ] Define whether direct CRUD changes to systemuser/team/businessunit/role/intersect rows update the graph. Setup facades and special role associations synchronize selected state today; arbitrary raw entity mutation should not be advertised as complete security synchronization.
- [ ] Verify business-unit anchoring of contributed team/role privileges and relevant ownership edge cases.
- [ ] Expand validation for unsupported principal/team types and ambiguous configuration.

Prefer one security graph and consistent mutation services to a parallel second set of access rules.

## P5/P6 - Ergonomics and deferred breadth

- [ ] Document existing fluent/XML setup and per-business-unit role rows in user-facing examples (also a P3 release requirement).
- [ ] Consider authenticated-principal/setup helpers and deterministic generated security JSON only when a real scenario requires them.
- [ ] Revisit full access-team templates after the current supported security subset has recorded parity evidence.
- [ ] Field-level security and advanced hierarchy behavior remain explicitly deferred rather than implied by stored metadata flags.
- [ ] Measure repeated graph validation/index construction under P7 before adding caches or complexity.

## Test evidence

Existing unit suites cover the basic matrix that the old roadmap requested:

- `PermissionsCalculatorTests`: direct roles, none/user/BU/deep/global, owner-team contributions, user/team shares, readable filtering.
- `SimulatedSecurityModelTests`: graph validation, privilege merging, default teams, access-team role exclusion.
- `ServiceSecurityEnforcementTests`: CRUD/query/request denials and append/append-to checks.
- `SecurityRequestHandlerTests`: sharing, principal access, team membership, and missing-object faults.
- `SecurityFixtureErgonomicsTests` and `SecurityRoleBusinessUnitReplicationTests`: automatic system rows, role replication and associations.
- `CoreOrganizationRequestTests`: transaction rollback including security changes.

These passed as part of the **560 passed / 4 skipped / 0 failed** unit run on 2026-10-22. Passing unit tests demonstrate the current model, with the P1/P2 qualifications above.

## Evidence anchors

- [Typed calculator](../CloudAwesome.Xrm.Simulate/SecurityModel/PermissionsCalculator.cs)
- [Runtime graph and setup](../CloudAwesome.Xrm.Simulate/SecurityModel/SimulatedSecurityModel.cs)
- [Enforcer and concrete-model boundary](../CloudAwesome.Xrm.Simulate/ServiceRequests/SimulatedSecurityEnforcer.cs)
- [Role assignment associations](../CloudAwesome.Xrm.Simulate/ServiceRequests/RoleAssignmentAssociationHandler.cs)
- [Per-service factory identity](../CloudAwesome.Xrm.Simulate/ServiceProviders/OrganisationServiceFactorySimulator.cs)
- [Shared ownership validation](../CloudAwesome.Xrm.Simulate/ServiceRequests/RecordOwnership.cs)
- [New regression suite](../CloudAwesome.Xrm.Simulate.Test/SecurityModelTests/SecurityIdentityRegressionTests.cs)
- [Live ownership parity](../CloudAwesome.Xrm.Simulate.Integration/SecurityTests/SecurityOwnershipParityTests.cs)
- [Opt-in restricted linked parity](../CloudAwesome.Xrm.Simulate.Integration/SecurityTests/RestrictedLinkedSecurityParityTests.cs)
- [Existing live security fault fixture](../CloudAwesome.Xrm.Simulate.Integration/SecurityTests/SecurityRequestFaultParityTests.cs)
