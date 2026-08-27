using System.Data;
using System.Reflection;

using Microsoft.Data.Sqlite;

using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-087. Rounds 34 and 35 filed the same gap once per arity: the eager-validation guards and
/// the <c>connection is not DbConnection</c> cast guard in the multi-entity family are unreached
/// above arity 2. <c>QueryMultiEntityEagerValidationTests</c> is sync-only and covers arity 3;
/// <c>QueryMultiEntityCommandOptionsEagerValidationTests</c> covers async arity 2 and describes
/// itself as a representative sample; <c>AsyncMultiEntityFallbackTests</c> reaches the cast guard at
/// arity 2 only. Deleting a guard line in any of <c>QueryMultiEntity{3..7}.cs</c> or their async
/// twins left the suite green.
/// <para>
/// Hand-written cases cannot speak for 432 overloads - 36 per file across twelve files - which is
/// why the gap was refiled per arity for three rounds running. This sweeps the whole surface by
/// reflection, in the manner of <see cref="ParametersNullGuardTests"/>, so a new overload added
/// without a guard fails here rather than quietly joining the untested majority.
/// </para>
/// </summary>
public class MultiEntityGuardSweepTests
{
    private const string Sql = "SELECT 1 AS Id";

    public sealed class Entity
    {
        public long Id { get; set; }
    }

    private static SqliteConnection Connection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Every public multi-entity overload, closed over <see cref="Entity"/>. "Multi-entity" is two
    /// or more generic arguments each constrained to <c>new()</c>; that is the shape the twelve
    /// QueryMultiEntity files emit and nothing else in the public surface has it.
    /// </summary>
    private static List<MethodInfo> Overloads()
    {
        List<MethodInfo> closed = [];

        foreach (MethodInfo method in typeof(Jaunty).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (!method.IsGenericMethodDefinition)
                continue;

            Type[] args = method.GetGenericArguments();
            if (args.Length < 2)
                continue;

            ParameterInfo[] p = method.GetParameters();
            if (p.Length < 2 || p[0].ParameterType != typeof(IDbConnection) || p[1].ParameterType != typeof(string))
                continue;

            // A delegate-typed argument is validated ahead of the rest in some overloads, so a null
            // one would win the race and report a different argument. None of the QueryMultiEntity
            // files has one, but the filter must not start matching a family that does.
            if (p.Skip(2).Any(x => typeof(Delegate).IsAssignableFrom(x.ParameterType)))
                continue;

            if (!args.All(a => a.GetGenericParameterConstraints().All(c => c.IsAssignableFrom(typeof(Entity)))))
                continue;

            try
            {
                closed.Add(method.MakeGenericMethod([.. args.Select(_ => typeof(Entity))]));
            }
            catch (ArgumentException)
            {
                // A constraint Entity cannot satisfy - skipped rather than guessed at.
            }
        }

        return closed;
    }

    /// <summary>
    /// Fills the arguments after <c>(connection, sql)</c>. Value types get their default - which is
    /// what an omitted <c>CommandOptions</c>, <c>MultiEntityCommandOptions</c> or
    /// <see cref="CancellationToken"/> is - and the only reference type in these signatures is the
    /// <c>object parameters</c> argument, which the caller supplies.
    /// </summary>
    private static object?[] Arguments(MethodInfo method, IDbConnection? connection, string? sql, object? parameters)
    {
        ParameterInfo[] p = method.GetParameters();
        object?[] arguments = new object?[p.Length];
        arguments[0] = connection;
        arguments[1] = sql;

        for (int i = 2; i < p.Length; i++)
        {
            arguments[i] = p[i].ParameterType == typeof(object) && p[i].Name == "parameters"
                ? parameters
                : p[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(p[i].ParameterType)
                    : null;
        }

        return arguments;
    }

    private static bool TakesParameters(MethodInfo method)
        => method.GetParameters().Any(p => p.ParameterType == typeof(object) && p.Name == "parameters");

