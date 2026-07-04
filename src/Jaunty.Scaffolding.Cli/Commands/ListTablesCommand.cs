using System.CommandLine;

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
                IReadOnlyList<(string Schema, string Table)> tables = await scaffolder.ListTablesAsync(
                    connection,
                    provider,
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
