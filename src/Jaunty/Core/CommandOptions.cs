using System.Data;

namespace Jaunty.Core;

/// <summary>
/// Options for command execution with custom mapper support.
/// </summary>
public readonly struct CommandOptions<T>(
    Func<IDataReader, T>? mapper = null,
    IDbTransaction? transaction = null,
    int? commandTimeout = null,
    CommandType commandType = CommandType.Text)
{
    public readonly Func<IDataReader, T>? Mapper = mapper;
    public readonly IDbTransaction? Transaction = transaction;
    public readonly int? CommandTimeout = commandTimeout;
    public readonly CommandType CommandType = commandType;

    public static CommandOptions<T> WithMapper(Func<IDataReader, T> mapper) => new(mapper: mapper);
    public static CommandOptions<T> WithTransaction(IDbTransaction transaction) => new(transaction: transaction);
    public static CommandOptions<T> WithTimeout(int seconds) => new(commandTimeout: seconds);
    public static CommandOptions<T> AsStoredProcedure() => new(commandType: CommandType.StoredProcedure);
    public static CommandOptions<T> With(Func<IDataReader, T> mapper, IDbTransaction transaction, int timeoutSeconds) => new(mapper, transaction, timeoutSeconds);

    public static implicit operator CommandOptions(CommandOptions<T> options) => new(options.Transaction, options.CommandTimeout, options.CommandType);
}

/// <summary>
/// Options for command execution.
/// </summary>
public readonly struct CommandOptions(
    IDbTransaction? transaction = null,
    int? commandTimeout = null,
    CommandType commandType = CommandType.Text)
{
    public readonly IDbTransaction? Transaction = transaction;
    public readonly int? CommandTimeout = commandTimeout;
    public readonly CommandType CommandType = commandType;

    public static CommandOptions WithTransaction(IDbTransaction transaction) => new(transaction: transaction);
    public static CommandOptions WithTimeout(int seconds) => new(commandTimeout: seconds);
    public static CommandOptions AsStoredProcedure() => new(commandType: CommandType.StoredProcedure);
    public static CommandOptions With(IDbTransaction transaction, int timeoutSeconds) => new(transaction, timeoutSeconds);
}