    /// <summary>
    /// Invokes and forces the result, so a lazy iterator or an already-faulted task surfaces its
    /// exception here rather than being discarded. Returns the exception the call produced, or null.
    /// </summary>
    private static Exception? Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            object? result = method.Invoke(null, arguments);
            Force(result);
            return null;
        }
        catch (TargetInvocationException ex)
        {
            return ex.InnerException;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static void Force(object? result)
    {
        switch (result)
        {
            case null:
                return;
            case System.Collections.IEnumerable sequence:
                foreach (object? _ in sequence)
                {
                    break;
                }

                return;
        }

        Type type = result.GetType();

        // Task<T> and ValueTask<T> both expose GetAwaiter().GetResult(), which rethrows the original
        // exception rather than an AggregateException.
        MethodInfo? getAwaiter = type.GetMethod("GetAwaiter", Type.EmptyTypes);
        object? awaiter = getAwaiter?.Invoke(result, null);
        awaiter?.GetType().GetMethod("GetResult", Type.EmptyTypes)?.Invoke(awaiter, null);
    }

    private static string Describe(MethodInfo method)
        => method.Name + '(' + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ')';

    /// <summary>
    /// A floor rather than NotEmpty: the failure this sweep has to survive is a filter that stops
    /// matching and passes vacuously. 432 overloads matched when it was written - 36 in each of
    /// QueryMultiEntity{,3,4,5,6,7}.cs and their async twins.
    /// </summary>
    [Fact]
    public void TheSweepMatchesTheWholeMultiEntitySurface()
    {
        List<MethodInfo> overloads = Overloads();

        Assert.True(overloads.Count >= 400,
            $"Only {overloads.Count} multi-entity overloads matched; 432 did when this sweep was written.");
        Assert.Contains(overloads, m => m.Name == "Query" && m.GetGenericArguments().Length == 7);
        Assert.Contains(overloads, m => m.Name == "QueryStreamAsync" && m.GetGenericArguments().Length == 7);
    }

    [Fact]
    public void EveryOverload_RejectsANullConnection()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads())
        {
            Exception? ex = Invoke(method, Arguments(method, null, Sql, new { }));

            if (ex is not ArgumentNullException { ParamName: "connection" })
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    [Fact]
    public void EveryOverload_RejectsANullSql()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads())
        {
            using SqliteConnection connection = Connection();
            Exception? ex = Invoke(method, Arguments(method, connection, null, new { }));

            if (ex is not ArgumentNullException { ParamName: "sql" })
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    [Fact]
    public void EveryOverload_RejectsWhitespaceSql()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads())
        {
            using SqliteConnection connection = Connection();
            Exception? ex = Invoke(method, Arguments(method, connection, "   ", new { }));

            if (ex is not ArgumentException { ParamName: "sql" } || ex is ArgumentNullException)
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    [Fact]
    public void EveryOverloadTakingParameters_RejectsANullParametersArgument()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads().Where(TakesParameters))
        {
            using SqliteConnection connection = Connection();
            Exception? ex = Invoke(method, Arguments(method, connection, Sql, null));

            if (ex is not ArgumentNullException { ParamName: "parameters" })
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    /// <summary>
    /// The cast guard: an <see cref="IDbConnection"/> that is not a <c>DbConnection</c> cannot run
    /// the async path, and every async overload says so rather than casting and failing later.
    /// </summary>
    [Fact]
    public void EveryAsyncOverload_RejectsANonDbConnection()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads().Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal)))
        {
            using SqliteConnection inner = Connection();
            var connection = new IDbConnectionWrapper(inner);

            Exception? ex = Invoke(method, Arguments(method, connection, Sql, new { }));

            if (ex is not InvalidOperationException || !ex.Message.Contains("DbConnection"))
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}: {ex?.Message}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    /// <summary>
    /// Control: the same reflection path runs a real query to completion, so a green sweep above
    /// cannot be explained by the invocation machinery failing before it reaches the library.
    /// </summary>
    [Fact]
    public void TheSweepCanAlsoRunAnOverloadSuccessfully()
    {
        MethodInfo method = Overloads().Single(m =>
            m.Name == "Query"
            && m.GetGenericArguments().Length == 2
            && m.GetParameters().Length == 2);

        using SqliteConnection connection = Connection();
        object? result = method.Invoke(null, [connection, Sql]);

        Assert.NotNull(result);
        Assert.Single((System.Collections.IEnumerable)result!);
    }
}
