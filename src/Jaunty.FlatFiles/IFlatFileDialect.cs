using Jaunty.Dialects;

namespace Jaunty.FlatFiles;

/// <summary>
/// Extends <see cref="ISqlDialect"/> with flat-file-specific SQL generation.
/// </summary>
public interface IFlatFileDialect : ISqlDialect
{
    /// <summary>
    /// Generates SQL to create a VIEW over a file source.
    /// For DuckDB: <c>CREATE OR REPLACE VIEW tableName AS SELECT * FROM read_csv_auto(...)</c>
    /// </summary>
    /// <param name="source">The file source to register.</param>
    /// <returns>The CREATE VIEW SQL statement.</returns>
    string GenerateCreateViewSql(IFileSource source);

    /// <summary>
    /// Generates SQL to create a TABLE (preloaded copy) from a file source.
    /// For DuckDB: <c>CREATE OR REPLACE TABLE tableName AS SELECT * FROM read_csv_auto(...)</c>
    /// </summary>
    /// <param name="source">The file source to register.</param>
    /// <returns>The CREATE TABLE AS SQL statement.</returns>
    string GenerateCreateTableAsSql(IFileSource source);

    /// <summary>
    /// Generates SQL to promote a VIEW to a TABLE for mutation support.
    /// </summary>
    /// <param name="source">The file source to promote.</param>
    /// <returns>SQL statements to drop the view and create a table.</returns>
    string GeneratePromoteToTableSql(IFileSource source);

    /// <summary>
    /// Generates SQL to export a table to a file via COPY TO.
    /// </summary>
    /// <param name="tableName">The table name to export.</param>
    /// <param name="outputPath">The output file path.</param>
    /// <param name="format">The output file format.</param>
    /// <returns>The COPY TO SQL statement.</returns>
    string GenerateCopyToSql(string tableName, string outputPath, FileFormat format);
}
