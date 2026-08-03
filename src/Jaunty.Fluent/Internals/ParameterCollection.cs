using System.Data;
using System.Dynamic;

namespace Jaunty.Fluent.Internals;

/// <summary>
/// Efficiently collects parameters for SQL queries without LINQ overhead.
/// </summary>
internal sealed class ParameterCollection
{
    private readonly List<(string Name, object? Value)> _parameters = new();
    private readonly HashSet<string> _names = new();

    // AUD-R35-201. The duplicate guard keys on the raw name, but ToParameterObject strips one
    // leading sigil before writing the expando, so "@p0" and "p0" passed Add, bound as two distinct
    // parameters through BindTo, and then collapsed onto a single "p0" key with the last value
    // winning - the exact silent-overwrite-versus-provider-throw split Add's own doc says the guard
    // removed. Keying the guard on the same stripped name ToParameterObject uses closes the half it
    // left open. _names stays keyed on the raw name because Contains and CreateUniqueName are asked
    // about placeholder text as it appears in SQL, sigil included.
    private readonly HashSet<string> _strippedNames = new();

    public int Count => _parameters.Count;

    /// <summary>
    /// Adds a parameter. Throws if <paramref name="name"/> was already added, so a bug that
    /// produces a duplicate parameter name fails consistently and immediately - previously
    /// <see cref="BindTo"/> would add both duplicates to the command (the ADO.NET provider then
    /// throws), while <see cref="ToParameterObject"/> silently let the last value win via
    /// dictionary overwrite, so the failure mode depended on which output path the caller used.
    /// </summary>
    public void Add(string name, object? value)
    {
        if (!_names.Add(name))
            throw new ArgumentException($"A parameter named '{name}' has already been added.", nameof(name));

        if (!_strippedNames.Add(StripSigil(name)))
        {
            _names.Remove(name);
            throw new ArgumentException(
                $"A parameter named '{name}' collides with one already added - two names that " +
                "differ only by their sigil bind to the same parameter.", nameof(name));
        }

        _parameters.Add((name, value));
    }

    /// <summary>
    /// Whether a parameter with this name has already been added. Lets callers raise a more
    /// specific error before <see cref="Add"/>'s generic duplicate check fires.
    /// </summary>
    public bool Contains(string name) => _names.Contains(name);

    /// <summary>
    /// Builds a placeholder name derived from a caller-supplied column that is safe to use as a
    /// SQL parameter identifier and is not already in this collection.
    /// </summary>
    /// <param name="parameterPrefix">The dialect's parameter sigil, e.g. <c>"@"</c>.</param>
    /// <param name="baseName">The caller's column text, qualified or not.</param>
    /// <remarks>
    /// AUD-R35-014. The string-based <c>Where(string column, object value)</c> overloads on the
    /// two-, three- and four-table joined builders each derived the placeholder straight from the
    /// column text - <c>prefix + column.Replace(".", "_")</c> - with no uniquifier and no
    /// sanitisation, so two filters on one column threw
    /// <c>ArgumentException: A parameter named '@o_order_date' has already been added</c> out of
    /// <see cref="Add"/>. A range filter, <c>.Where("o.order_date", from).Where("o.order_date",
    /// to)</c>, is the ordinary way to hit it. The single-table twin
    /// <c>QueryBuilder.Where(string, object?)</c> had both protections and the expression overloads
    /// on the joined builders are renumbered by <c>AddWhereExpression</c>; the string overload was
    /// the one path with neither.
    /// <para>
    /// Two distinct columns could also collapse onto one name, since <c>.</c> was rewritten to
    /// <c>_</c>: <c>"p.category_id"</c> and <c>"p_category_id"</c> both became
    /// <c>@p_category_id</c>. The count suffix separates them.
    /// </para>
    /// <para>
    /// Sanitisation is the AUD-R22 rule: anything that is not a letter, digit or underscore
    /// becomes an underscore, so <c>Where("Order Date", v)</c> cannot emit the malformed
    /// <c>@Order Date</c>. The <c>while</c> loop closes the residual case the bare count suffix
    /// leaves - a caller whose column text already ends in <c>_&lt;n&gt;</c> matching the current
    /// count - which would otherwise still reach <see cref="Add"/>'s throw.
    /// </para>
    /// </remarks>
    public string CreateUniqueName(string parameterPrefix, string baseName)
    {
        string sanitized = Sanitize(baseName);
        int suffix = _parameters.Count;
        string candidate = parameterPrefix + sanitized + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);

