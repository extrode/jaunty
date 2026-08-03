namespace Jaunty.FlatFiles.DuckDB.Tests.Unit;

/// <summary>
/// AUD-R35-008. <c>JauntyConfig</c>'s interceptor list and <c>Logger</c> are process-wide mutable
/// state, and xUnit runs test classes in parallel. Two classes here register their own recording
/// interceptor and clear the list in their constructor and <c>Dispose</c>, so run concurrently each
/// saw the other's commands - or had its interceptor cleared mid-test. The visible symptom was
/// <c>TheInsertIsReportedOncePerImportNotOncePerRow</c> counting more than one INSERT and
/// <c>TheInsertItselfReachesTheInterceptor</c> finding none, intermittently, in the full run only;
/// both pass 4/4 in isolation.
/// </summary>
/// <remarks>
/// Every test class that mutates <c>JauntyConfig</c> global state belongs in this collection.
/// Nothing enforces that but this comment - a new class that registers an interceptor and does not
/// join the collection reintroduces the flake.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class GlobalInterceptorStateCollection
{
    public const string Name = "jaunty-global-interceptor-state";
}
