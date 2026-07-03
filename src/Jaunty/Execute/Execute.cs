using System.Data;

using Jaunty.Core;

namespace Jaunty;

public static partial class Jaunty
{
    /// <summary>
    /// Executes a SQL command (INSERT, UPDATE, DELETE, DDL, etc.) and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <returns>The number of rows affected by the command. For INSERT, UPDATE, DELETE, this is the affected row count. For DDL, the return value is database-dependent.</returns>
    /// <remarks>
    /// <para>
    /// This method executes arbitrary SQL that does not return a result set, such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>INSERT statements</description></item>
    /// <item><description>UPDATE statements</description></item>
    /// <item><description>DELETE statements</description></item>
    /// <item><description>CREATE TABLE / ALTER TABLE (DDL)</description></item>
    /// <item><description>TRUNCATE</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Simple INSERT
    /// int rowsAffected = connection.Execute(
    ///     "INSERT INTO products (name, price) VALUES ('Widget', 99.99)");
    /// </code>
    /// </example>
    public static int Execute(this IDbConnection connection, string sql)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return ExecuteNonQueryCore(connection, sql, null, default, CommandType.Text);
    }

    /// <summary>
    /// Executes a SQL command with parameters and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <returns>The number of rows affected by the command.</returns>
    public static int Execute(this IDbConnection connection, string sql, object parameters)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return ExecuteNonQueryCore(connection, sql, parameters, default, CommandType.Text);
    }

    /// <summary>
    /// Executes a SQL command with command options and returns the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="options">Command execution options, including transaction, timeout, and command type.</param>
    /// <returns>The number of rows affected by the command.</returns>
    public static int Execute(this IDbConnection connection, string sql, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
#endif
        return ExecuteNonQueryCore(connection, sql, null, options, options.CommandType);
    }

    /// <summary>
    /// Executes a SQL command with parameters and command options, returning the number of rows affected.
    /// </summary>
    /// <param name="connection">The database connection to execute the command against.</param>
    /// <param name="sql">The SQL command to execute.</param>
    /// <param name="parameters">An anonymous object or dictionary containing parameter values.</param>
    /// <param name="options">Command execution options, including transaction, timeout, and command type.</param>
    /// <returns>The number of rows affected by the command.</returns>
    public static int Execute(this IDbConnection connection, string sql, object parameters, CommandOptions options)
    {
#if NET8_0_OR_GREATER
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(sql);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);
#else
        if (connection is null) throw new ArgumentNullException(nameof(connection));
        if (sql is null) throw new ArgumentNullException(nameof(sql));
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL cannot be empty or whitespace.", nameof(sql));
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
#endif
        return ExecuteNonQueryCore(connection, sql, parameters, options, options.CommandType);
    }
}
