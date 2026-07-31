using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit.Core;

/// <summary>
/// R27 carry-forward 6, enforcing AUD-R26-054's rule: the implicit
/// <c>CommandOptions&lt;T&gt;</c> -> <c>CommandOptions</c> conversion silently drops
/// <c>Mapper</c> and <c>ExpectedRowCount</c>, so an API that maps entities must take
/// <c>CommandOptions&lt;T&gt;</c>, never the non-generic form. The rule was previously enforced
/// only by a doc comment on the conversion operator. This pins the full set of public methods
/// that take the non-generic form while returning a generic-parameter-shaped result without an
/// explicit map delegate; anything new arriving in that shape must be added here deliberately
/// or move to <c>CommandOptions&lt;T&gt;</c>.
/// </summary>
public class CommandOptionsGenericFormDriftTests
{
    private static readonly string[] AllowedScalarOrProjectionMethods =
    [
        // Scalar reads: they convert a single value via ScalarConverter/GetFieldValue<T>, never a
        // mapper, and build no list a row-count hint could pre-size.
        "GridReader.ReadScalar<1>(1)",
        "GridReader.ReadScalarAsync<1>(2)",
    ];

    [Fact]
    public void No_entity_mapping_api_takes_the_non_generic_CommandOptions()
    {
        var offenders = typeof(Jaunty).Assembly.GetTypes()
            .Where(t => t.IsPublic)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(CommandOptions)))
            .Where(m => m.IsGenericMethodDefinition)
            .Where(m => !m.IsDefined(typeof(ObsoleteAttribute), inherit: false))
            .Where(m => ReferencesGenericParameter(m.ReturnType))
            .Where(m => !m.GetParameters().Any(IsMapDelegate))
            .Select(m => $"{m.DeclaringType!.Name}.{m.Name}<{m.GetGenericArguments().Length}>({m.GetParameters().Length})")
            .Distinct()
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        var unexpected = offenders.Except(AllowedScalarOrProjectionMethods).ToList();
        var stale = AllowedScalarOrProjectionMethods.Except(offenders).ToList();

        Assert.True(unexpected.Count == 0,
            "Entity-shaped methods taking non-generic CommandOptions (add to the allow-list only if the method provably never consults a mapper or row-count hint):\n"
            + string.Join("\n", unexpected));
        Assert.True(stale.Count == 0, "Allow-list entries no longer on the surface:\n" + string.Join("\n", stale));
    }

    private static bool ReferencesGenericParameter(Type type)
    {
        if (type.IsGenericParameter)
            return true;
        if (type.HasElementType)
            return ReferencesGenericParameter(type.GetElementType()!);
        return type.IsGenericType && type.GetGenericArguments().Any(ReferencesGenericParameter);
    }

    private static bool IsMapDelegate(ParameterInfo parameter)
    {
        Type type = parameter.ParameterType;
        if (!typeof(Delegate).IsAssignableFrom(type) || !type.IsGenericType)
            return false;
        MethodInfo? invoke = type.GetMethod("Invoke");
        return invoke is not null && ReferencesGenericParameter(invoke.ReturnType);
    }
}
