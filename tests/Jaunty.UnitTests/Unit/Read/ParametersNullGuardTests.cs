using System.Reflection;

using Microsoft.Data.Sqlite;

using Xunit;

namespace Jaunty.Tests.Unit.Read;

/// <summary>
/// AUD-R26 (batch 2, low/consistency). <c>Execute</c> and <c>ExecuteAsync</c> were the only place in
/// the public API that null-checked the <c>object parameters</c> argument - 4 overloads out of 273
/// with that exact signature, across 48 files. So <c>connection.Execute(sql, (object)null!)</c> threw
/// <see cref="ArgumentNullException"/> naming the argument, while
/// <c>connection.Query&lt;T&gt;(sql, (object)null!)</c> went on to execute the statement with nothing
/// bound and failed later inside the provider, with a different exception type and a message that
/// did not name the argument. Same mistake, two diagnoses, decided by which method you called.
///
/// <para>
/// The argument is declared <c>object</c>, not <c>object?</c>, and every one of these methods has a
/// sibling overload that takes no parameters at all - so <see langword="null"/> here is a caller
/// error under the declared contract, not a way of saying "no parameters". The guard was added to
/// all 273 rather than removed from the 4, which would have made the worse diagnostic universal.
/// </para>
/// </summary>
public class ParametersNullGuardTests
{
    private const string Sql = "SELECT 1 WHERE 1 = @Id";

    private static SqliteConnection Connection()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        return connection;
    }

    private static void AssertGuards(Action call)
    {
        ArgumentNullException ex = Assert.Throws<ArgumentNullException>(call);
        Assert.Equal("parameters", ex.ParamName);
    }

    // ------------------------------------------------------------------
    // A representative from each family the sweep touched
    // ------------------------------------------------------------------

    [Fact]
    public void Query_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.Query<object>(Sql, (object)null!));
    }

    [Fact]
    public void QueryPartialList_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.QueryPartialList(Sql, (object)null!).ToList());
    }

    [Fact]
    public void QueryScalar_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.QueryScalar<int>(Sql, (object)null!));
    }

    [Fact]
    public void ExecuteScalar_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.ExecuteScalar<int>(Sql, (object)null!));
    }

    [Fact]
    public void QueryFirstOrDefault_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.QueryFirstOrDefault<object>(Sql, (object)null!));
    }

    [Fact]
    public void QueryStream_Guards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.QueryStream<object>(Sql, (object)null!).ToList());
    }

    /// <summary>The behaviour that was already correct, and the reason the sweep went this way.</summary>
    [Fact]
    public void Execute_StillGuards()
    {
        using SqliteConnection connection = Connection();
        AssertGuards(() => connection.Execute(Sql, (object)null!));
    }

    // ------------------------------------------------------------------
    // The whole surface, not just the samples above
    // ------------------------------------------------------------------

