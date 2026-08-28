# Integration testing roadmap

## Purpose

The integration project should become the project's parity safety net. Its job is not only to prove that live Dataverse works. Its job is to discover and freeze exact Dataverse behavior so the simulator can match it.

## Current state

The integration project currently has:

- A connection manager that reads a connection string from `DATAVERSE_APPSETTINGS` or `appsettings.local.json`.
- A base fixture that disposes the connection at fixture teardown.
- One initial test that creates, retrieves, compares, and deletes a contact.

This is enough to prove connectivity, but it is not yet a reusable dual-execution framework.

## Target design

### Dual execution harness

Create a test harness that can run the same scenario against:

- Live `IOrganizationService`
- Simulated `IOrganizationService`

Each scenario should define:

- Arrange live data.
- Arrange simulated data.
- Operation under test.
- Cleanup live data.
- Normalization rules.
- Expected comparison.

Example scenario shape:

```csharp
public sealed class DataverseParityScenario<T>
{
    public string Name { get; init; }
    public Func<IOrganizationService, Task> ArrangeLive { get; init; }
    public Action<IOrganizationService> ArrangeSimulated { get; init; }
    public Func<IOrganizationService, T> Act { get; init; }
    public Func<T, T> Normalize { get; init; }
    public Action<T, T> AssertEquivalent { get; init; }
}
```

The exact shape can be simpler, but the important point is that live and simulated execution are first-class parts of one scenario.

### Result capture

Capture both successful responses and exceptions:

- Response type.
- `Results` keys and value types.
- Entity attributes, formatted values, related entities, aliased values, and entity references.
- `EntityCollection` metadata such as `MoreRecords`, `PagingCookie`, `TotalRecordCount`, and `TotalRecordCountLimitExceeded`.
- Exception type.
- Fault error code.
- Message.
- Inner fault details where available.

Store captured behavior as test fixtures or snapshots only where it helps. Avoid large opaque snapshots for common tests; focused assertions are easier to maintain.

### Test data isolation

Live tests need strong cleanup:

- Create unique test data prefixes.
- Track created ids in a cleanup scope.
- Delete in reverse dependency order.
- Use a dedicated solution, publisher prefix, and sandbox environment.
- Avoid relying on existing environment data except system entities that are guaranteed.
- Add a cleanup-only utility for abandoned test records.

### Secrets and configuration

Move away from local-only configuration:

- Support connection string via environment variable.
- Support client id/client secret or certificate authentication through secrets.
- Never commit live environment URLs, usernames, passwords, or client secrets.
- Keep `appsettings.local.json` ignored.
- Add `appsettings.example.json`.

## Priority live parity suites

### Phase 1 - Existing behavior

Build live parity around features already implemented:

- Create, retrieve, update, delete.
- Associate and disassociate.
- RetrieveMultiple with QueryExpression.
- RetrieveMultiple with FetchXML.
- QueryByAttribute.
- Assign.
- WhoAmI.
- CreateRequest and direct Create equivalence.
- RetrieveMultipleRequest and direct RetrieveMultiple equivalence.
- Custom request handler behavior does not need live parity unless used for custom APIs.

### Phase 2 - Exceptions and validation

Capture exact live behavior for:

- Retrieve missing record.
- Update missing record.
- Delete missing record.
- Duplicate id create.
- Unknown entity.
- Unknown attribute in column set.
- Missing required attributes.
- Invalid option set value.
- Invalid state/status pair.
- Invalid lookup target.
- Insufficient privileges.
- Unsupported or malformed FetchXML.
- Query using attributes not present in column set.

### Phase 3 - Query semantics

Create a live dataset with accounts, contacts, teams, users, lookups, money, option sets, dates, nullable columns, and relationships.

Cover:

- All condition operators.
- Link entity inner/outer joins.
- Aliased values.
- Multi-level links.
- Ordering by base and linked attributes.
- Distinct.
- Paging.
- Aggregates.
- FetchXML-specific constructs.

### Phase 4 - Security

Create controlled security fixtures:

- Users in different business units.
- Owner teams and access teams.
- Roles with none/user/business-unit/parent-child/organization depth.
- Sharing and revoking access.
- Records owned by users and teams.

Assertions:

- Retrieve and RetrieveMultiple visibility.
- Write/delete/assign/share success and failure.
- Access rights responses.
- Error messages and fault codes.

## CI integration

Run integration tests in three modes:

- Local developer mode: opt-in, using local config.
- CI manual dispatch: uses repository secrets and a sandbox.
- Scheduled release safety net: runs before publishing or nightly/weekly.

Use categories:

- `Connectivity`
- `ParitySmoke`
- `ParityCore`
- `ParityQuery`
- `ParitySecurity`
- `Destructive`

CI should default to smoke/core only. Full query and security parity can be scheduled because they are slower and more environment-sensitive.

## Microsoft service change safety net

The integration project should be able to detect changes in Microsoft-managed behavior:

- Run a scheduled suite against the sandbox.
- Produce a diff report for changed exception messages, response shapes, and query outputs.
- Mark diffs as expected changes only through an explicit baseline update.
- Use the same diff report to guide simulator changes.

This turns integration tests into a live compatibility monitor, not just a one-time implementation aid.

