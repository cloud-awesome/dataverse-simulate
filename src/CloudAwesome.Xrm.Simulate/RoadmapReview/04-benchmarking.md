# Benchmarking roadmap

## Priority

Benchmarking is useful, but it should remain lower priority than parity and safety net work. The current benchmark project is an empty console application, which is acceptable for now.

## Benchmark goals

The benchmark project should answer practical user questions:

- How fast is the simulator for common plugin unit tests?
- How does query complexity affect runtime?
- How much memory is used for realistic seed sizes?
- Are changes regressing performance?
- How does CloudAwesome.Xrm.Simulate compare with alternatives where comparison is legally and practically acceptable?

## Recommended tooling

Use BenchmarkDotNet once the project is ready.

Suggested benchmark groups:

- Create one record.
- Create many records.
- Retrieve by id.
- Update sparse entity.
- Delete.
- RetrieveMultiple simple filter.
- RetrieveMultiple date filters.
- RetrieveMultiple linked entity.
- FetchXML parse and execute.
- Aggregate queries.
- Security-filtered queries.
- ExecuteMultiple once implemented.
- Plugin pipeline execution once implemented.

## Dataset sizes

Use multiple seeded sizes:

- Tiny: 10 records.
- Typical unit test: 100 records.
- Medium: 5,000 records.
- Large: 50,000 records.

The large size matters because Dataverse page size, aggregate limits, and query behavior often surface at scale.

## Competitor comparisons

Comparison with competitors should be handled carefully:

- Keep comparisons factual and reproducible.
- Compare only equivalent scenarios.
- Include package versions and target frameworks.
- Avoid benchmark claims until parity for that scenario is good enough.
- Separate "speed" from "accuracy"; a fast wrong simulation is not a mature outcome.

## Performance architecture considerations

Potential future optimizations:

- Index records by logical name and id instead of scanning lists.
- Add optional indexes for frequently queried attributes.
- Avoid mutating source entities during query joins.
- Avoid repeated XML parsing for reused FetchXML where possible.
- Separate entity cloning/projection costs from filtering costs.
- Make relationship storage efficient for many-to-many scenarios.

## CI use

Do not run full benchmarks on every PR. Instead:

- Run a small performance smoke test for obvious regressions.
- Run full benchmarks manually or nightly.
- Store benchmark reports as artifacts.
- Compare against the previous release before publishing.