#if NET8_0_OR_GREATER
    // NullabilityInfoContext is net6+, and it is what distinguishes `object` from `object?` here -
    // they are the same runtime type, so on net472 this sweep cannot tell the two contracts apart
    // and would demand a guard on overloads that legitimately accept null. The hand-written cases
    // above run on both frameworks.

    /// <summary>
    /// Seven hand-written cases cannot speak for 273 overloads, and the finding was precisely that
    /// the exceptions were invisible among the many. This reflects over every public extension method
    /// with the <c>(IDbConnection, string, object, ...)</c> shape and asserts each one rejects a null
    /// parameters argument - so a new overload added without the guard fails here rather than
    /// quietly reintroducing the split.
    /// </summary>
    [Fact]
    public void EveryOverloadWithThatSignature_RejectsANullParametersArgument()
    {
        // Nullability is metadata, not runtime type: `object` and `object?` are both
        // typeof(object) to reflection, and the whole distinction here is between them.
        var nullability = new NullabilityInfoContext();

        MethodInfo[] candidates = [.. typeof(Jaunty).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m =>
            {
                ParameterInfo[] p = m.GetParameters();
                return p.Length >= 3
                    && p[0].ParameterType == typeof(IDbConnection)
                    && p[1].ParameterType == typeof(string)
                    && p[2].ParameterType == typeof(object)
                    && p[2].Name == "parameters"
                    // Required and non-nullable. The other two shapes are different contracts where
                    // null legitimately means "no parameters", and must keep working:
                    //   `object? parameters = null`  - the obsolete arity-2 Query/QueryStream family
                    //   `object? parameters`         - the stored-procedure family, whose second
                    //                                  argument is a procedure name, not SQL
                    && !p[2].IsOptional
                    && nullability.Create(p[2]).WriteState == NullabilityState.NotNull;
            })];

        // A floor, not NotEmpty: the failure mode this test must survive is a filter that stops
        // matching and passes vacuously. 288 overloads matched when the sweep was made; the bound is
        // deliberately loose so adding or removing a handful does not break the build, but a filter
        // that collapses does.
        Assert.True(candidates.Length >= 250,
            $"Only {candidates.Length} overloads matched the (IDbConnection, string, object parameters) shape; " +
            "288 did when this sweep was made, so the filter has probably stopped matching.");

        List<string> unguarded = [];

        foreach (MethodInfo method in candidates)
        {
            MethodInfo callable = method;

            if (callable.IsGenericMethodDefinition)
            {
                Type[] args = callable.GetGenericArguments();

                // Every generic parameter here is an entity type; object satisfies the `new()` and
                // reference constraints these methods use. Anything object cannot satisfy is skipped
                // rather than guessed at.
                if (args.Any(a => a.GetGenericParameterConstraints().Length > 0
                                  && !a.GetGenericParameterConstraints().All(c => c.IsAssignableFrom(typeof(object)))))
                {
                    continue;
                }

                try
                {
                    callable = callable.MakeGenericMethod([.. args.Select(_ => typeof(object))]);
                }
                catch (ArgumentException)
                {
                    continue;
                }
            }

            ParameterInfo[] parameters = callable.GetParameters();
            object?[] arguments = new object?[parameters.Length];

            using SqliteConnection connection = Connection();
            arguments[0] = connection;
            arguments[1] = Sql;
            arguments[2] = null;

            for (int i = 3; i < parameters.Length; i++)
            {
                arguments[i] = parameters[i].ParameterType.IsValueType
                    ? Activator.CreateInstance(parameters[i].ParameterType)
                    : null;
            }

            // A delegate-typed argument (the multi-entity `map` functions) is validated before the
            // parameters argument in some overloads; a null one would win the race and report the
            // wrong name, so those are counted separately rather than asserted here.
            if (parameters.Skip(3).Any(p => typeof(Delegate).IsAssignableFrom(p.ParameterType)))
                continue;

            try
            {
                object? result = callable.Invoke(null, arguments);

                // Lazy iterators do not run until enumerated.
                if (result is System.Collections.IEnumerable sequence and not string)
                {
                    foreach (object? _ in sequence)
                    {
                        break;
                    }
                }

                unguarded.Add($"{Describe(callable)} -> returned {result?.GetType().Name ?? "null"} without throwing");
            }
            catch (TargetInvocationException tie) when (tie.InnerException is ArgumentNullException ane
                                                        && ane.ParamName == "parameters")
            {
                // Guarded, as required.
            }
            catch (TargetInvocationException tie)
            {
                unguarded.Add($"{Describe(callable)} -> {tie.InnerException?.GetType().Name}");
            }
            catch (Exception ex)
            {
                // A lazy iterator throws during the enumeration above rather than from Invoke, so it
                // arrives unwrapped. Either way it is not the ArgumentNullException required here.
                unguarded.Add($"{Describe(callable)} -> {ex.GetType().Name} (on enumeration)");
            }
        }

        Assert.True(unguarded.Count == 0,
            $"{unguarded.Count} overload(s) do not reject a null parameters argument:{Environment.NewLine}" +
            string.Join(Environment.NewLine, unguarded));
    }

#endif

    private static string Describe(MethodInfo method)
        => $"{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";
}
