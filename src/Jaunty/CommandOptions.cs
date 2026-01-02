using System.Data;

namespace Jaunty;

/// <summary>
/// Options for command execution.
/// </summary>
public readonly struct CommandOptions(IDbTransaction? transaction = null, int? commandTimeout = null)
{
    public readonly IDbTransaction? Transaction = transaction;
    public readonly int? CommandTimeout = commandTimeout;

    public static CommandOptions WithTransaction(IDbTransaction transaction) => new(transaction, null);
    public static CommandOptions WithTimeout(int seconds) => new(null, seconds);
    public static CommandOptions With(IDbTransaction transaction, int timeoutSeconds) => new(transaction, timeoutSeconds);
}
