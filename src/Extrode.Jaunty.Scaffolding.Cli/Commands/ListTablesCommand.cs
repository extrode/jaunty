using System.CommandLine;

using Extrode.Jaunty.Scaffolding.Abstractions;
using Extrode.Jaunty.Scaffolding.Configuration;

namespace Extrode.Jaunty.Scaffolding.Cli.Commands;

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
            // AUD-R35-266: AutoDetect belongs in the list. It is both a valid value and this
            // option's own default, and omitting it left --help unable to say what the default was.
            Description = "Database provider (AutoDetect, SqlServer, PostgreSql, MySql, SQLite); default AutoDetect",
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
                // them here. AUD-R38-043: the readers are the only filter. A second pass here
                // compared against each table's Schema, which MySQL and SQLite report as empty, so
                // it dropped every table the MySQL reader had accepted for its database name and
                // disagreed with scaffold, which has no such pass.
                IReadOnlyList<(string Schema, string Table)> tables = await scaffolder.ListTablesAsync(
                    connection,
                    provider,
                    schemas.Length > 0 ? new SchemaReaderOptions { IncludeSchemas = schemas } : null,
                    cancellationToken).ConfigureAwait(false);

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
            // AUD-R35-267: cancellation named explicitly, so both commands report a Ctrl-C the same
            // way and the message does not depend on which exception type the cancelled operation
            // happened to throw.
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Error: Operation canceled.");
                return 1;
            }
            // AUD-R38-108: the same flattened cause chain scaffold reports, not the outer message alone.
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {Scaffolder.Describe(ex)}");
                return 1;
            }
        });
    }
}
