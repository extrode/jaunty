using System.CommandLine;

using Extrode.Jaunty.Scaffolding.Cli.Commands;

var rootCommand = new RootCommand("Extrode.Jaunty Scaffolding CLI - Generate C# entity classes from database schema");

rootCommand.Subcommands.Add(new ScaffoldCommand());
rootCommand.Subcommands.Add(new ListTablesCommand());

return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
