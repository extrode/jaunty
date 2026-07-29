using System.Data;

using DuckDB.NET.Data;

namespace Jaunty.FlatFiles.DuckDB.Internals;

/// <summary>
/// Describes what a DuckDB command binds, in the shape <c>ICommandInterceptor</c> implementations
/// and <c>JauntyConfig.Logger</c> already understand.
/// </summary>
/// <remarks>
/// <para>
/// AUD-R26 (batch 7, high/security; part of the interception cluster). Neither
/// <c>Jaunty.FlatFiles</c> nor <c>Jaunty.FlatFiles.DuckDB</c> contained a single reference to
/// <c>InterceptorPipeline</c> or <c>JauntyConfig.Logger</c> - grep across every file in both
/// returned zero - against 21 <c>CreateCommand()</c> sites that build and execute commands
/// directly. Every DuckDB read, write, delete, update, write-back and import was invisible to a
/// registered interceptor.
/// </para>
/// <para>
/// The finding filed the fix as "a shared execution helper used by every assembly that opens a
/// command", jointly with the same gap in Jaunty.Fluent and in core's write paths. That helper is
/// <c>Jaunty.Internals.CommandObservation</c>, reachable here through the <c>InternalsVisibleTo</c>
/// grant Jaunty core already gave this assembly.
/// </para>
/// <para>
/// A dictionary rather than the parameter list itself: <c>LoggingInterceptor.FormatParameters</c>
/// reflects over an unrecognised object's public properties, so handing it a
/// <c>List&lt;DuckDBParameter&gt;</c> would log <c>Capacity=…, Count=…</c> instead of the values.
/// <c>IDictionary&lt;string, object?&gt;</c> is a first-class parameter set to both the binder and
/// the logger.
/// </para>
/// </remarks>
internal static class DuckDbObservation
{
    /// <summary>An empty parameter set, shared so a parameterless command allocates nothing.</summary>
    private static readonly Dictionary<string, object?> None = new(0, StringComparer.OrdinalIgnoreCase);

    public static object Describe(List<DuckDBParameter> parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return None;

        var described = new Dictionary<string, object?>(parameters.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < parameters.Count; i++)
        {
            DuckDBParameter parameter = parameters[i];
            string name = parameter.ParameterName ?? i.ToString(System.Globalization.CultureInfo.InvariantCulture);

            // Positional DuckDB parameters can repeat the empty name; keep every one rather than
            // letting a later blank overwrite an earlier value in the audit record.
            described[described.ContainsKey(name) ? $"{name}#{i}" : name] = parameter.Value;
        }

        return described;
    }

    public static object Describe((string Name, object? Value)[] parameters)
    {
        if (parameters is null || parameters.Length == 0)
            return None;

        var described = new Dictionary<string, object?>(parameters.Length, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < parameters.Length; i++)
        {
            (string name, object? value) = parameters[i];

            // Callers may pass the bare name or the "$" placeholder text; report the bare form so
            // an audit record reads the same whichever spelling the caller used.
            string key = name.Length > 0 && name[0] == '$' ? name.Substring(1) : name;
            described[described.ContainsKey(key) ? $"{key}#{i}" : key] = value;
        }

        return described;
    }

    /// <summary>The command type every DuckDB statement Jaunty issues carries.</summary>
    public const CommandType Text = CommandType.Text;
}
