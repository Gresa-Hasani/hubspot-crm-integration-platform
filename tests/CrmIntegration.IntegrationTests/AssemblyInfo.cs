using Xunit;

// All integration tests share one real PostgreSQL database (no per-test schema/transaction
// isolation). Most tests avoid collisions with random/unique test data, but tests that query
// "the next claimable row" (IIntegrationEventRepository.TryClaimNextAsync) are inherently
// sensitive to *any* other test concurrently inserting a Received IntegrationEvent — xUnit runs
// test classes in parallel by default, which made that class of test flaky. Running the whole
// assembly sequentially is the simplest correct fix, and the suite is small enough (a few
// seconds) that the serialization cost is negligible.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
