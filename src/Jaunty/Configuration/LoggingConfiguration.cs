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
    /// Comparison is case-insensitive.
    /// </para>
    /// <para>
    /// Common sensitive parameter names include: "Password", "Secret", "Token", "ApiKey", "Credentials".
    /// </para>
    /// </remarks>
    public ISet<string> SensitiveParameterNames => _sensitiveParameterNames;

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
