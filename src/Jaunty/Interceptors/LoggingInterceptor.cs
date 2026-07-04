using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

using Jaunty.Configuration;

namespace Jaunty.Interceptors;

/// <summary>
/// An <see cref="ICommandInterceptor"/> that logs command execution using <see cref="ILogger"/>.
/// </summary>
/// <remarks>
/// <para>
/// This interceptor provides comprehensive logging for Jaunty command execution including:
/// </para>
/// <list type="bullet">
/// <item><description>SQL command text (configurable)</description></item>
/// <item><description>Parameter values (with sensitive data masking)</description></item>
/// <item><description>Execution duration</description></item>
/// <item><description>Slow query detection and warning</description></item>
/// <item><description>Exception details on failure</description></item>
/// </list>
/// <para>
/// Sensitive parameter names are automatically masked based on <see cref="LoggingConfiguration.SensitiveParameterNames"/>.
/// </para>
/// </remarks>
public sealed class LoggingInterceptor : ISyncCommandInterceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;
    private readonly LoggingConfiguration _config;
    private readonly bool _hasSlowQueryThreshold;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingInterceptor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="config">Logging configuration options.</param>
    public LoggingInterceptor(ILogger<LoggingInterceptor> logger, LoggingConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new LoggingConfiguration();
        _hasSlowQueryThreshold = _config.SlowQueryThreshold > TimeSpan.Zero;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggingInterceptor"/> class with default configuration.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public LoggingInterceptor(ILogger<LoggingInterceptor> logger)
        : this(logger, null)
    {
    }

    /// <inheritdoc/>
    public ValueTask OnCommandExecutingAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (!_logger.IsEnabled(_config.MinimumLogLevel))
            return new ValueTask();

        if (_config.LogSql)
        {
            var parametersMessage = _config.LogParameters && context.Parameters is not null
                ? FormatParameters(context.Parameters)
                : string.Empty;

            _logger.Log(
                _config.MinimumLogLevel,
                "Executing {CommandType}: {CommandText}{Parameters}",
                GetCommandTypeDescription(context.CommandType),
                context.CommandText,
                string.IsNullOrEmpty(parametersMessage) ? string.Empty : " Parameters: " + parametersMessage);
        }

        return new ValueTask();
    }

    /// <inheritdoc/>
    public ValueTask OnCommandExecutedAsync(CommandContext context, CancellationToken cancellationToken)
    {
        if (!_logger.IsEnabled(_config.MinimumLogLevel))
            return new ValueTask();

        var isSlow = _hasSlowQueryThreshold && context.Elapsed > _config.SlowQueryThreshold;
        var level = isSlow ? LogLevel.Warning : _config.MinimumLogLevel;

        if (!IsEnabledAtLevel(level))
            return new ValueTask();

        var message = new StringBuilder();
        message.Append("Completed ");
        message.Append(GetCommandTypeDescription(context.CommandType));
        message.Append(" in ");
        message.Append(context.Elapsed.TotalMilliseconds.ToString("F2"));
        message.Append("ms");

        if (isSlow)
        {
            message.Append(" (SLOW - exceeded ");
            message.Append(_config.SlowQueryThreshold.TotalMilliseconds);
            message.Append("ms threshold)");
        }

        _logger.Log(
            level,
            "Completed {CommandType} in {ElapsedMilliseconds:F2}ms{SlowQueryIndicator}",
            GetCommandTypeDescription(context.CommandType),
            context.Elapsed.TotalMilliseconds,
            isSlow ? $" (SLOW - exceeded {_config.SlowQueryThreshold.TotalMilliseconds}ms threshold)" : "");

        return new ValueTask();
    }

    /// <inheritdoc/>
    public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsEnabledAtLevel(LogLevel.Error))
            return new ValueTask();

        _logger.LogError(
            exception,
            "Failed executing {CommandType} after {ElapsedMilliseconds:F2}ms: {ErrorMessage}",
            GetCommandTypeDescription(context.CommandType),
            context.Elapsed.TotalMilliseconds,
            exception.Message);

        return new ValueTask();
    }

    private bool IsEnabledAtLevel(LogLevel level) => _logger.IsEnabled(level);

    private static string GetCommandTypeDescription(CommandType commandType) => commandType switch
    {
        CommandType.Text => "SQL Text",
        CommandType.StoredProcedure => "Stored Procedure",
        CommandType.TableDirect => "Table Direct",
        _ => commandType.ToString()
    };

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> _propertyCache = new();

    private string FormatParameters(object parameters)
    {
        var sb = new StringBuilder();

        if (parameters is System.Collections.IDictionary dict)
        {
            var first = true;
            foreach (System.Collections.DictionaryEntry entry in dict)
            {
                if (!first) sb.Append(", ");
                first = false;

                var name = entry.Key?.ToString() ?? "(unknown)";
                var value = FormatParameterValue(name, entry.Value);
                sb.Append(name).Append("=").Append(value);
            }
        }
        else
        {
            var properties = _propertyCache.GetOrAdd(parameters.GetType(), GetPublicProperties);

            var first = true;
            foreach (var property in properties)
            {
                if (property.GetIndexParameters().Length > 0)
                    continue;

                if (!first) sb.Append(", ");
                first = false;

                var value = FormatParameterValue(property.Name, property.GetValue(parameters));
                sb.Append(property.Name).Append("=").Append(value);
            }
        }

        return sb.ToString();
    }

#if NET5_0_OR_GREATER
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Used for anonymous types and records whose properties are always preserved by the compiler.")]
#endif
    private static PropertyInfo[] GetPublicProperties(
#if NET5_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
#endif
        Type type)
    {
        // AOT-SAFE: parameter annotated with DynamicallyAccessedMembers(PublicProperties); trimmer preserves the members it reflects over
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    private string FormatParameterValue(string paramName, object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        if (_config.SensitiveParameterNames.Contains(paramName))
            return _config.MaskedValueFormat;

        return value switch
        {
            string s => $"\"{s}\"",
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            bool b => b.ToString().ToLowerInvariant(),
            _ => value.ToString() ?? "NULL"
        };
    }

    /// <inheritdoc/>
    public void OnCommandExecuting(CommandContext context)
        => OnCommandExecutingAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void OnCommandExecuted(CommandContext context)
        => OnCommandExecutedAsync(context, CancellationToken.None).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void OnCommandFailed(CommandContext context, Exception exception)
        => OnCommandFailedAsync(context, exception, CancellationToken.None).GetAwaiter().GetResult();
}
