using System.Data;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Data.Sqlite;

using Jaunty.Tests.Helpers;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R35-096. Batch 02a, 02c and the round-34 carried list all report the same shape from
/// different files: the eager <c>connection</c>/<c>sql</c> guards and the async
/// <c>connection is not DbConnection</c> cast guard are unreached across the partial-read, scalar
/// and multiple-result families.
/// <list type="bullet">
/// <item><description><c>QueryPartialAsync</c> - the sync twin has
/// <c>QueryPartialEagerValidationTests</c>, the async one has nothing but a single null-sql case in
/// <c>QueryNullParameterTests</c>, and the cast guard it alone carries is dark.</description></item>
/// <item><description><c>QueryScalarAsync</c>, <c>QueryPartialSingleAsync</c> and
/// <c>QueryPartialSingleOrDefaultAsync</c> - the <c>IDbConnectionFallback</c> suite pins the cast
/// guard for the write, stream, stored-procedure and multi-entity families and has no equivalent
/// for these.</description></item>
/// <item><description>The whole <c>QueryPartialSingle*</c> family, 16 overloads across four files -
/// nothing asserts the null-connection or null/whitespace-sql guards for any of
/// them.</description></item>
/// <item><description><c>QueryMultiple</c>/<c>QueryMultipleAsync</c> - no test passes a null
/// connection, null sql or whitespace sql to any of the 14 overloads, and none reaches the cast
/// guard on the seven async entry points.</description></item>
/// <item><description><c>QuerySingleOrDefault</c>/<c>QuerySingleOrDefaultAsync</c> - the guards are
/// tested for <c>QuerySingle</c> only (<c>QueryNullParameterTests</c>), never for the OrDefault
/// twins (batch 02d).</description></item>
/// </list>
/// <para>
/// Filed per family for two rounds, which is what a hand-written case per overload costs. Swept by
/// reflection instead, in the manner of <see cref="MultiEntityGuardSweepTests"/>, so a new overload
/// added without a guard fails here.
/// </para>
/// </summary>
public class PartialReadGuardSweepTests
{
    private const string Sql = "SELECT 1 AS Id";

    private static readonly string[] Families =
    [
        "QueryPartialAsync",
        "QueryPartialSingle",
        "QueryPartialSingleAsync",
        "QueryPartialSingleOrDefault",
        "QueryPartialSingleOrDefaultAsync",
        "QueryScalarAsync",
        "QueryMultiple",
        "QueryMultipleAsync",
        "QuerySingleOrDefault",
        "QuerySingleOrDefaultAsync",
    ];

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

    private static List<MethodInfo> Overloads()
    {
        List<MethodInfo> closed = [];

        foreach (MethodInfo method in typeof(Jaunty).GetMethods(BindingFlags.Public | BindingFlags.Static))
        {
            if (Array.IndexOf(Families, method.Name) < 0)
                continue;

            ParameterInfo[] p = method.GetParameters();
            if (p.Length < 2 || p[0].ParameterType != typeof(IDbConnection) || p[1].ParameterType != typeof(string))
                continue;

            if (!method.IsGenericMethodDefinition)
            {
                closed.Add(method);
                continue;
            }

            Type[] args = method.GetGenericArguments();
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
    /// A do-nothing callback of whatever delegate shape the overload asks for. The guards under test
    /// all run before the callback is invoked, so a stub that returns <c>default</c> - including a
    /// null <see cref="Task"/> - is enough to get past the reader null-check and reach them.
    /// </summary>
    private static Delegate Stub(Type delegateType)
    {
        MethodInfo invoke = delegateType.GetMethod("Invoke")!;
        ParameterExpression[] parameters = [.. invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType))];
        Expression body = invoke.ReturnType == typeof(void)
            ? Expression.Empty()
            : Expression.Default(invoke.ReturnType);

        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    private static object?[] Arguments(
        MethodInfo method, IDbConnection? connection, string? sql, bool withCallback = true)
    {
        ParameterInfo[] p = method.GetParameters();
        object?[] arguments = new object?[p.Length];
        arguments[0] = connection;
        arguments[1] = sql;

        for (int i = 2; i < p.Length; i++)
        {
            Type type = p[i].ParameterType;

            arguments[i] = typeof(Delegate).IsAssignableFrom(type)
                ? withCallback ? Stub(type) : null
                : type == typeof(object) && p[i].Name == "parameters"
                    ? new { }
                    : type.IsValueType
                        ? Activator.CreateInstance(type)
                        : null;
        }

        return arguments;
    }

    private static bool TakesCallback(MethodInfo method)
        => method.GetParameters().Any(p => typeof(Delegate).IsAssignableFrom(p.ParameterType));

    private static Exception? Invoke(MethodInfo method, object?[] arguments)
    {
        try
        {
            Force(method.Invoke(null, arguments));
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
        if (result is null)
            return;

        if (result is IDisposable disposable)
        {
            disposable.Dispose();
            return;
        }

        Type type = result.GetType();
        MethodInfo? getAwaiter = type.GetMethod("GetAwaiter", Type.EmptyTypes);
        object? awaiter = getAwaiter?.Invoke(result, null);
        awaiter?.GetType().GetMethod("GetResult", Type.EmptyTypes)?.Invoke(awaiter, null);
    }

    private static string Describe(MethodInfo method)
        => method.Name + '(' + string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name)) + ')';

    /// <summary>
    /// A floor rather than NotEmpty: a filter that stops matching would otherwise pass every theory
    /// below vacuously. 46 overloads matched when this was written.
    /// </summary>
    [Fact]
    public void TheSweepMatchesTheseFamilies()
    {
        List<MethodInfo> overloads = Overloads();

        Assert.True(overloads.Count >= 42,
            $"Only {overloads.Count} overloads matched; 46 did when this sweep was written.");

        foreach (string family in Families)
            Assert.Contains(overloads, m => m.Name == family);
    }

    [Fact]
    public void EveryOverload_RejectsANullConnection()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads())
        {
            Exception? ex = Invoke(method, Arguments(method, null, Sql));

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
            Exception? ex = Invoke(method, Arguments(method, connection, null));

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
            Exception? ex = Invoke(method, Arguments(method, connection, "   "));

            if (ex is not ArgumentException { ParamName: "sql" } || ex is ArgumentNullException)
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    [Fact]
    public void EveryCallbackOverload_RejectsANullCallback()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads().Where(TakesCallback))
        {
            using SqliteConnection connection = Connection();
            Exception? ex = Invoke(method, Arguments(method, connection, Sql, withCallback: false));

            if (ex is not ArgumentNullException { ParamName: "reader" })
                unguarded.Add($"{Describe(method)} -> {ex?.GetType().Name ?? "no exception"}");
        }

        Assert.True(unguarded.Count == 0, string.Join("\n", unguarded.Take(20)));
    }

    [Fact]
    public void EveryAsyncOverload_RejectsANonDbConnection()
    {
        List<string> unguarded = [];

        foreach (MethodInfo method in Overloads().Where(m => m.Name.EndsWith("Async", StringComparison.Ordinal)))
        {
            using SqliteConnection inner = Connection();
            var connection = new IDbConnectionWrapper(inner);

            Exception? ex = Invoke(method, Arguments(method, connection, Sql));

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
            m.Name == "QueryPartialSingle" && m.GetParameters().Length == 2);

        using SqliteConnection connection = Connection();
        object? result = method.Invoke(null, [connection, Sql]);

        Assert.Equal(1, Assert.IsType<Entity>(result).Id);
    }
}
