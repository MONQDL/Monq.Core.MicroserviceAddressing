using Xunit;

// Namespace environment variable tests mutate process-wide state, so tests run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
