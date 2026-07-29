using System.Reflection;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Jaunty;

public static partial class Jaunty
{
    static Jaunty()
    {
        TryEnableReflectionMapping();
    }

    /// <summary>
    /// The exception that stopped reflection mapping from being enabled at startup, or
    /// <see langword="null"/> if nothing went wrong.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R26-055 (batch 4, low/consistency). <see cref="TryEnableReflectionMapping"/>'s catch-all
    /// swallowed every unanticipated exception with the note that they were "silently ignored to
    /// maintain backward compatibility". The narrow first catch is genuinely expected - the
    /// extension assembly being absent or trimmed is the supported source-gen-only configuration -
    /// but the catch-all is not: if <c>UseReflectionMapping</c> itself throws, reflection mapping is
    /// off and every subsequent query fails with "No mapper found for type 'X'", with nothing
    /// connecting the two symptoms.
    /// </para>
    /// <para>
    /// This records the failure without changing the no-throw contract a static constructor needs.
    /// It stays <see langword="null"/> for the expected absent-assembly case, which is not a
    /// failure. Anyone diagnosing a "No mapper found" error can read it and see the real cause.
    /// </para>
    /// </remarks>
    public static Exception? ReflectionMappingInitializationError { get; private set; }

    /// <summary>
    /// Attempts to load and initialize reflection-based mapping from Jaunty.Extensions.Reflection.
    /// This method is NativeAOT-safe: it gracefully handles the case where the extension assembly
    /// is not present or was trimmed away.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For NativeAOT applications, there are two options:
    /// </para>
    /// <list type="number">
    /// <item><description>Exclude Jaunty.Extensions.Reflection from trimming (recommended if using special types)</description></item>
    /// <item><description>Manually call <c>JauntyReflectionExtensions.UseReflectionMapping()</c> and <c>SpecialTypeMappers.Register()</c> at startup</description></item>
    /// </list>
    /// <para>
    /// Applications that only use source-generated mappers (entities with <c>[Table]</c> attribute)
    /// do not need the extension assembly and can safely trim it.
    /// </para>
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Extension loading is wrapped in try-catch; NativeAOT users initialize manually.")]
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Extension loading is wrapped in try-catch; NativeAOT users initialize manually.")]
#endif
    private static void TryEnableReflectionMapping()
    {
        try
        {
            // Auto-discover and enable reflection mapping if the extension assembly is present
            var assembly = Assembly.Load(new AssemblyName("Jaunty.Extensions.Reflection"));

            Type? type = assembly.GetType("Jaunty.Extensions.Reflection.JauntyReflectionExtensions");
            MethodInfo? method = type?.GetMethod("UseReflectionMapping", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or MissingMethodException)
        {
            // Extension not present or trimmed away, which is fine for source-gen-only users
            // NativeAOT applications should manually initialize if they need reflection mapping
        }
        catch (Exception ex)
        {
            // AUD-R26-055: not silently ignored any more. Still swallowed - this runs from a static
            // constructor, where throwing would turn a missing optional feature into a
            // TypeInitializationException on first touch of any Jaunty API - but recorded, so the
            // "No mapper found for type 'X'" errors that follow can be traced to their cause. See
            // ReflectionMappingInitializationError.
            ReflectionMappingInitializationError = ex;
        }
    }
}