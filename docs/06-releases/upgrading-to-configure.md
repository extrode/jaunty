# Upgrading to `JauntyConfig.Configure`

> **Warning**
> **This release breaks your build if your code sets any Jaunty mapping setting.** Assigning
> `JauntyConfig.TableNameResolver`, `ColumnNameResolver`, `SchemaNameResolver`,
> `DefaultEnumStorage`, `CopyImportFactory` or any `Reflection*Resolver`, or calling
> `JauntyConfig.RegisterTypeHandler`, `JauntyConfig.RemoveTypeHandler`,
> `JauntyReflectionExtensions.UseReflectionMapping()`, `SpecialTypeMappers.Register()` or
> `JauntyNpgsql.Use()`, no longer compiles. Move those lines into one `JauntyConfig.Configure`
> call at startup, as shown below.

Code that only runs queries, or only sets `Logger`, the capacities, interceptors,
`BulkCopyConfiguration` or dialect registrations, is not affected. Code the source generator wrote
for your entities is not affected either; you do not need to rebuild for it.

## What you will see

```text
error CS0200: Property or indexer 'JauntyConfig.ColumnNameResolver' cannot be assigned to -- it is read only
error CS0117: 'JauntyConfig' does not contain a definition for 'RegisterTypeHandler'
error CS0117: 'JauntyNpgsql' does not contain a definition for 'Use'
error CS7036: There is no argument given that corresponds to the required parameter 'config' of 'JauntyReflectionExtensions.UseReflectionMapping(JauntyConfigBuilder)'
error CS7036: There is no argument given that corresponds to the required parameter 'config' of 'SpecialTypeMappers.Register(JauntyConfigBuilder)'
```

## Why

One Jaunty operation reads these settings more than once: an `Insert` reads the column names for
its parameter values, then again for its SQL text. If another thread changed a setting between
those reads, the SQL and the parameters were built from different settings, and the call failed
with a confusing database error such as "Must add values for the following parameters". Setting
everything once, before the first query, removes that window, and removing the setters makes a
late change a compile error rather than a rare runtime failure. The full reasoning, the options
compared and the measurements are in
[decision 014](../decisions/2026-10-02-014-configuration-is-set-once-at-startup.md).

## What to change

Put every line from the left column into one `Configure` call, as in the right column.

| Before | After, inside `JauntyConfig.Configure(c => { ... })` |
|---|---|
| `JauntyConfig.TableNameResolver = f;` | `c.TableNameResolver = f;` |
| `JauntyConfig.SchemaNameResolver = f;` | `c.SchemaNameResolver = f;` |
| `JauntyConfig.ColumnNameResolver = f;` | `c.ColumnNameResolver = f;` |
| `JauntyConfig.DefaultEnumStorage = s;` | `c.DefaultEnumStorage = s;` |
| `JauntyConfig.CopyImportFactory = f;` | `c.CopyImportFactory = f;` |
| `JauntyConfig.ReflectionMapperResolver = f;` (and the other 6 `Reflection*Resolver`s, `SpecialTypeMapperResolver`) | `c.ReflectionMapperResolver = f;` |
| `JauntyConfig.RegisterTypeHandler<T>(fromDb, toDb);` | `c.RegisterTypeHandler<T>(fromDb, toDb);` |
| `JauntyConfig.RegisterTypeHandler(new MyHandler());` | `c.RegisterTypeHandler(new MyHandler());` |
| `JauntyReflectionExtensions.UseReflectionMapping();` | `c.UseReflectionMapping();` (usually not needed, see below) |
| `SpecialTypeMappers.Register();` | `SpecialTypeMappers.Register(c);` (`UseReflectionMapping` already does this) |
| `JauntyNpgsql.Use();` | `c.UseNpgsqlCopy();` |
| `JauntyConfig.RemoveTypeHandler<T>();` | No replacement: don't register it. Handlers are fixed at startup. |

Reading a setting is unchanged: `JauntyConfig.ColumnNameResolver` still returns the resolver.

### A console app or worker

```csharp
// Before
JauntyConfig.TableNameResolver = t => Snake(t.Name);
JauntyConfig.ColumnNameResolver = Snake;
JauntyConfig.RegisterTypeHandler(new MoneyHandler());
JauntyNpgsql.Use();

// After
JauntyConfig.Configure(c =>
{
    c.TableNameResolver = t => Snake(t.Name);
    c.ColumnNameResolver = Snake;
    c.RegisterTypeHandler(new MoneyHandler());
    c.UseNpgsqlCopy();
});
```

