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
            var type = assembly.GetType("Jaunty.Extensions.Reflection.JauntyReflectionExtensions");
            var method = type?.GetMethod("UseReflectionMapping", BindingFlags.Public | BindingFlags.Static);
            method?.Invoke(null, null);
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or MissingMethodException)
        {
            // Extension not present or trimmed away, which is fine for source-gen-only users
            // NativeAOT applications should manually initialize if they need reflection mapping
        }
        catch
        {
            // Other exceptions (e.g., security) are silently ignored to maintain backward compatibility
        }
    }
}