        while (_names.Contains(candidate))
        {
            suffix++;
            candidate = parameterPrefix + sanitized + "_" + suffix.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return candidate;
    }

    private static string Sanitize(string name)
    {
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_')
                chars[i] = '_';
        }
        return new string(chars);
    }

    public void AddRange(List<(string Name, object? Value)> parameters)
    {
        foreach ((string name, object? value) in parameters)
            Add(name, value);
    }

    /// <summary>
    /// Binds all parameters directly to the command (bypasses reflection-based ParameterBinder).
    /// </summary>
    public void BindTo(IDbCommand command)
    {
        for (int i = 0; i < _parameters.Count; i++)
        {
            (string? name, object? value) = _parameters[i];
            IDbDataParameter param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value ?? DBNull.Value;
            command.Parameters.Add(param);
        }
    }

    /// <summary>
    /// Creates a parameter object suitable for Jaunty's core Query methods.
    /// Uses ExpandoObject which ParameterBinder can read via reflection.
    /// </summary>
    /// <remarks>
    /// AUD-R26. This returned <see langword="null"/> when there were no parameters, and all 144 call
    /// sites passed the result straight into a core overload declared <c>object parameters</c> -
    /// non-nullable - with a <c>!</c> suppressing the warning that was telling the truth. It worked
    /// only because those overloads did not check, which is the finding this is part of. Now it
    /// returns an empty <see cref="ExpandoObject"/>: <c>ParameterBinder</c> reads that as an empty
    /// <c>IDictionary&lt;string, object?&gt;</c> and binds nothing, which is what a fluent query with
    /// no parameters wants, and the SQL those queries generate has no placeholders to leave unbound.
    /// The <c>!</c> at the call sites is now redundant rather than wrong; they are left alone because
    /// rewriting 144 lines for a no-op is risk without benefit.
    /// </remarks>
    public object ToParameterObject()
    {
        var expando = new ExpandoObject();
        var dict = (IDictionary<string, object?>)expando;

        for (int i = 0; i < _parameters.Count; i++)
        {
            (string? name, object? value) = _parameters[i];
            dict[StripSigil(name)] = value;
        }

        return expando;
    }

    /// <summary>
    /// The parameter's name without one leading sigil, which is the name a parameter object's key
    /// and the duplicate guard both use. One sigil, not all of them: a name like <c>@@rowcount</c>
    /// keeps its second one, as <c>ParameterRenamer</c> and <c>JoinParameterName.Qualify</c> do.
    /// </summary>
    private static string StripSigil(string name) =>
        name.Length > 0 && name[0] is '@' or '$' or ':' ? name.Substring(1) : name;

    public void Clear()
    {
        _parameters.Clear();
        _names.Clear();
        _strippedNames.Clear();
    }

    /// <summary>
    /// Gets all parameters as a list of tuples.
    /// </summary>
    public IReadOnlyList<(string Name, object? Value)> GetAll() => _parameters;

    /// <summary>
    /// Creates a copy of this parameter collection.
    /// </summary>
    public ParameterCollection Clone()
    {
        var clone = new ParameterCollection();
        clone._parameters.AddRange(_parameters);
        clone._names.UnionWith(_names);
        clone._strippedNames.UnionWith(_strippedNames);
        return clone;
    }
}