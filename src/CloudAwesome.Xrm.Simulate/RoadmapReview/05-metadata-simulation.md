# Metadata simulation roadmap

Date reviewed: 2026-10-22

## Current position

Optional metadata is implemented and is no longer a prerequisite waiting to be built. The remaining work is validation consistency, fuller metadata consumption, and evidence for the external contract.

- [x] `SimulatorOptions.Metadata` and `.Simulated().Metadata().Load(path)` are available.
- [x] `SimulatedMetadata.Load` accepts contract **1.0**, inline entities, or an index containing relative `entityFiles` paths.
- [x] Runtime entity/attribute/state/status/key/relationship models and case-insensitive lookup indexes exist, separately from JSON DTOs.
- [x] Create/update validation covers selected required fields, known attributes, create/update validity, lengths, numeric range/precision, lookup target types, choices/multiselect values, and supplied state/status pairs.
- [x] Create defaults state/status and uses the configured primary-id attribute.
- [x] Retrieve, QueryExpression, and QueryByAttribute have metadata validation paths.
- [x] `RetrieveEntityRequest`, `RetrieveAttributeRequest`, `RetrieveAllEntitiesRequest`, and `RetrieveRelationshipRequest` return SDK metadata objects from the loaded model.
- [x] Queue-enabled capability `IsValidForQueue` is loaded and consumed by queue setup/operations.
- [x] No metadata preserves permissive operation behavior; metadata retrieval requests explicitly require a loaded model.

`MetadataSimulationTests` and `QueueSimulationTests` cover these first implementations. Selected metadata/security integration fixtures also exist; they were not run live in this review.

## Contract and generator status

The checked-in [metadata index](../CloudAwesome.Xrm.Simulate.Test/TestMetadata/entity-metadata.json) identifies `CloudAwesome.Dataverse.Cli` publisher version `0.0.0.8` and references account/contact/lead entity files. A loading regression consumes that split layout. This is evidence of consumer compatibility with the fixture, not a current audit of the separate CLI implementation or published schemas.

Retained external dependency:

- Repository: <https://github.com/cloud-awesome/dataverse-customisation>
- Package: <https://www.nuget.org/packages/CloudAwesome.Dataverse.Cli>
- Companion command contract: `dvcli document generate-metadata --manifest ./dataverse-metadata.manifest.json`

The CLI repository, current package, and schema endpoints were not reviewed here. Do not mark the whole generator roadmap complete or re-implement it in this solution based on stale assumptions.

### Shared contract requirements retained

- Deterministic entity/attribute/option/key/relationship ordering, reviewable diffs, and explicit `contractVersion`.
- Environment-neutral output by default, with only optional source/generator identity and generation timestamp.
- Table allowlisting as the normal path; whole-environment export must be explicit.
- Combined-document and index-plus-per-entity output, with consistent relative paths and versions.
- Manifest controls for related/intersect entities, attributes, relationships, keys, option labels, and unpublished metadata.
- Clear failures for invalid manifests, missing selected entities, unrepresentable metadata, and unsupported versions/types.
- Offline schema availability, examples, and generation-time validation.

The sample [generation manifest](../CloudAwesome.Xrm.Simulate.Test/TestMetadata/generate-metadata-manifest.json) retains these planned input controls. The metadata schema URI is `https://schemas.cloudawesome.dev/dataverse-simulate/metadata/v1/dataverse-simulate.metadata.schema.json`; the manifest schema URI has the same prefix and ends in `dataverse-simulate.metadata-manifest.schema.json`. Their publication/reachability was not verified.

Potential CLI overrides remain output/environment/connection selection, repeated entity selection, split output, validate-only, and fail-on-unsupported behavior. Confirm the actual external implementation before finalizing or documenting flags.

### Consumer contract verification still needed

- [ ] P3/P4: confirm current generator and consumer round-trip compatibility, deterministic output, and contract version policy.
- [ ] P3/P4: confirm schema publication/offline distribution and document the supported subset. No bundled JSON Schema or schema validator was found in this solution.
- [ ] P4: test both inline and split outputs, unsupported/missing versions, malformed JSON, missing files, and conflicting index/entity logical names comprehensively.
- [ ] P4: reject duplicate/missing metadata with consistent `SimulatedMetadataException` paths. Some runtime dictionaries are constructed before semantic duplicate checks and can leak generic dictionary errors.

The loader already rejects missing/unsupported contract versions, empty documents, missing files, and certain inconsistent primary/state/key/relationship definitions. This is code validation of a subset, not complete JSON Schema validation.

## Metadata actually consumed

