using Extrode.Jaunty.Configuration;

using System.Reflection;

namespace Extrode.Jaunty;

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
    /// but the catch-all is not: if the extension's hook itself throws, reflection mapping is
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
    /// Switches on reflection mapping from Extrode.Jaunty.Extensions.Reflection when
    /// <c>JauntyConfig.Configure</c> has not been called, and records an unexpected failure in
    /// <see cref="ReflectionMappingInitializationError"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Runs from this class's static constructor, on the first touch of any <c>Jaunty</c> member -
    /// normally the first query. When <c>Configure</c> has run, it already did this and the call is
    /// a no-op. Otherwise only hooks that are still unset are filled (AUD-R35-099), and the
    /// once-only and read-first checks of <c>Configure</c> are bypassed: generated code and Fluent
    /// can read the settings before this class is touched, and refusing here would leave every
    /// query failing with "No mapper found".
    /// </para>
    /// <para>
    /// NativeAOT applications that need reflection mapping call <c>UseReflectionMapping()</c>
    /// inside <c>JauntyConfig.Configure</c>. Applications that only use source-generated mappers do
    /// not need the extension assembly and can trim it.
    /// </para>
    /// </remarks>
    internal static void TryEnableReflectionMapping()
    {
        Exception? failure = TryEnableReflectionMapping(static name => Assembly.Load(name));

        if (failure is not null)
            ReflectionMappingInitializationError = failure;
    }

    /// <summary>
    /// The body of <see cref="TryEnableReflectionMapping()"/>, with the assembly load lifted into a
    /// parameter and the unexpected failure returned rather than recorded (AUD-R35-152), so a test
    /// can drive both catch arms without leaving a recorded error behind.
    /// </summary>
    /// <param name="load">Loads the extension assembly by name.</param>
    /// <returns>
    /// The exception the probe swallowed, or <see langword="null"/> if nothing went wrong, the
    /// assembly was simply absent, or <c>Configure</c> had already run.
    /// </returns>
    internal static Exception? TryEnableReflectionMapping(Func<AssemblyName, Assembly> load)
        => JauntyConfig.InstallAutoReflectionIfUnconfigured(load);
}
