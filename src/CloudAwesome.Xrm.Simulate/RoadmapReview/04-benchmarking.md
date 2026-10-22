# Benchmarking roadmap

Date reviewed: 2026-10-22

## Current position and priority

Benchmarking remains **P7**, after correctness, repeatable live parity, and release readiness in the [shared roadmap](00-executive-summary.md#updated-order-of-work).

The benchmark project still contains only `Console.WriteLine("Hello, World!")`. Its project file has no BenchmarkDotNet package or library project reference. No benchmark implementation, performance baseline, or CI performance gate is present. The project building successfully is not a benchmark result.

## First benchmark milestone

- [ ] Reference the simulator and add BenchmarkDotNet.
- [ ] Use deterministic fixtures, a fixed clock, and explicitly named metadata/security configurations.
- [ ] Measure runtime and allocations for supported behavior, recording SDK/library/runtime versions and machine details.
- [ ] Separate simulator initialization/seeding cost from operation cost, with an additional end-to-end unit-test scenario.
- [ ] Store reproducible baseline reports as artifacts before setting regression thresholds.

## Recommended scenario order

| Group | Scenarios | Current dependency status |
| --- | --- | --- |
| Basic test workload | Initialize simulator; create; retrieve by id; sparse update; delete | Implemented; suitable first baseline. |
| Queries | Filter and projection; date conditions; inner/outer links; distinct; ordered multi-page retrieval | Implemented subset; use the corrected P1 contracts. |
| FetchXML and aggregates | Parse/execute; distinct count; date/fiscal grouping; aggregate-limit boundary | Implemented, with output/limit parity still to harden. |
| Security | Base and linked readable-row filtering, owner teams, shares, depth checks | Base filtering exists; linked filtering is P1 work. |
| Organization requests | ExecuteMultiple, successful transaction and rollback | Already implemented; remove the old dependency on first implementing ExecuteMultiple. |
| Queues | Typed seeding; add/pick/release/route/remove | Already implemented; baseline after parity hardening. |
| Isolated plugin unit | Context/provider initialization and execution of one plugin/code unit | Existing mocks support this; automatic plugin pipelines remain out of scope. |

Avoid comparing unlike contracts: metadata/security-enabled simulation does more work than permissive simulation and should have separate results.

## Dataset sizes

Use 10, 100, 5,000, and 50,000 rows as initial scales. Add boundary fixtures such as 5,001 and 50,001 only where page/count/aggregate-limit behavior is being measured. The normal unit-test workload should remain the main reference, with larger data used to expose scaling costs.

## Optimization candidates after measurement

- Id and declared alternate-key indexes in the current dictionary/list store.
- Join lookup construction and security evaluation/model validation costs.
- Relationship lookup efficiency and transaction snapshot cost.
- Clone/projection allocations and full-result materialization before paging.
- Repeated FetchXML parsing where callers reuse identical queries.

Query results already clone top-level entities to avoid the previously tested source-mutation bugs. Benchmark that implementation before changing its isolation contract. Do not sacrifice fidelity or remove useful validation merely to improve a throughput number.

## CI and comparisons

- [ ] Run full benchmarks manually or on a controlled schedule, not on every PR.
- [ ] Retain reports and compare like-for-like releases on comparable hosts.
- [ ] Introduce a small performance smoke check only after a stable baseline makes its threshold meaningful.
- [ ] If comparing alternative libraries, match supported behavior, versions, frameworks, datasets, and configuration, and publish reproducible commands.

Performance results should distinguish operation speed, allocation cost, initialization cost, and supported fidelity. No comparative performance claims are justified by the current empty project.

## Evidence anchors

- [Benchmark entry point](../CloudAwesome.Xrm.Simulate.BenchMark/Program.cs)
- [Benchmark project configuration](../CloudAwesome.Xrm.Simulate.BenchMark/CloudAwesome.Xrm.Simulate.BenchMark.csproj)
