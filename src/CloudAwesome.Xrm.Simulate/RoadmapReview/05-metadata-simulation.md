# Metadata simulation roadmap

## 1. Summary, use case, and shared contracts

### Use case

`CloudAwesome.Xrm.Simulate` needs optional Dataverse metadata so simulated CRUD and query operations can enforce platform-compatible behavior without every test manually arranging low-level table and column rules.

The first supported source should be a generated JSON metadata document produced by `CloudAwesome.Dataverse.Cli`:

- GitHub: `https://github.com/cloud-awesome/dataverse-customisation`
- NuGet: `https://www.nuget.org/packages/CloudAwesome.Dataverse.Cli`
- Proposed command: `dvcli document generate-metadata`

The generated document should allow tests to opt into higher-fidelity behavior with a small setup cost:

```csharp
var options = new SimulatorOptions
{
    Metadata = SimulatedMetadata.Load("dataverse-metadata.json")
};
```

The same JSON contract should also be documented and published with a JSON Schema so users can generate compatible metadata from their own pipeline if they do not use `dvcli`.

### Shared contract requirements

The JSON output must be:

- Deterministic, so source-controlled metadata diffs are reviewable.
- Versioned, so `dataverse-simulate` can reject unsupported contract versions clearly.
- Environment-neutral by default, avoiding IDs where logical names or option values are sufficient.
- Small enough for test projects, with table allowlisting as the normal path.
- Complete enough to support create/update validation, defaulting, relationship validation, alternate-key behavior, and query validation over time.

Suggested top-level shape:

```json
{
  "$schema": "https://schemas.cloudawesome.dev/dataverse-simulate/metadata/v1/dataverse-simulate.metadata.schema.json",
  "contractVersion": "1.0",
  "generatedOnUtc": "2026-09-08T00:00:00Z",
  "source": {
    "environmentUrl": "https://example.crm.dynamics.com",
    "publisher": "CloudAwesome.Dataverse.Cli",
    "publisherVersion": "x.y.z"
  },
  "entities": []
}
```

Suggested schema publication:

- `https://schemas.cloudawesome.dev/dataverse-simulate/metadata/v1/dataverse-simulate.metadata.schema.json`
- Include the schema in the CLI package/repo for offline validation.
- Include contract examples for a small account/contact/lead model.

### Metadata required in v1

Entity metadata:

- Logical name.
- Schema name.
- Collection schema/logical name where available.
- Primary ID attribute.
- Primary name attribute.
- Ownership type.
- Is activity.
- Is intersect.
- Valid messages/capabilities where available, such as create/update/delete/associate/disassociate.
- Attributes.
- State/status mapping.
- Alternate keys.
- Relationships.

Attribute metadata:

- Logical name.
- Schema name.
- Attribute type.
- Required level.
- Is primary ID.
- Is primary name.
- Is valid for create.
- Is valid for update.
- Is valid for read.
- Is secured, if available.
- Lookup targets.
- String max length and format.
- Memo max length.
- Integer min/max.
- Decimal, double, and money precision/min/max.
- Date/time behavior and format.
- Boolean labels and values.
- Choice and multi-select option values.
- Default value where Dataverse exposes one.

State/status metadata:

- Valid state values.
- Valid status values.
- Default status per state.
- State/status allowed pairs.

Relationship metadata:

- Schema name.
- Relationship type: one-to-many, many-to-one, many-to-many.
- Referencing entity and attribute.
- Referenced entity and attribute.
- Intersect entity for many-to-many.
- Entity role where relevant.
- Cascade configuration where available.

Alternate key metadata:

- Key name.
- Entity logical name.
- Attribute logical names in key order.
- Key status, if available.

## 2. `dvcli document generate-metadata` implementation

### Command goals

Add a new command to `CloudAwesome.Dataverse.Cli`:

```powershell
dvcli document generate-metadata --manifest .\dataverse-metadata.manifest.json
```

The command should connect to Dataverse, retrieve metadata, and write JSON conforming to the published schema.

### Manifest

The command should accept a manifest file so metadata generation is repeatable in local development and CI.

Suggested manifest shape:

```json
{
  "$schema": "https://schemas.cloudawesome.dev/dataverse-simulate/metadata/v1/dataverse-simulate.metadata-manifest.schema.json",
  "output": {
    "path": "./GeneratedMetadata/dataverse-metadata.json",
    "splitFilesPerEntity": false,
    "includeGeneratedOnUtc": true
  },
  "entities": {
    "include": [
      "account",
      "contact",
      "lead"
    ],
    "includeRelatedEntities": false,
    "includeIntersectEntities": true
  },
  "metadata": {
    "includeAttributes": true,
    "includeRelationships": true,
    "includeAlternateKeys": true,
    "includeOptionLabels": false,
    "includeUnpublished": false
  }
}
```

Manifest requirements:

- `entities.include` should be required unless an explicit `includeAllEntities` switch is set.
- `splitFilesPerEntity` should support:
  - one combined JSON document; or
  - a manifest/index JSON plus one file per entity.
- Entity ordering must be deterministic by logical name.
- Attribute, option, key, and relationship ordering must be deterministic.
- The manifest schema should be published next to the metadata output schema.

### Command options

Suggested options:

- `--manifest <path>`: manifest path.
- `--output <path>`: override manifest output path.
- `--environment <url-or-id>`: optional environment override, matching common Power Platform CLI conventions.
- `--connection <name>`: if the CLI already supports named connections.
- `--include-entity <logicalName>`: repeatable or semicolon-delimited override for quick use.
- `--split-files-per-entity`: command-line override.
- `--validate-only`: validate manifest and connection without writing output.
- `--fail-on-unsupported-metadata`: fail instead of warning when a metadata field cannot be represented.

### Dataverse retrieval

The command should use Dataverse metadata APIs directly, not generated early-bound classes.

Primary SDK messages to evaluate:

- `RetrieveAllEntitiesRequest`
- `RetrieveEntityRequest`
- `RetrieveAttributeRequest`
- `RetrieveRelationshipRequest`
- `RetrieveOptionSetRequest`

The implementation should request only the metadata needed for selected entities where possible.

### Output validation

Generation should validate output against the JSON Schema before writing the final file.

Failure behavior:

- Invalid manifest: fail before connecting.
- Missing entity from allowlist: fail with the logical name.
- Unsupported metadata type: fail or warn based on manifest/command option.
- Unserializable metadata: fail with entity and attribute or relationship path.

### Versioning

The CLI should emit:

- `contractVersion`
- generator name and version
- optional source environment URL

Contract version should change only when `dataverse-simulate` consumption could be affected.

## 3. `dataverse-simulate` implementation consuming JSON output

### Simulator API

Add metadata support to simulator setup:

```csharp
var options = new SimulatorOptions
{
    Metadata = SimulatedMetadata.Load("GeneratedMetadata/dataverse-metadata.json")
};
```

Alternative setup API:

```csharp
_organizationService.Simulated()
    .Metadata()
    .Load("GeneratedMetadata/dataverse-metadata.json");
```

The options path should be the primary path so all tests can share setup through a fixture.

### Internal models

Add simulator-owned metadata models rather than depending directly on SDK metadata serialization:

- `SimulatedMetadata`
- `SimulatedEntityMetadata`
- `SimulatedAttributeMetadata`
- `SimulatedStateMetadata`
- `SimulatedStatusMetadata`
- `SimulatedRelationshipMetadata`
- `SimulatedAlternateKeyMetadata`

Model requirements:

- Preserve enough source data to make validation decisions.
- Provide lookup methods optimized for runtime use, such as `GetEntity(logicalName)` and `GetAttribute(entityName, attributeName)`.
- Keep JSON DTOs separate from runtime indexes if needed.
- Reject unsupported contract versions at load time.
- Validate internal consistency at load time.

### Loading and validation

`SimulatedMetadata.Load(path)` should:

- Read JSON.
- Validate against the bundled JSON Schema or equivalent code validation.
- Validate semantic consistency:
  - every primary ID/name attribute exists;
  - every relationship references known entities and attributes when those entities are present;
  - every alternate key references known attributes;
  - state/status default pairs are valid;
  - attribute logical names are unique per entity.
- Produce clear exceptions with JSON paths or entity/attribute paths.

### Create message consumption

`EntityCreator` should use metadata when available for:

- Duplicate ID validation.
- Unknown entity validation.
- Unknown attribute validation.
- Primary ID attribute handling.
- Required field validation.
- Create-valid attribute validation.
- String and memo max length validation.
- Numeric min/max and precision validation.
- Lookup target validation.
- Choice and multi-select option validation.
- State/status defaulting.
- State/status pair validation.
- Owner defaults based on ownership type.
- Relationship creation from `Entity.RelatedEntities` only when relationship metadata supports it.

Duplicate ID validation should be implemented before the broader metadata work because it does not require metadata.

### Update message consumption

`EntityUpdater` should use metadata when available for:

- Unknown entity validation.
- Unknown attribute validation.
- Update-valid attribute validation.
- String and memo max length validation.
- Numeric min/max and precision validation.
- Lookup target validation.
- Choice and multi-select option validation.
- State/status pair validation.
- Alternate-key resolution when update-by-key is supported.

### Retrieve and query consumption

`EntityRetriever` and retrieve multiple query parsing should use metadata when available for:

- Unknown entity validation.
- Unknown column validation.
- Primary ID projection rules.
- Lookup alias/name attribute behavior where supported.
- Relationship validation for joins.
- Type-aware condition validation.

### Associate and Disassociate consumption

Relationship metadata should eventually drive:

- Relationship name validation.
- Valid target/related entity validation.
- Entity role handling.
- Many-to-many intersect behavior.
- One-to-many lookup mutation behavior where Dataverse does that for the relationship type.
- Cascade effects later, when delete behavior is implemented.

### Execution mode

Metadata-backed validation should be opt-in at first:

- No metadata: preserve current permissive simulator behavior.
- Metadata loaded: enforce known metadata rules.
- Later option: strict mode for Dataverse-shaped exception types and messages.

### Test strategy

Add tests in layers:

- Unit tests for JSON loading and schema/semantic validation.
- Unit tests for `EntityCreator` metadata-backed validation/defaulting.
- Unit tests for `EntityUpdater` metadata-backed validation.
- Unit tests for associate/disassociate metadata validation.
- Integration parity tests for live-observed create/update failures and defaults.

Test fixtures should use small checked-in metadata JSON examples generated from the public schema, not hand-built object graphs in every test.
