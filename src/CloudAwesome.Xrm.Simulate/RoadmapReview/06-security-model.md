# Security model roadmap

Date reviewed: 2026-10-22

## Current position

The first security model and its service wiring are implemented. The old description of a create-only boolean guard is obsolete. The next work is to harden the implemented model and verify real privilege/ownership behavior, not to build another graph/calculator.

Security remains opt-in. No configured model preserves permissive operation behavior. `IgnoreMissingEntities` controls missing table configuration for the concrete model. Source presence and passing local tests are distinguished from full live parity throughout this review.

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
| Create | Table create privilege | Proposed owner/business-unit scope is not fully validated. |
| Retrieve | Record read privilege | Uses configured acting user. |
| RetrieveMultiple | Base rows filtered before query projection/counts/paging/aggregation | Linked rows remain unfiltered. |
| Update/delete | Record write/delete privilege | Ownership and field-level rules are incomplete. |
| Associate/disassociate | Append-to on target, append on related rows | General metadata relationship direction is not consumed. |
| Assign | Record assign privilege | Assignee validation, ownership-field consistency, and cascades remain incomplete. |
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

- [ ] Filter readable linked rows before inner/outer join matching and before aggregates/counts/paging. `EntityMultipleRetriever.ApplyReadableFilter` currently replaces only the base table list; `LinkedEntities` reads unrestricted linked lists.
- [ ] Honor `CreateOrganizationService(userId)` without resetting shared state. `OrganisationServiceFactorySimulator` currently ignores the supplied id and uses the existing authenticated user/options.
- [ ] Define/test supported null/system identity and caller/initiating-user distinctions, preserving the per-service identity when multiple services share a store.
- [ ] Resolve the public `ISecurityModel` contract: `SimulatedSecurityEnforcer.TryGetContext` only recognizes the concrete `SimulatedSecurityModel`. A different configured interface implementation currently bypasses the new enforcement path. Support it, reject it clearly, or narrow the documented API.

### Ownership and privilege contracts

- [ ] Validate creating on behalf of another owner/business unit and reassignment to a valid user/owner team.
- [ ] Clear stale `owninguser`/`owningteam` and maintain owning-business-unit fields on assignment; define behavior for owner changes through update/upsert as well as AssignRequest.
- [ ] Treat organization-owned tables according to metadata. The calculator currently receives no ownership metadata and evaluates records through owner/depth rules, so this is not completed fidelity.
- [ ] Capture operation-specific privilege-denial faults rather than treating the common `AccessDenied` code and simulator-generated message as exact live parity.

Use the P2 fixtures below while closing these gaps. General relationship direction/cascade completion belongs to P4, but misleading authorization on an already supported path should be fixed early.

## P2 - Live parity hardening

Existing live fixture coverage is narrow: three security request tests cover missing target/principal/team faults, and two metadata tests compare selected retrieval behavior. The 40 integration tests were discovered, not executed live during this review. No broad live depth/ownership matrix is currently established by those tests.

Priority cases:

- [ ] None/user/business-unit/parent-child/organization depths across own, sibling, child, and parent business units.
- [ ] Owner-team privilege inheritance and team-owned record access, including role assignment/removal through association requests.
- [ ] Explicit user/team shares with missing/available table privileges, overlapping masks, modification, revocation, and effective-access responses.
- [ ] Retrieve and RetrieveMultiple visibility, including inner/outer links, aggregates, count, and page boundaries.
- [ ] Create/update/delete/assign/share/append authorization and exact denied faults.
- [ ] Per-service caller identities, missing/invalid principals, owner-team versus access-team assignments, and organization-owned tables.
- [ ] Queue-specific requirements and transaction rollback of memberships/shares/assignments.

Use transient role/principal fixtures and customization automation where necessary. Avoid a permanent gold dataset. See [03](03-integration-testing.md) for configuration, cleanup, and CI work.

## P4 - Metadata, relationship, and synchronization completion

- [ ] Use loaded ownership/relationship metadata in security decisions and append/append-to direction checks.
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
- [Factory identity gap](../CloudAwesome.Xrm.Simulate/ServiceProviders/OrganisationServiceFactorySimulator.cs)
- [Existing live security fault fixture](../CloudAwesome.Xrm.Simulate.Integration/SecurityTests/SecurityRequestFaultParityTests.cs)
