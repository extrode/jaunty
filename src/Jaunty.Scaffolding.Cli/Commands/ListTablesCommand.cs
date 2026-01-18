using System.CommandLine;

using Jaunty.Scaffolding.Configuration;

namespace Jaunty.Scaffolding.Cli.Commands;

internal sealed class ListTablesCommand : Command
{
    public ListTablesCommand() : base("list-tables", "List tables in the database")
    {
        var connectionOption = new Option<string>(
            aliases: ["--connection", "-c"],
            description: "Database connection string")
        { IsRequired = true };

        var providerOption = new Option<DatabaseProvider>(
            aliases: ["--provider", "-p"],
            description: "Database provider (SqlServer, PostgreSql, MySql, SQLite)",
            getDefaultValue: () => DatabaseProvider.AutoDetect);

        var schemasOption = new Option<string[]>(
            "--schemas",
            description: "Filter by schemas")
        { AllowMultipleArgumentsPerToken = true };

        AddOption(connectionOption);
        AddOption(providerOption);
        AddOption(schemasOption);

        this.SetHandler(async (context) =>
        {
            var connection = context.ParseResult.GetValueForOption(connectionOption)!;
            var provider = context.ParseResult.GetValueForOption(providerOption);
            var schemas = context.ParseResult.GetValueForOption(schemasOption) ?? [];

            try
            {
                var scaffolder = new Scaffolder();
                var tables = await scaffolder.ListTablesAsync(
                    connection,
                    provider,
                    context.GetCancellationToken());

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
                var grouped = tables.GroupBy(t => t.Schema);
                foreach (var group in grouped.OrderBy(g => g.Key))
                {
                    var schemaName = string.IsNullOrEmpty(group.Key) ? "(no schema)" : group.Key;
                    Console.WriteLine($"  {schemaName}:");
                    foreach (var table in group.OrderBy(t => t.Table))
                    {
                        Console.WriteLine($"    - {table.Table}");
                    }
                    Console.WriteLine();
                }

                context.ExitCode = 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                context.ExitCode = 1;
            }
        });
    }
}
