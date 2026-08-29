using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Jaunty.Fluent.Tests.Unit;

/// <summary>
/// Guards the rooting of grouped-projection result types.
///
/// GroupedJoinedResultMapper reflects over the caller's projection type to find its constructor and
/// properties. Until this was fixed nothing rooted that type: a trimmed or NativeAOT build removed
/// the members, GetProperties returned empty, and every row mapped to a fully defaulted instance
/// with no exception. An IL2090 pragma suppressed the only warning that would have said so.
///
/// The fix is [DynamicallyAccessedMembers] on TResult at every hop. These tests fail if a new
/// overload is added without it, which is the way the gap would come back.
/// </summary>
public class ProjectionRootingTests
{
    private const DynamicallyAccessedMemberTypes Required =
        DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicConstructors;

    private static readonly Type[] GroupedInterfaces =
    [
        typeof(IGroupedQuery<,>),
        typeof(IGroupedJoinedQuery<,,>),
        typeof(IGroupedJoinedQuery3<,,,>),
        typeof(IGroupedJoinedQuery4<,,,,>)
    ];

    private static IEnumerable<MethodInfo> ProjectionMethods(Type type) =>
        type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsGenericMethodDefinition)
            .Where(m => m.Name is "Select" or "SelectAsync");

    private static DynamicallyAccessedMemberTypes AnnotationOf(Type genericParameter)
    {
        var attribute = genericParameter
            .GetCustomAttributes(typeof(DynamicallyAccessedMembersAttribute), inherit: false)
            .Cast<DynamicallyAccessedMembersAttribute>()
            .FirstOrDefault();

        return attribute?.MemberTypes ?? DynamicallyAccessedMemberTypes.None;
    }

    [Fact]
    public void EveryGroupedSelectRootsItsProjectionType()
    {
        var unrooted = new List<string>();

        foreach (Type type in GroupedInterfaces)
        {
            foreach (MethodInfo method in ProjectionMethods(type))
            {
                Type tResult = method.GetGenericArguments()
                    .Single(a => a.Name == "TResult");

                if ((AnnotationOf(tResult) & Required) != Required)
                    unrooted.Add($"{type.Name}.{method.Name}({method.GetParameters().Length} params)");
            }
        }

        Assert.Empty(unrooted);
    }

    [Fact]
    public void TheGroupedInterfacesActuallyDeclareProjectionOverloads()
    {
        // Without this the test above passes vacuously if the methods are ever renamed: an empty
        // set of methods trivially contains no unrooted one.
        foreach (Type type in GroupedInterfaces)
            Assert.NotEmpty(ProjectionMethods(type));
    }

    [Fact]
    public void TheBuildersRootProjectionTypesToo()
    {
        // The interface annotation does not flow to an implementation automatically - a mismatch is
        // IL2095. The builders are internal, so they are reached through the assembly.
        Assembly fluent = typeof(IGroupedQuery<,>).Assembly;

        var unrooted = new List<string>();

        foreach (Type type in fluent.GetTypes().Where(t => t.Name.StartsWith("Grouped", StringComparison.Ordinal)))
        {
            foreach (MethodInfo method in type
                         .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(m => m.IsGenericMethodDefinition)
                         .Where(m => m.Name is "Select" or "SelectAsync"))
            {
                Type? tResult = method.GetGenericArguments().FirstOrDefault(a => a.Name == "TResult");

                if (tResult is not null && (AnnotationOf(tResult) & Required) != Required)
                    unrooted.Add($"{type.Name}.{method.Name}");
            }
        }

        Assert.Empty(unrooted);
    }

    [Fact]
    public void TheMapperEntryPointsRootTheirProjectionType()
    {
        Assembly fluent = typeof(IGroupedQuery<,>).Assembly;

        Type mapper = fluent.GetType("Jaunty.Fluent.Internals.GroupedJoinedResultMapper", throwOnError: true)!;
        Type plan = fluent.GetType("Jaunty.Fluent.Internals.GroupedJoinedResultMapper+ResultMapperPlan", throwOnError: true)!;

        MethodInfo mapResult = mapper.GetMethod("MapResult", BindingFlags.Public | BindingFlags.Static)!;
        MethodInfo resolve = plan.GetMethod("Resolve", BindingFlags.Public | BindingFlags.Static)!;

        foreach (MethodInfo method in new[] { mapResult, resolve })
        {
            Type tResult = method.GetGenericArguments().Single(a => a.Name == "TResult");
            Assert.Equal(Required, AnnotationOf(tResult) & Required);
        }
    }
}
