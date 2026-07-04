# Jaunty.Scaffolding.Cli

`dotnet-jaunty` — a .NET global tool that scaffolds C# entity classes from a live database schema.

## Installation

```shell
dotnet tool install --global Beparey.Jaunty.Scaffolding.Cli
```

Or from a local build:

```shell
dotnet pack src/Jaunty.Scaffolding.Cli/Jaunty.Scaffolding.Cli.csproj
dotnet tool install --global --add-source ./nupkg Beparey.Jaunty.Scaffolding.Cli
```

## Commands

### `scaffold`

Generates entity classes from database schema.

```shell
dotnet-jaunty scaffold --connection <conn> [options]
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--connection` | `-c` | *(required)* | Database connection string |
| `--provider` | `-p` | `AutoDetect` | `SqlServer`, `PostgreSql`, `MySql`, `SQLite` |
| `--output` | `-o` | `./Entities` | Output directory |
| `--namespace` | `-n` | `Generated.Entities` | Namespace for generated classes |
| `--tables` | | *(all)* | Only scaffold these tables (repeatable) |
| `--exclude-tables` | | | Tables to skip (repeatable) |
| `--schemas` | | *(all)* | Restrict to these schemas (repeatable) |
| `--no-table-attribute` | | | Omit `[Table]` attributes |
| `--no-column-attribute` | | | Omit `[Column]` attributes |
| `--no-key-attribute` | | | Omit `[Key]` attributes |
| `--no-database-generated` | | | Omit `[DatabaseGenerated]` attributes |
| `--no-nullable` | | | Disable nullable reference types |
| `--partial` | | | Emit `partial` classes |
| `--no-singularize` | | | Keep table names as-is (no singularization) |
| `--class-prefix` | | | Prefix to prepend to class names |
| `--class-suffix` | | | Suffix to append to class names |
| `--data-annotations` | | | Include `System.ComponentModel.DataAnnotations` attributes |
| `--block-namespace` | | | Use block-scoped namespaces |
| `--force` | | | Overwrite existing files |
| `--dry-run` | | | Preview output without writing files |
| `--verbose` | | | Print detailed progress |

**Example — scaffold all tables from a SQLite database:**

```shell
dotnet-jaunty scaffold \
  --connection "$DB_CONN" \
  --provider SQLite \
  --output src/MyApp/Entities \
  --namespace MyApp.Data
```

**Example — dry-run with table filter:**

```shell
dotnet-jaunty scaffold \
  --connection "$DB_CONN" \
  --tables orders order_details \
  --dry-run --verbose
```

### `list-tables`

Lists all tables visible to the provided connection.

```shell
dotnet-jaunty list-tables --connection <conn> [--provider <p>] [--schemas <s>...]
```

| Option | Short | Default | Description |
|---|---|---|---|
| `--connection` | `-c` | *(required)* | Database connection string |
| `--provider` | `-p` | `AutoDetect` | Database provider |
| `--schemas` | | *(all)* | Filter output to these schemas |

**Example:**

```shell
dotnet-jaunty list-tables --connection "$DB_CONN" --provider PostgreSql
```

## Notes

- The CLI is published as a NativeAOT executable; no .NET runtime is required on the target machine after publish.
- Provider is auto-detected from the connection string when `--provider` is omitted. Explicit `--provider` is recommended for ambiguous strings.
- Connection strings should be supplied via environment variables or a secrets manager, not passed directly in shell history.