### ASP.NET Core

Call it in `Program.cs` before `builder.Build()` runs anything that queries, and before any hosted
service starts:

```csharp
var builder = WebApplication.CreateBuilder(args);

JauntyConfig.Configure(c =>
{
    c.TableNameResolver = t => Snake(t.Name);
    c.ColumnNameResolver = Snake;
});

builder.Services.AddJauntyLogging();   // runtime settings are unchanged
var app = builder.Build();
```

If a setting comes from configuration, read it first and guard the call (see the next section):

```csharp
string schema = builder.Configuration["Db:Schema"] ?? "";
if (!JauntyConfig.IsConfigured)
    JauntyConfig.Configure(c => c.SchemaNameResolver = _ => schema);
```

### Reflection mapping

If your project references Extrode.Jaunty.Extensions.Reflection, `Configure` switches reflection
mapping on before your callback runs, exactly as Jaunty did on its own before. You only need
`c.UseReflectionMapping()` where that automatic step cannot run, which is a trimmed or NativeAOT
publish, or to put the hooks back after replacing one of them by hand. If you never call
`Configure`, Jaunty still switches reflection mapping on by itself on first use.

## Rules to know

1. **Call `Configure` once, at startup, before anything touches Jaunty.** Running a query, a Fluent
   query, or reading a generated member such as `Product.Jaunty.TableName` counts as first use.
   `Configure` after that throws:

   ```text
   InvalidOperationException: JauntyConfig.Configure was called after Extrode.Jaunty had already
   read its settings (a query, a generated TableName, or a Fluent query ran first).
   ```

2. **A second call with the same settings does nothing; with different settings it throws.** "The
   same" means every delegate refers to the same method on the same target. Method groups
   (`c.ColumnNameResolver = Snake;`) and lambdas that capture nothing compare equal. A lambda that
   captures a variable is a new delegate on every call, so guard it with
   `if (!JauntyConfig.IsConfigured)`.

3. **Settings cannot change while the app runs.** Per-tenant or per-request table names are not
   supported: they would need settings scoped to a connection, which Jaunty does not have. If you
   switched a resolver at runtime, the usual replacement is one resolver that handles every case,
   or separate schemas chosen by the connection string.

## Integration tests that start the app more than once

`WebApplicationFactory`, Aspire and similar hosts can run `Program.cs` several times in one test
process. That keeps working when the settings are the same each time (rule 2). If they differ, or
use captured values, guard the call with `IsConfigured`. The first host's settings then apply to
every host in that process.

## Your own unit tests

`JauntyConfig.Reset()` restores the defaults and allows `Configure` again. It is for tests only,
and it is not safe while another test is using Jaunty, so put tests that call `Reset()` and then
`Configure` in a collection that does not run in parallel (xUnit:
`[CollectionDefinition("Jaunty config", DisableParallelization = true)]`).

```csharp
[Collection("Jaunty config")]
public sealed class SnakeCaseNamingTests : IDisposable
{
    public SnakeCaseNamingTests()
    {
        JauntyConfig.Reset();
        JauntyConfig.Configure(c => c.ColumnNameResolver = Snake);
    }

    public void Dispose() => JauntyConfig.Reset();
}
```

A test that used `RemoveTypeHandler` to undo its own registration now gets the same effect from
`Reset()` in its cleanup.

## Packages that configure Jaunty

A library that set Jaunty settings in its own startup method, such as a type handler package with
a `MyHandlers.Use()` that called `JauntyConfig.RegisterTypeHandler`, has to become a builder
extension, and its users call it inside `Configure`:

```csharp
// Before, in the package
public static void Use() => JauntyConfig.RegisterTypeHandler(new MoneyHandler());

// After, in the package
public static JauntyConfigBuilder UseMoneyHandlers(this JauntyConfigBuilder c)
    => c.RegisterTypeHandler(new MoneyHandler());

// After, in the app
JauntyConfig.Configure(c => c.UseMoneyHandlers());
```

`Extrode.Jaunty.Extensions.Npgsql` and `Extrode.Jaunty.Extensions.Reflection` made this change
themselves: `JauntyNpgsql.Use()` is now `c.UseNpgsqlCopy()`, and
`JauntyReflectionExtensions.UseReflectionMapping()` is now `c.UseReflectionMapping()`.
`UseNativeBulkCopy()` is unchanged, because bulk copy settings stay settable at runtime.