| Area | Implemented behavior | Limit still open |
| --- | --- | --- |
| Entity model | Logical/schema/collection names, primary id/name, ownership, activity/intersect, valid messages, queue capability | Many properties are retained but do not yet drive every operation. |
| Attribute model | Names/types, required/read/create/update flags, secured flag, lookup targets, limits, precision, options, integer defaults | Strict SDK runtime type checks, datetime behavior, full boolean/choice defaults, field-level security. |
| Create | Known entity/attributes, configured create validity, selected required fields/value rules, primary id and state/status defaults | Ownership-aware system defaults, related-entity creation, full required-level parity. |
| Update | Attribute/value validity and state/status pair when both supplied | Validate effective merged state/status for partial updates; key-based direct operations and ownership transitions. |
| Retrieve/queries | Known/readable selected columns, converted FetchXML and QueryExpression filter/order/link/aggregate source attributes, QueryByAttribute attributes, aggregate order aliases | Complete all-columns/readability and richer alias/link validation still need review. |
| Paging/aggregation | Metadata-backed primary-id cookie keys and numeric type hints for empty-table aggregates | Elastic-table limits, full type/aggregate validity, timezone/fiscal and mixed-currency behavior. |
| Relationships | Shape stored and projected to SDK metadata; query link column existence checked | General associate/disassociate schema/direction rules and lookup/intersect mutations are not implemented. |
| Alternate keys | Definitions load; referenced attributes checked; Upsert validates declared column sets/status when matching existing rows and recognizes primary-id keys | Global Create/Update uniqueness, platform collation, richer key lookup resolution, and direct key-based CRUD. |
| Metadata requests | Four retrieval handlers and selected SDK properties | Full SDK property/filter/identifier behavior, richer options/labels, global `RetrieveOptionSetRequest`. |

Important distinctions:

- `IsSecured` is metadata storage/projection, not field-level access enforcement.
- `OwnershipType` now drives organization-owned defaults/access and shared user/team ownership validation in the [23 October security pass](06-security-model.md#p1-implementation-and-verification---23-october-2026). Modernized BU ownership/cascades and general `ValidMessages` capability validation remain incomplete.
- Lookup validation currently checks allowed logical targets, not general referenced-row existence.
- Query links check referenced column existence; that does not prove relationship schema validation.
- The required-create rule currently selects create-valid `ApplicationRequired` attributes. Capture real SDK behavior for required levels before treating that rule as exact platform parity.

## Remaining work in roadmap order

### P1 - Validation consistency

- [ ] Apply metadata validation to converted FetchXML before execution, with alias/aggregate-aware rules where necessary.
- [ ] Resolve empty column-set and primary-id output semantics across retrieval/query paths; do not infer a primary key from every `id`-suffixed matching GUID.
- [ ] Capture and test ownership/state/aggregate response cases that currently give misleading fidelity guarantees.

### P2 - Live validation fixtures

- [ ] Exact faults/defaults for unknown entities/columns, required levels, invalid values/types, lookups, state/status, and ownership.
- [ ] Compare metadata SDK property shapes, `EntityFilters`, missing names/identifiers, and option metadata beyond the existing narrow snapshots.
- [ ] Keep metadata retrieval separate from record-read security; the current tests intentionally allow retrieval without simulated table-read privileges.

### P4 - Deeper model consumption

- [ ] Strict value-type handling rather than silently skipping a rule when the value has an unexpected CLR type.
- [ ] Ownership-specific defaults and valid-message enforcement where supported by the contract.
- [ ] Primary-id/readability rules, formatted values, lookup names, and supported datetime behavior.
- [x] Validate matching Upsert key column sets/status using loaded metadata, with composite/partial/inactive-key regressions and live undeclared-key rejection (P1). See [the mutation contract](01-simulation-parity.md#p1-mutationresponse-implementation---23-october-2026); unmatched values follow the verified Create path and metadata remains optional.
- [ ] Enforce alternate-key uniqueness across Create/Update, capture positive live declared-key cases, and extend direct key-based operations.
- [ ] Relationship references checked against loaded tables/attributes; partial allowlists must remain possible.
- [ ] General associate/disassociate schema, endpoint, entity-role, and direction validation.
- [ ] One-to-many lookup mutation and many-to-many intersect rows so ordinary queries see relationship effects.
- [ ] Selected delete/assign/reparent cascade behavior. Cascade configuration is not currently represented by `SimulatedRelationshipMetadata`/DTOs and needs a contract decision first.
- [ ] Row versions/concurrency where a consumer test requires them.

### P6 - Metadata breadth

- [ ] Global option-set retrieval and additional SDK metadata properties when justified by actual code under test.
- [ ] Date/timezone, formatted-label, and specialized metadata behavior in small parity-backed increments.

## Implementation boundaries and tests

Keep metadata optional. Prefer small checked-in generated fixtures to large hand-built object graphs. Expand the existing tests rather than introducing a second loader or duplicate metadata store.

Metadata configuration errors can retain explicit simulator exceptions. Operation failures advertised as Dataverse parity need captured fault contracts; a new strict mode is a design option, not an already implemented setting. The external generator/schema work should be tracked separately from the local consumer checklist.

## Evidence anchors

- [Loader/version checks](../CloudAwesome.Xrm.Simulate/Metadata/SimulatedMetadata.cs)
- [Current validation rules](../CloudAwesome.Xrm.Simulate/Metadata/MetadataValidator.cs)
- [SDK metadata projection](../CloudAwesome.Xrm.Simulate/Metadata/SdkMetadataProjection.cs)
- [Metadata regression fixture](../CloudAwesome.Xrm.Simulate.Test/MetadataSimulationTests.cs)
- [Integration metadata/security fixture](../CloudAwesome.Xrm.Simulate.Integration/MetadataTests/MetadataRetrievalSecurityParityTests.cs)
