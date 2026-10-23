# Simulation parity roadmap

Date reviewed: 2026-10-22

## Goal and status convention

The simulator should be Dataverse-compatible enough that an isolated failing test predicts a failing live operation. Implemented behavior must be distinguished from complete platform parity.

The order of work is [P1-P7 in the executive summary](00-executive-summary.md#updated-order-of-work). Checked items below describe delivered implementation, with limitations kept open. Integration fixtures mentioned here exist in source; they were not executed against a live sandbox during this review.

## Correctness foundations delivered

- [x] Partial-column `Retrieve` selects the requested id and projects a result.
- [x] `Update` merges sparse attributes, preserves omitted attributes, sets `modifiedon`/`modifiedby`, invokes the update processor, and audits.
- [x] Missing records and duplicate create ids produce Dataverse-shaped faults.
- [x] `Disassociate` removes stored relationship state.
- [x] Association state is held separately from `Entity.RelatedEntities`.
- [x] Unsupported `Execute` requests throw a descriptive `NotSupportedException` identifying request type/name and the custom-handler extension point.
- [x] Service-provider state is isolated per simulated instance, including logs, telemetry, service bus, data, and audits.
- [x] Regression suites cover the above and direct-method/request equivalents.

Evidence: `ServiceRequests/EntityCreator.cs`, `EntityRetriever.cs`, `EntityUpdater.cs`, `EntityAssociator.cs`, `EntityDisassociator.cs`, `DataverseServiceFaults.cs`, `RequestHandlerRegistry.cs`, and the corresponding unit fixtures. Paths in this document are relative to the library project unless a project is named.

Do not equate these fixes with complete CRUD parity. Relationship/cascade behavior, ownership, full metadata validation, concurrency, and audit coverage still need work.

## OrganizationRequest support matrix

`OrganisationServiceSimulator.RegisterServiceRequests()` now registers **29** built-in handlers:

| Area | Implemented requests | Remaining qualification |
| --- | --- | --- |
| Direct equivalents | `CreateRequest`, `RetrieveRequest`, `RetrieveMultipleRequest`, `UpdateRequest`, `DeleteRequest`, `AssociateRequest`, `DisassociateRequest` | Shared operation paths exist; auditing and relationship semantics are not uniformly complete. |
| Identity and ownership | Per-service caller identity, connection identity from `WhoAmIRequest`, shared ownership validation for Create/Assign/Update/Upsert | Valid user/owner teams and owning fields are covered; cascades, modernized BU settings, and broad denied-operation parity remain open. |
| Upsert and state | `UpsertRequest`, `SetStateRequest` | Upsert matches id or supplied `KeyAttributes`; it does not enforce declared metadata keys/uniqueness. State/status validation uses metadata when loaded. |
| Batches | `ExecuteMultipleRequest`, `ExecuteTransactionRequest` | Continue-on-error/response handling and entity/relationship/audit/security rollback exist. Transaction fault envelopes and faulted request indexes need live comparison. |
| Sharing | `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`, `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest` | Table/record checks and explicit share state exist; exact privilege/fault semantics need broader live fixtures. |
| Teams | `AddMembersTeamRequest`, `RemoveMembersTeamRequest` | Membership affects owner-team privileges. Full access-team template behavior remains deferred. |
| Queues | `AddToQueueRequest`, `RemoveFromQueueRequest`, `PickFromQueueRequest`, `ReleaseToQueueRequest`, `RouteToRequest` | Typed responses and shared services exist; no dedicated live queue fixture suite yet. See [07](07-queue-simulation.md). |
| Metadata | `RetrieveEntityRequest`, `RetrieveAttributeRequest`, `RetrieveAllEntitiesRequest`, `RetrieveRelationshipRequest` | Requires loaded metadata. Selected SDK fields are projected; global option-set retrieval is absent. See [05](05-metadata-simulation.md). |

- [x] Direct request equivalents, sharing/team requests, queues, and the four metadata retrieval requests are implemented.
- [x] First implementations of upsert, set-state, multiple execution, and transaction execution are implemented.
- [ ] Harden declared alternate keys and direct retrieve/update/delete by key where required (P4).
- [ ] Add `RetrieveOptionSetRequest` and `UpsertMultipleRequest` only as supported consumer scenarios justify them (P6).
- [ ] Activity helpers such as send/close/cancel, rollup calculation, initialization-from, duplicate detection, and other environment/user helpers remain optional backlog (P6).

Custom typed handlers are available through `.Simulated().CustomOrgRequests().Add<TRequest>()`. They cannot replace an already registered handler through this public facade. Failure settings select organization request names and optionally record ids on supported paths. A default unknown handler, request-name dispatch for generic custom APIs, and broad built-in replacement are not implemented; keep these as explicit API decisions rather than implied guarantees.

## Query support matrix

| Surface | Delivered | Still open |
| --- | --- | --- |
| Query forms | `QueryExpression`, converted FetchXML, and converted `QueryByAttribute` share execution | FetchXML-specific validation and richer syntax. |
| Filtering/projection/order | Full base rows filtered before projection; ordering before top; projected distinct; cloned query rows | Empty column sets, exact primary-id/distinct output, nested/OR FetchXML filters, alias-reference edge cases. |
| Links | Inner/left outer joins, `AliasedValue`, selected linked columns, unselected join/filter columns, focused immutability tests | Linked-row security; ordering by unprojected linked columns; nested alias behavior; advanced joins. |
| Paging | Count/page slicing, `MoreRecords`, generated cookie, capped total count across all three forms | Incoming cookie consumption, maximum page size, stable ordering, invalid option combinations, live cookie semantics. |
| Aggregation | Sum/average/min/max/count/countcolumn, distinct countcolumn, grouping/date/fiscal settings, default limit fault | SDK value wrappers/types, empty inputs, synthetic ids, explicit limit semantics, timezone and live aggregate fixtures. |

The old broad query rewrite has been substantially delivered. The remaining work is a focused correctness pass (P1), followed by evidence-led expansion (P6), not restarting the query engine. See [08](08-query-execution-phase-review.md) for source-level findings.

### Operator backlog

- [ ] `In` and `NotIn` (first P6 query additions).
- [ ] Current user/team/business-unit and hierarchy operators.
- [ ] Fiscal period/year conditions (aggregate fiscal grouping already exists).
- [ ] Multi-select contains-values operators and masks.
- [ ] `Exists`, `In`, `MatchFirstRowUsingCrossApply`, and any/all joins.
- [ ] Broader SDK type and null semantics across operators.

Unsupported `QueryExpression` condition operators currently throw `NotImplementedException`; unsupported link operators throw `NotSupportedException`. FetchXML unknown operators currently silently become `Equal`, making rejection/validation a P1 correctness item.

## Security and ownership

- [x] Security is optional and now applies to CRUD, base query visibility, associations, assignment, sharing, team requests, and queues.
- [x] Runtime users, business units, roles, owner teams, memberships, shares, and privilege depths exist.
- [x] Security fixture setup seeds normal system-table rows, including per-business-unit role records and relevant relationship rows.
- [x] Apply read security to linked rows before joins/aggregation/paging, with 23 October unit regressions (P1).
- [x] Honor the factory principal with per-service identities and defined null/empty behavior; live plugin-host parity remains open (P1/P2).
- [x] Validate create/reassignment ownership and metadata-driven organization-owned tables in the supported contract (P1).
- [ ] Align association append direction with metadata; cascades and modernized BU ownership remain deferred (P4).
- [x] Extend live security fixtures: seven new ownership/fault cases and five restricted-caller link cases passed live (P2). The restricted follow-up corrected `WhoAmI` connection identity; broader depths remain open.
- [ ] Field-level security and full access-team templates remain later, explicit features (P6).

See [06](06-security-model.md). Basic access-team representation and explicit team shares do not mean full access-team workflows are complete.

## Metadata and relationship behavior

- [x] Versioned optional metadata, create/update checks/defaults, selected-column/query validation, and four retrieval handlers exist.
- [ ] Validate converted FetchXML through the metadata path (P1).
- [ ] Validate general association schemas/roles and update lookup/intersect rows using relationship metadata (P4).
- [ ] Add ownership-aware defaults, declared-key uniqueness, strict value types, and validation of merged state/status updates (P4).
- [ ] Add selected cascade rules, formatted-value generation, row versions, and optimistic concurrency where tests need them (P4).

A relationship store is already present. It does not yet make ordinary associations visible as lookup/intersect changes to queries; special security role associations have additional wiring.

## Isolated plugin/service-provider behavior

- [x] Mocks exist for execution context, tracing, organization service factory, telemetry logger, and endpoint notifications.
- [x] Stage, depth, parameters, shared variables, and pre/post images can be supplied and consumed; factory services preserve provider data/audit state.
- [ ] Complete exposed context wiring for mode/isolation, initiating-user distinctions, parent context, correlation/request identifiers, and transaction flags where required (P5).
- [ ] Add convenient, deterministic fixtures for these values and verify telemetry/notification return contracts (P5).
- [x] Fix factory identity handling with isolated shared-store views; full live impersonation parity remains qualified by [06](06-security-model.md).

**Scope decision retained:** test one plugin/code unit with a supplied execution context. Automatic registration of plugin steps and cascading execution of other plugins are unsupported by design. Transaction context fixtures do not require building an event pipeline.

## Exit criteria for the next parity milestone

Close or document each P1 gap, add targeted unit regressions and live fixtures, and publish a feature matrix that distinguishes implemented behavior from recorded live verification. Preserve useful exactness checks for fault type/code/message, response names/keys/types, entity projection, and side effects. The existing 560 passing tests are the baseline, not evidence that every table or SDK request is covered.
