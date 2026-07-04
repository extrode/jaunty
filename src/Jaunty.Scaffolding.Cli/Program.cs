using System.CommandLine;

using Jaunty.Scaffolding.Cli.Commands;

var rootCommand = new RootCommand("Jaunty Scaffolding CLI - Generate C# entity classes from database schema");

rootCommand.AddCommand(new ScaffoldCommand());
rootCommand.AddCommand(new ListTablesCommand());

return await rootCommand.InvokeAsync(args).ConfigureAwait(false);