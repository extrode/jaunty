using Jaunty.Configuration;

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
    /// AUD-R26-055 (batch 4, low/consistency). <see cref="TryEnableReflectionMapping()"/>'s catch-all
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
    internal static void TryEnableReflectionMapping()
    {
        Exception? failure = TryEnableReflectionMapping(static name => Assembly.Load(name));

        if (failure is not null)
            ReflectionMappingInitializationError = failure;
    }

    /// <summary>
    /// The body of <see cref="TryEnableReflectionMapping()"/>, with the assembly load lifted into a
    /// parameter and the unexpected failure returned rather than recorded.
    /// </summary>
    /// <param name="load">Loads the extension assembly by name.</param>
    /// <returns>
    /// The exception the catch-all swallowed, or <see langword="null"/> if nothing went wrong or the
    /// assembly was simply absent.
    /// </returns>
    /// <remarks>
    /// AUD-R35-152. The catch-all that AUD-R26-055 added existed for four rounds with no test:
    /// <c>ReflectionMappingInitializationTests</c> asserts only that
    /// <see cref="ReflectionMappingInitializationError"/> is null in a healthy process, which it
    /// would be just as readily if the assignment were reverted to a bare <c>catch { }</c>. The gap
    /// was structural rather than an oversight - the code runs from a static constructor, and the
    /// only thing it can be made to fail on is an assembly load.
    /// <para>
    /// So the load is a parameter, and the failure comes back as a return value rather than being
    /// written to the property: a test can drive both catch arms without leaving a recorded error
    /// behind for whatever runs next in the process. The parameterless entry point is the only
    /// writer, and behaves exactly as it did.
    /// </para>
    /// </remarks>
#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2026", Justification = "Extension loading is wrapped in try-catch; NativeAOT users initialize manually.")]
    [UnconditionalSuppressMessage("AOT", "IL2075", Justification = "Extension loading is wrapped in try-catch; NativeAOT users initialize manually.")]
#endif
    internal static Exception? TryEnableReflectionMapping(Func<AssemblyName, Assembly> load)
    {
        try
        {
            // Auto-discover and enable reflection mapping if the extension assembly is present
            var assembly = load(new AssemblyName("Jaunty.Extensions.Reflection"));

            Type? type = assembly.GetType("Jaunty.Extensions.Reflection.JauntyReflectionExtensions");
            // AOT-SAFE: optional probe for Jaunty.Extensions.Reflection; absent or trimmed is the expected source-gen-only case, caught below and recorded in ReflectionMappingInitializationError.
            MethodInfo? method = type?.GetMethod("UseReflectionMapping", BindingFlags.Public | BindingFlags.Static);

            if (method is null)
                return null;

            // AUD-R35-099. This constructor does not run at startup: it runs on the first touch of
            // any Jaunty static member, which is normally the caller's first query. Every hook below
            // is public and documented as settable at any time, so anything the caller configured
            // before that first query - a custom metadata resolver, a shimmed mapper - was
            // overwritten here by UseReflectionMapping()'s unconditional assignments, silently, at a
            // moment the caller has no way to observe. The symptom is "my resolver is never called".
            //
            // Only this auto-init path is guarded. An explicit UseReflectionMapping() call still
            // installs all seven, because that is exactly what the caller asked for.
            var mapper = JauntyConfig.ReflectionMapperResolver;
            var insertBinder = JauntyConfig.ReflectionInsertBinderResolver;
            var updateBinder = JauntyConfig.ReflectionUpdateBinderResolver;
            var deleteBinder = JauntyConfig.ReflectionDeleteBinderResolver;
            var tableMetadata = JauntyConfig.ReflectionTableMetadataResolver;
            var multiMapper = JauntyConfig.ReflectionMultiMapperResolver;
            var multiMapperN = JauntyConfig.ReflectionMultiMapperResolverN;

            method.Invoke(null, null);

            if (mapper is not null) JauntyConfig.ReflectionMapperResolver = mapper;
            if (insertBinder is not null) JauntyConfig.ReflectionInsertBinderResolver = insertBinder;
            if (updateBinder is not null) JauntyConfig.ReflectionUpdateBinderResolver = updateBinder;
            if (deleteBinder is not null) JauntyConfig.ReflectionDeleteBinderResolver = deleteBinder;
            if (tableMetadata is not null) JauntyConfig.ReflectionTableMetadataResolver = tableMetadata;
            if (multiMapper is not null) JauntyConfig.ReflectionMultiMapperResolver = multiMapper;
            if (multiMapperN is not null) JauntyConfig.ReflectionMultiMapperResolverN = multiMapperN;
        }
        catch (Exception ex) when (ex is FileNotFoundException or TypeLoadException or MissingMethodException)
        {
            // Extension not present or trimmed away, which is fine for source-gen-only users
            // NativeAOT applications should manually initialize if they need reflection mapping
            return null;
        }
        catch (Exception ex)
        {
            // AUD-R26-055: not silently ignored any more. Still swallowed - this runs from a static
            // constructor, where throwing would turn a missing optional feature into a
            // TypeInitializationException on first touch of any Jaunty API - but recorded, so the
            // "No mapper found for type 'X'" errors that follow can be traced to their cause. See
            // ReflectionMappingInitializationError.
            return ex;
        }

        return null;
    }
}