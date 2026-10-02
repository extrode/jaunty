using System.Reflection;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace Extrode.Jaunty.Configuration;

/// <summary>
/// Switches on reflection mapping when Extrode.Jaunty.Extensions.Reflection is present, without the
/// caller referencing it. Used by <see cref="JauntyConfig.Configure"/> and, when <c>Configure</c>
/// is never called, by <c>Jaunty</c>'s static constructor.
/// </summary>
/// <remarks>
/// NativeAOT-safe: an absent or trimmed extension is the supported source-gen-only configuration
/// and is not an error. NativeAOT applications that need reflection mapping call
/// <c>UseReflectionMapping()</c> on the builder themselves.
/// </remarks>
internal static class AutoReflection
{
    private const string ExtensionAssembly = "Extrode.Jaunty.Extensions.Reflection";
    private const string ExtensionType = "Extrode.Jaunty.Extensions.Reflection.JauntyReflectionExtensions";
    private const string HookName = "ApplyAutoReflection";

    /// <summary>
    /// Applies reflection mapping to <paramref name="builder"/> if the extension is present. An
    /// unexpected failure propagates: inside <see cref="JauntyConfig.Configure"/> there is a caller
    /// to report it to.
    /// </summary>
    internal static void Apply(JauntyConfigBuilder builder)
    {
        Exception? failure = TryApply(builder, static name => Assembly.Load(name));
        if (failure is not null)
            throw new InvalidOperationException("Switching on reflection mapping from " + ExtensionAssembly + " failed.", failure);
    }

    /// <summary>
    /// Applies reflection mapping to <paramref name="builder"/> if the extension is present.
    /// </summary>
    /// <param name="builder">Receives the extension's resolvers.</param>
    /// <param name="load">Loads the extension assembly by name; a parameter so tests can drive both failure arms.</param>
    /// <returns>
    /// The unexpected exception, or <see langword="null"/> if nothing went wrong or the assembly
    /// was simply absent (AUD-R35-152).
    /// </returns>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Extension loading is wrapped in try-catch; NativeAOT users call UseReflectionMapping() on the builder.")]
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Extension loading is wrapped in try-catch; NativeAOT users call UseReflectionMapping() on the builder.")]
#endif
    internal static Exception? TryApply(JauntyConfigBuilder builder, Func<AssemblyName, Assembly> load)
    {
        try
        {
            var assembly = load(new AssemblyName(ExtensionAssembly));

            Type? type = assembly.GetType(ExtensionType);
            // AOT-SAFE: optional probe for Extrode.Jaunty.Extensions.Reflection; absent or trimmed is the expected source-gen-only case and returns null unrecorded - only an unexpected exception is returned to the caller.
            MethodInfo? method = type?.GetMethod(HookName, BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(JauntyConfigBuilder) }, null);

            if (method is null)
                return type is null ? null : MismatchedExtension(ProductVersion(assembly), ProductVersion(typeof(AutoReflection).Assembly));

            method.Invoke(null, new object[] { builder });
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or MissingMethodException)
        {
            return null;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return ex.InnerException;
        }
        catch (Exception ex)
        {
            return ex;
        }

        return null;
    }

    /// <summary>
    /// The error for an extension assembly that has the extension type but not the hook: a package
    /// from another release next to the core. A missing hook at the same version is a trimmed one,
    /// the expected source-gen-only case.
    /// </summary>
    /// <remarks>
    /// Compares package versions, not <see cref="AssemblyName.Version"/>: releases set only the
    /// package version, so every assembly version is 1.0.0.0 and could not tell rc.2 from rc.3.
    /// </remarks>
    internal static Exception? MismatchedExtension(string? extension, string? core)
        => extension is not null && core is not null && !string.Equals(extension, core, StringComparison.Ordinal)
            ? new InvalidOperationException(
                ExtensionAssembly + " " + extension + " does not match Extrode.Jaunty " + core +
                " and cannot switch on reflection mapping; update it to the same version.")
            : null;

    /// <summary>
    /// The package version an assembly was built as, without the build metadata after <c>+</c>.
    /// </summary>
    internal static string? ProductVersion(Assembly assembly)
    {
        // AOT-SAFE: assembly-level attributes are kept by the trimmer; if one is ever stripped this returns null and the version check is skipped.
        string? version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (version is null)
            return null;
        int plus = version.IndexOf('+');
        return plus < 0 ? version : version.Substring(0, plus);
    }
}
