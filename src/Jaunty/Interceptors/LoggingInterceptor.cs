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
        var isSlow = _hasSlowQueryThreshold && context.Elapsed > _config.SlowQueryThreshold;
        var level = isSlow ? LogLevel.Warning : _config.MinimumLogLevel;

        if (!IsEnabledAtLevel(level))
            return new ValueTask();

        // AUD-R25: LogExecutionTime was declared, defaulted to true and documented, but had no
        // consumer anywhere - the elapsed time was formatted in regardless, so setting it to false
        // did nothing. Its three sibling flags (LogSql, LogParameters, SlowQueryThreshold) are all
        // honoured. Slow-query *detection* deliberately still works when it is off: the flag governs
        // whether the measurement appears in the message, not whether a slow query is worth warning
        // about, and the threshold quoted in that warning is configuration rather than a measurement.
        if (_config.LogExecutionTime)
        {
            _logger.Log(
                level,
                "Completed {CommandType} in {ElapsedMilliseconds:F2}ms{SlowQueryIndicator}",
                GetCommandTypeDescription(context.CommandType),
                context.Elapsed.TotalMilliseconds,
                isSlow ? $" (SLOW - exceeded {_config.SlowQueryThreshold.TotalMilliseconds}ms threshold)" : "");
        }
        else
        {
            _logger.Log(
                level,
                "Completed {CommandType}{SlowQueryIndicator}",
                GetCommandTypeDescription(context.CommandType),
                isSlow ? $" (SLOW - exceeded {_config.SlowQueryThreshold.TotalMilliseconds}ms threshold)" : "");
        }

        return new ValueTask();
    }

    /// <inheritdoc/>
    public ValueTask OnCommandFailedAsync(CommandContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (!IsEnabledAtLevel(LogLevel.Error))
            return new ValueTask();

        if (_config.LogExecutionTime)
        {
            _logger.LogError(
                exception,
                "Failed executing {CommandType} after {ElapsedMilliseconds:F2}ms: {ErrorMessage}",
                GetCommandTypeDescription(context.CommandType),
                context.Elapsed.TotalMilliseconds,
                exception.Message);
        }
        else
        {
            _logger.LogError(
                exception,
                "Failed executing {CommandType}: {ErrorMessage}",
                GetCommandTypeDescription(context.CommandType),
                exception.Message);
        }

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
    [UnconditionalSuppressMessage("AOT", "IL2070", Justification = "Reflects over the properties of whatever object was passed as command parameters, which is a runtime Type and so cannot be annotated. Anonymous types and records are preserved by the compiler, but a named POCO passed as parameters is not, and under trimming or NativeAOT its properties may be removed - the log line then omits them. Logging is diagnostic and never affects the query, so this degrades output rather than behaviour. Suppressed pending a source-generated parameter-binding path (spec 009, the same limitation ParameterCache.BuildMetadata carries); callers using NativeAOT publish today must ensure their parameter POCOs are otherwise rooted if they want parameter logging.")]
#endif
    private static PropertyInfo[] GetPublicProperties(Type type)
    {
        // AOT-SAFE: reflection over an unannotated runtime Type, suppressed above with the trimming
        // limitation stated. The DynamicallyAccessedMembers annotation this parameter used to carry
        // was removed on 2026-07-30: the sole call site passes parameters.GetType(), so no caller
        // ever supplied a statically-known type for the trimmer to act on, and converting the
        // annotated method to a delegate at the GetOrAdd above raised IL2111 for nothing.
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
    }

    private string FormatParameterValue(string paramName, object? value)
    {
        if (value is null || value == DBNull.Value)
            return "NULL";

        // AUD-R26: this was two exact-equality lookups, one raw and one prefix-stripped, so the
        // seeded "Password" masked a parameter called exactly Password and not NewPassword,
        // PasswordHash or password_hash. The configuration now owns the definition of "sensitive"
        // and handles the prefix, separators and whole-word matching in one place.
        if (_config.IsSensitiveParameter(paramName))
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
