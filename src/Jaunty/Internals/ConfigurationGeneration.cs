using System.Threading;

namespace Jaunty.Internals;

/// <summary>
/// A monotonic counter bumped whenever configuration that derived caches are built from changes.
/// Caches record the generation they were built under and rebuild when it no longer matches.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 4, medium/bug). Jaunty derives a lot from a handful of configuration delegates:
/// entity metadata from <c>SchemaNameResolver</c>/<c>TableNameResolver</c>/<c>ColumnNameResolver</c>,
/// compiled parameter binders from <c>Reflection{Insert,Update,Delete}BinderResolver</c>, CRUD SQL
/// from the metadata, compiled getters and setters from the columns. All of it is expensive to
/// build and was therefore cached per entity type - and <em>only</em> per entity type, never on the
/// configuration it came from. So the first resolution of a given <c>T</c> fixed its answer for the
/// life of the process, whatever happened to the configuration afterwards.
/// </para>
/// <para>
/// The sharpest form of that was <c>WriteParameterCache&lt;T&gt;</c>, a static generic whose
/// constructor read the binder resolvers once. If it ran while they were null - which
/// <c>JauntyConfig.Reset()</c> guarantees, and which <c>Reset()</c>'s own remarks invite by saying
/// it "is intended for test cleanup" - then <c>Insert&lt;T&gt;</c>, <c>Update&lt;T&gt;</c> and
/// <c>Delete&lt;T&gt;</c> were broken for that <c>T</c> permanently, and re-registering the resolver
/// could not fix it. The read path was recoverable because <c>DrDispatcher</c> consults its resolver
/// per resolution; the write path was not. The documented NativeAOT workaround in
/// <c>Jaunty.Init.cs</c> - "call UseReflectionMapping() at startup" - carried the same unstated
/// ordering requirement: anything touching the write path first poisoned it.
/// </para>
/// <para>
/// This file is the answer to all of it at once. Deferring the read into a per-call closure, which
/// is what the previous fixes in this area did for <c>DefaultEnumStorage</c> and
/// <c>TypeHandlerRegistry</c>, works only for scalars consulted at call time - it cannot help when
/// the configuration determines the <em>shape</em> of what was compiled. A generation counter can:
/// mutators bump it, caches compare against it, and a cache entry built under superseded
/// configuration is simply not a hit.
/// </para>
/// <para>
/// The cost is one relaxed 32-bit read and a comparison per cache lookup - configuration is
/// normally written once at startup, so the steady state is a hit and nothing is ever rebuilt.
/// Counter wrap-around is not a correctness concern: a cache would have to observe exactly
/// 2^32 intervening mutations between two reads to alias, and the consequence would be one stale
/// rebuild skipped rather than a wrong answer.
/// </para>
/// </remarks>
internal static class ConfigurationGeneration
{
    private static int _current;

    /// <summary>
    /// The current generation. Read this <em>before</em> building a cache entry and store it with
    /// the entry: reading it afterwards would let configuration change during the build and still
    /// tag the result as current. Reading it first means such a build is tagged stale and the next
    /// lookup rebuilds, which is self-healing.
    /// </summary>
    public static int Current => Volatile.Read(ref _current);

    /// <summary>
    /// Marks every configuration-derived cache entry built so far as superseded.
    /// </summary>
    public static void Invalidate() => Interlocked.Increment(ref _current);
}

/// <summary>
/// A cached value together with the <see cref="ConfigurationGeneration"/> it was built under.
/// </summary>
/// <remarks>
/// The generation travels on the entry rather than in the cache key so that a superseded entry is
/// overwritten rather than accumulated. Folding it into the key would leave one dead entry per
/// configuration change per cached item, in caches that have no eviction - which a test suite
/// calling <c>JauntyConfig.Reset()</c> between cases would turn into steady growth.
/// </remarks>
internal readonly struct ConfigurationScoped<TValue>
{
    public ConfigurationScoped(int generation, TValue value)
    {
        Generation = generation;
        Value = value;
    }

    public int Generation { get; }

    public TValue Value { get; }
}
