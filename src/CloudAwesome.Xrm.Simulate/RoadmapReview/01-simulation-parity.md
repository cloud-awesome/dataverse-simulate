# Simulation parity roadmap

Date reviewed: 2026-10-28; implementation history: 2026-10-22/23/28

## Goal and status convention

The simulator should be Dataverse-compatible enough that an isolated failing test predicts a failing live operation. Implemented behavior must be distinguished from complete platform parity.

The order of work is [P1-P7 in the executive summary](00-executive-summary.md#updated-order-of-work). Checked items describe delivered implementation, with limitations kept open. The original 22 October review used source/build evidence; dated P1 follow-ups record the subsequent live runs.

The [28 October close-out implementation](09-p1-closeout-review.md) closes the three final P1 items: ordinary CRUD object isolation, unselected linked ordering, and silent qualified-input misexecution. Qualified filters/orders and column comparisons now fail explicitly; their execution remains P2/P6 work.

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

Do not equate these fixes with complete CRUD parity. Relationship/cascade behavior, full metadata validation, concurrency, and broader operation-specific security still need work. The 23 October mutation pass unifies audit coverage for the direct/request equivalents below.

## OrganizationRequest support matrix

`OrganisationServiceSimulator.RegisterServiceRequests()` now registers **29** built-in handlers:

| Area | Implemented requests | Remaining qualification |
| --- | --- | --- |
| Direct equivalents | `CreateRequest`, `RetrieveRequest`, `RetrieveMultipleRequest`, `UpdateRequest`, `DeleteRequest`, `AssociateRequest`, `DisassociateRequest` | Shared success-only audit paths and detached Create/Update inputs/Retrieve outputs, including supported mutable SDK values. Full relationship semantics remain P4. |
| Identity and ownership | Per-service caller identity, connection identity from `WhoAmIRequest`, shared ownership validation for Create/Assign/Update/Upsert | Valid user/owner teams and owning fields are covered; cascades, modernized BU settings, and broad denied-operation parity remain open. |
| Upsert and state | `UpsertRequest`, `SetStateRequest` | Upsert honors primary-id precedence, copies create keys, removes identifying key columns on update, validates matching declared keys when metadata is loaded, and preserves request input. Global alternate-key uniqueness and direct key-based CRUD remain P4. |
| Batches | `ExecuteMultipleRequest`, `ExecuteTransactionRequest` | Batch settings/partial commit, transaction response defaults/order, rollback, fault type/code/message/index, and nested-batch rejection now have live comparisons. External side effects from custom handlers are outside snapshot rollback. |
| Sharing | `GrantAccessRequest`, `ModifyAccessRequest`, `RevokeAccessRequest`, `RetrievePrincipalAccessRequest`, `RetrieveSharedPrincipalsAndAccessRequest` | Table/record checks and explicit share state exist; exact privilege/fault semantics need broader live fixtures. |
| Teams | `AddMembersTeamRequest`, `RemoveMembersTeamRequest` | Membership affects owner-team privileges. Full access-team template behavior remains deferred. |
| Queues | `AddToQueueRequest`, `RemoveFromQueueRequest`, `PickFromQueueRequest`, `ReleaseToQueueRequest`, `RouteToRequest` | Shared services, validation before mutation, stable move identity, and a dedicated live fixture now exist. Full property/privilege validation and received-email special cases remain scoped work. See [07](07-queue-simulation.md). |
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
| Filtering/projection/order | Full rows filtered before projection; ordering before top; distinct/primary-id/empty-column contracts; nested FetchXML filters; cloned SDK query values; qualified inputs/column comparisons explicitly rejected | Qualified/post-join execution, full alias support and platform collation remain deeper work. |
| Links | Inner/left outer joins, readable linked rows, `AliasedValue`, selected linked columns, unselected join/filter/order columns, hidden-sort removal before distinct/output, immutability tests | Wider nested alias projection and advanced joins remain scoped follow-up. |
| Paging | Standard-table caps, value-based base-query cookies, final-page metadata, exact selected faults, all three query forms, live continuation after result-set changes | Linked/distinct/grouped cookies; elastic-table limits; remaining invalid-cookie/option envelopes. |
| Aggregation | Typed SDK aliases with empty ids; verified numeric/empty/grouped output; distinct countcolumn; default limit fault; live explicit-limit/invalid-limit contract | Timezone/fiscal behavior, mixed-currency conversion, complete group/linked aggregation, BigInt live evidence. |

The scoped P1 query correctness work is delivered. The remaining work is repeatable/deeper evidence (P2) and evidence-led expansion (P6). See [08](08-query-execution-phase-review.md) for supported contracts and limitations.

### Operator backlog

- [ ] `In` and `NotIn` (first P6 query additions).
- [ ] Current user/team/business-unit and hierarchy operators.
- [ ] Fiscal period/year conditions (aggregate fiscal grouping already exists).
- [ ] Multi-select contains-values operators and masks.
- [ ] `Exists`, `In`, `MatchFirstRowUsingCrossApply`, and any/all joins.
- [ ] Broader SDK type and null semantics across operators.

Unsupported `QueryExpression` condition operators currently throw `NotImplementedException`; unsupported link operators throw `NotSupportedException`. The first P1 pass added explicit rejection of unknown FetchXML operators and unsupported input syntax.

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
- [x] Validate converted FetchXML through the metadata path (first P1 pass; see [08](08-query-execution-phase-review.md#p1-query-inputoutput-implementation---22-october-2026)).
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

The three [P1 findings](09-p1-closeout-review.md) now have corrected-contract unit regressions and focused live fixtures. The Release unit suite passes 1,043 cases (130 additions). Preserve checks for fault type/code/message, response names/keys/types, projection and side effects as P2 expands repeatable parity and P3 prepares the release/support matrix.

## P1 mutation/response implementation - 23 October 2026

This records the delivered 23 October scope. The 26 October review identified ordinary CRUD isolation as an additional P1 gate; the [28 October follow-up](09-p1-closeout-review.md) closes it while preserving the corrections below.

- [x] Direct calls and request wrappers share success-only audit behavior for Create, Retrieve, RetrieveMultiple, Update, Delete, Associate, and Disassociate. Query audits contain the base table and a null record id. Upsert produces one `Upsert` entry; its internal Create/Update still runs the relevant processor but does not add another audit entry. These are simulator diagnostics, not a simulation of Dataverse audit-table retention or event history.
- [x] Assign retains the shared ownership validation/defaults delivered in the security pass, with one successful Assign audit. Those regression and live ownership fixtures remain part of verification.
- [x] Upsert operates on a clone, preserves the caller's id/attributes/keys, and returns a fresh id-only `EntityReference`. Id takes precedence over keys; a single primary-id key is resolved from stored entity identity. The update path removes identifying key columns; the create path copies missing key values while preserving explicit body values. This follows the [documented standard-table algorithm](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/use-upsert-insert-update-record).
- [x] Loaded metadata checks that a matching alternate-key column set is declared and active (unspecified status remains accepted), including composite keys. The live sandbox rejects a matching undeclared key with `-2147088247`, but permits unmatched values to flow to Create; the simulator retains that distinction. Without metadata, existing permissive key matching remains available. Ambiguous seeded matches fault instead of selecting an arbitrary row.
- [x] Transactions restore entity, relationship, audit, and concrete security snapshots, then return `ExecuteTransactionFault` as the detail of `FaultException<OrganizationServiceFault>`, preserving the underlying code/message/details and adding the zero-based request index. Non-service exceptions still roll back and retain their original type. Responses default to enabled when omitted; explicit false returns an empty collection. Nested ExecuteMultiple/ExecuteTransaction requests are rejected before any mutation, following the [transaction contract](https://learn.microsoft.com/en-us/power-apps/developer/data-platform/org-service/use-executetransaction).
- [x] ExecuteMultiple live comparisons cover all ContinueOnError/ReturnResponses combinations, response keys/indexes, fault entries even when successes are suppressed, and committed mutations before/after failure.
- [x] Queue source selection and validation finish before a cloned candidate is written. Ordinary task moves preserve item identity, including implicit moves, repeated adds, and same-queue moves. Queue facade/request failures share the same injection and audit path. See [queue contracts and remaining limits](07-queue-simulation.md#p1p2-implementation-and-verification---23-october-2026).

Verification: **123 new regression cases; 913 unit tests passed, 0 skipped** in Release. The four-project Release build succeeded with **28 existing warnings, 0 errors**. **47 new live parity cases** cover response/transaction/upsert contracts (17) and queues (30). Full-run results are recorded in [03](03-integration-testing.md).

Remaining P2/P4 work: positive live comparisons against a provisioned active alternate key, alternate-key uniqueness across ordinary Create/Update, platform string collation and key lookup resolution, direct retrieve/update/delete by key, full queue metadata/privilege enforcement, received-email multi-queue behavior, team routing, and broader platform fault envelopes. No table keys or security roles were provisioned by this pass.
