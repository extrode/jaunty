using System.CommandLine;

using Jaunty.Scaffolding.Abstractions;
using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Commands;

internal sealed class ListTablesCommand : Command
{
    public ListTablesCommand() : base("list-tables", "List tables in the database")
    {
        var connectionOption = new Option<string>("--connection", "-c")
        {
            Description = "Database connection string",
            Required = true
        };

        var providerOption = new Option<DatabaseProvider>("--provider", "-p")
        {
            Description = "Database provider (SqlServer, PostgreSql, MySql, SQLite)",
            DefaultValueFactory = _ => DatabaseProvider.AutoDetect
        };

        var schemasOption = new Option<string[]>("--schemas")
        {
            Description = "Filter by schemas",
            AllowMultipleArgumentsPerToken = true
        };

        Options.Add(connectionOption);
        Options.Add(providerOption);
        Options.Add(schemasOption);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var connection = parseResult.GetValue(connectionOption)!;
            DatabaseProvider provider = parseResult.GetValue(providerOption);
            var schemas = parseResult.GetValue(schemasOption) ?? [];

            try
            {
                var scaffolder = new Scaffolder();
                // AUD-R35-079: push --schemas down to the reader so the unwanted schemas' tables
                // are never read, rather than reading every table in the database and discarding
                // them here. The client-side pass below is still needed: SQLite ignores
                // IncludeSchemas (it has no schemas) and MySQL treats it as an accept/reject on the
                // attached database name, so neither narrows a multi-schema listing on its own.
                IReadOnlyList<(string Schema, string Table)> tables = await scaffolder.ListTablesAsync(
                    connection,
                    provider,
                    schemas.Length > 0 ? new SchemaReaderOptions { IncludeSchemas = schemas } : null,
                    cancellationToken).ConfigureAwait(false);

                // Filter by schemas if specified
                if (schemas.Length > 0)
                {
                    tables = [.. tables
                        .Where(t => schemas.Contains(t.Schema, StringComparer.OrdinalIgnoreCase) ||
                                   (string.IsNullOrEmpty(t.Schema) && schemas.Contains("", StringComparer.OrdinalIgnoreCase)))];
                }

                Console.WriteLine($"Found {tables.Count} table(s):");
                Console.WriteLine();

                // Group by schema
                IEnumerable<IGrouping<string, (string Schema, string Table)>> grouped = tables.GroupBy(t => t.Schema);
                foreach (IGrouping<string, (string Schema, string Table)>? group in grouped.OrderBy(g => g.Key))
                {
                    var schemaName = string.IsNullOrEmpty(group.Key) ? "(no schema)" : group.Key;
                    Console.WriteLine($"  {schemaName}:");
                    foreach ((string Schema, string Table) table in group.OrderBy(t => t.Table))
                    {
                        Console.WriteLine($"    - {table.Table}");
                    }
                    Console.WriteLine();
                }

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });
    }
}
