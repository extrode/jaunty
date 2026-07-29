using Microsoft.Extensions.Logging;

namespace Jaunty.Configuration;

/// <summary>
/// Provides configuration options for Jaunty's logging behavior.
/// </summary>
public sealed class LoggingConfiguration
{
    private ISet<string> _sensitiveParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the minimum log level for Jaunty commands.
    /// </summary>
    /// <remarks>
    /// Default is <see cref="LogLevel.Information"/>. Commands will only be logged
    /// if their severity meets or exceeds this level.
    /// </remarks>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the threshold for slow query logging.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Commands that take longer than this threshold will be logged at <see cref="LogLevel.Warning"/>
    /// regardless of the <see cref="MinimumLogLevel"/> setting.
    /// </para>
    /// <para>
    /// Default is 1 second. Set to <see cref="TimeSpan.Zero"/> to disable slow query logging.
    /// </para>
    /// </remarks>
    public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether parameter values should be included in log output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When true, parameter values are logged alongside the SQL command.
    /// Sensitive parameters (see <see cref="SensitiveParameterNames"/>) are always masked.
    /// </para>
    /// <para>
    /// Default is true. Set to false to disable parameter logging for security or privacy.
    /// </para>
    /// </remarks>
    public bool LogParameters { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the SQL command text should be included in log output.
    /// </summary>
    /// <remarks>
    /// Default is true. Set to false to only log execution times and metadata.
    /// </remarks>
    public bool LogSql { get; set; } = true;

    /// <summary>
    /// Gets or sets whether execution time should be included in log output.
    /// </summary>
    /// <remarks>
    /// Default is true. When enabled, all logged commands include elapsed time in milliseconds.
    /// </remarks>
    public bool LogExecutionTime { get; set; } = true;

    /// <summary>
    /// Gets the set of parameter names that should be masked in log output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parameter names in this set will have their values replaced with "***MASKED***" in log output.
    /// Comparison is case-insensitive and, by default, matches a configured name as a <em>whole
    /// word</em> anywhere in the parameter name - see <see cref="SensitiveParameterMatching"/>. So the
    /// seeded <c>Password</c> also masks <c>NewPassword</c>, <c>PasswordHash</c> and
    /// <c>password_hash</c>, and the seeded <c>Token</c> also masks <c>AccessToken</c> and
    /// <c>RefreshToken</c> - but not <c>TokenizerVersion</c>, where "Token" is not a word.
    /// </para>
    /// <para>
    /// Seeded by default: "Password", "Secret", "Token", "ApiKey", "ApiSecret", "Credentials",
    /// "PrivateKey". Add your own with <see cref="WithSensitiveParameters"/>; a name you add is
    /// matched the same way, so adding "Ssn" also masks "EmployeeSsnValue".
    /// </para>
    /// </remarks>
    public ISet<string> SensitiveParameterNames => _sensitiveParameterNames;

    /// <summary>
    /// Gets or sets how parameter names are compared against <see cref="SensitiveParameterNames"/>.
    /// Default is <see cref="Configuration.SensitiveParameterMatching.WholeWord"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R26 (batch 4, medium/security). This was fixed exact-equality matching, which meant the
    /// seeded names masked almost nothing real: <c>NewPassword</c>, <c>PasswordHash</c>,
    /// <c>AccessToken</c>, <c>RefreshToken</c>, <c>ClientSecret</c> and <c>Api_Key</c> all logged in
    /// clear, at Information level, with <see cref="LogParameters"/> defaulting to
    /// <see langword="true"/> and nothing in the documentation saying matching was exact.
    /// <para>
    /// <b>This is a behaviour change</b>: names that used to log in clear are now masked. It can
    /// still over-mask, but only on a real word - <c>TokenizerVersion</c> is safe because "Token" is
    /// not a word in it, whereas adding a broad name like "Key" of your own would mask
    /// <c>PartitionKey</c> and <c>SortKey</c> along with the credentials you meant. Prefer specific
    /// names; where over-masking does happen, a masked column is a nuisance and a logged credential
    /// is an incident. Set this to
    /// <see cref="Configuration.SensitiveParameterMatching.Exact"/> to restore the old behaviour.
    /// </para>
    /// </remarks>
    public SensitiveParameterMatching SensitiveParameterMatching { get; set; } = SensitiveParameterMatching.WholeWord;

    /// <summary>
    /// Whether <paramref name="parameterName"/> should have its value masked in log output.
    /// </summary>
    /// <remarks>
    /// Lives here rather than in <see cref="Interceptors.LoggingInterceptor"/> so that the
    /// configuration owns the definition of "sensitive", and anything else that formats parameters
    /// answers the question the same way.
    /// </remarks>
    public bool IsSensitiveParameter(string? parameterName)
    {
        if (string.IsNullOrEmpty(parameterName) || _sensitiveParameterNames.Count == 0)
            return false;

        List<string> candidate = SplitWords(parameterName!);
        if (candidate.Count == 0)
            return false;

        foreach (string sensitive in _sensitiveParameterNames)
        {
            List<string> needle = SplitWords(sensitive);
            if (needle.Count == 0)
                continue;

            bool hit = SensitiveParameterMatching == SensitiveParameterMatching.Exact
                ? SequenceEqualsIgnoreCase(candidate, needle)
                : ContainsSequence(candidate, needle);

            if (hit)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Splits a parameter name into words, dropping a leading provider prefix, treating
    /// <c>_</c>, <c>-</c> and <c>.</c> as separators and breaking on camel-case boundaries - so
    /// <c>@api_key</c>, <c>Api-Key</c> and <c>apiKey</c> all yield <c>[api, key]</c>.
    /// </summary>
    /// <remarks>
    /// Word boundaries rather than a plain substring test, because a substring test over-matches in
    /// a way that quietly makes the log useless: "Token" occurs inside <c>TokenizerVersion</c>, and
    /// masking a tokenizer's version number teaches people to turn masking off. Splitting into words
    /// keeps every real hit - <c>AccessToken</c>, <c>RefreshToken</c>, <c>TokenValue</c> - and drops
    /// that class of false positive.
    /// </remarks>
    private static List<string> SplitWords(string name)
    {
        var words = new List<string>();
        int start = name.Length > 0 && name[0] is '@' or ':' or '?' or '$' ? 1 : 0;

        var current = new System.Text.StringBuilder(name.Length);

        void Flush()
        {
            if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        for (int i = start; i < name.Length; i++)
        {
            char c = name[i];

            if (c is '_' or '-' or '.')
            {
                Flush();
                continue;
            }

            if (char.IsUpper(c) && current.Length > 0)
            {
                char previous = name[i - 1];

                // "accessToken" -> Access|Token, and "APIKey" -> API|Key (an upper run ending
                // where a new capitalised word begins).
                bool afterLowerOrDigit = char.IsLower(previous) || char.IsDigit(previous);
                bool endOfAcronym = char.IsUpper(previous) && i + 1 < name.Length && char.IsLower(name[i + 1]);

                if (afterLowerOrDigit || endOfAcronym)
                    Flush();
            }

            current.Append(c);
        }

        Flush();
        return words;
    }

    private static bool SequenceEqualsIgnoreCase(List<string> candidate, List<string> needle)
    {
        if (candidate.Count != needle.Count)
            return false;

        for (int i = 0; i < candidate.Count; i++)
        {
            if (!string.Equals(candidate[i], needle[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>Whether <paramref name="needle"/>'s words appear consecutively in <paramref name="candidate"/>.</summary>
    private static bool ContainsSequence(List<string> candidate, List<string> needle)
    {
        if (needle.Count > candidate.Count)
            return false;

        for (int offset = 0; offset <= candidate.Count - needle.Count; offset++)
        {
            bool matched = true;
            for (int i = 0; i < needle.Count; i++)
            {
                if (!string.Equals(candidate[offset + i], needle[i], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets or sets the format string for masked sensitive values.
    /// </summary>
    /// <remarks>
    /// Default is "***MASKED***". This value is used to replace sensitive parameter values in logs.
    /// </remarks>
    public string MaskedValueFormat { get; set; } = "***MASKED***";

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingConfiguration"/> class.
    /// </summary>
    public LoggingConfiguration()
    {
        // Add common sensitive parameter names by default
        _sensitiveParameterNames.Add("Password");
        _sensitiveParameterNames.Add("Secret");
        _sensitiveParameterNames.Add("Token");
        _sensitiveParameterNames.Add("ApiKey");
        _sensitiveParameterNames.Add("ApiSecret");
        _sensitiveParameterNames.Add("Credentials");
        _sensitiveParameterNames.Add("PrivateKey");
    }

    /// <summary>
    /// Adds parameter names to the sensitive set.
    /// </summary>
    /// <param name="names">The parameter names to mark as sensitive.</param>
    /// <returns>This configuration instance for chaining.</returns>
    public LoggingConfiguration WithSensitiveParameters(params string[] names)
    {
        if (names is not null)
        {
            foreach (var name in names)
            {
                if (!string.IsNullOrEmpty(name))
                    _sensitiveParameterNames.Add(name);
            }
        }
        return this;
    }

    /// <summary>
    /// Configures the slow query threshold.
    /// </summary>
    /// <param name="threshold">The threshold for slow query logging.</param>
    /// <returns>This configuration instance for chaining.</returns>
    public LoggingConfiguration WithSlowQueryThreshold(TimeSpan threshold)
    {
        SlowQueryThreshold = threshold;
        return this;
    }

    /// <summary>
    /// Configures the minimum log level.
    /// </summary>
    /// <param name="level">The minimum log level.</param>
    /// <returns>This configuration instance for chaining.</returns>
    public LoggingConfiguration WithMinimumLogLevel(LogLevel level)
    {
        MinimumLogLevel = level;
        return this;
    }
}
