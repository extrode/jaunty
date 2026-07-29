using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace Jaunty.Scaffolding.Internals;

/// <summary>
/// Constructs a provider's <see cref="DbConnection"/> by reflection, so the scaffolder does not
/// take a compile-time dependency on all four ADO.NET providers at once.
/// </summary>
internal static class ReflectedConnectionFactory
{
    /// <summary>
    /// Instantiates <paramref name="connectionType"/> with the given connection string,
    /// reporting a constructor failure as the exception the constructor actually threw.
    /// </summary>
    /// <remarks>
    /// AUD-R26: the four schema readers each called
    /// <c>Activator.CreateInstance(type, connectionString)</c> directly. That wraps anything the
    /// constructor throws in <see cref="TargetInvocationException"/>, whose own message is the
    /// fixed string "Exception has been thrown by the target of an invocation." -
    /// <see cref="Scaffolder.ScaffoldAsync"/> catches and reports <c>ex.Message</c>, so that is
    /// verbatim what the user saw. Measured: scaffolding with a malformed connection string
    /// printed exactly that and nothing else, while the real cause sat one level down in
    /// <c>InnerException</c> as <c>ArgumentException: Format of the initialization string does
    /// not conform to specification starting at index 0.</c>
    ///
    /// <para>
    /// <see cref="ExceptionDispatchInfo"/> rethrows the inner exception with its original stack
    /// trace intact rather than <c>throw ex</c>, which would reset it to this line.
    /// </para>
    /// </remarks>
    /// <param name="connectionType">
    /// The provider's connection type. Annotated for the trimmer: callers reach it through
    /// <c>Type.GetType(&lt;literal&gt;)</c>, which the trimmer treats intrinsically, but those
    /// annotations do not survive being passed across a method boundary - without this the
    /// extraction of this helper fails the build with IL2067.
    /// </param>
    /// <param name="connectionString">The connection string to pass to the constructor.</param>
    public static DbConnection Create(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type connectionType,
        string connectionString)
    {
        try
        {
            return (DbConnection)Activator.CreateInstance(connectionType, connectionString)!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw; // unreachable - Throw() always throws, but the compiler needs a path out.
        }
    }
}
