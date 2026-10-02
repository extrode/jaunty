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

Reading a setting still works the same way: `JauntyConfig.ColumnNameResolver` still returns the
resolver. But a read now counts as first use (rule 1), so code that logs a setting such as
`JauntyConfig.DefaultEnumStorage` before calling `Configure` must move after it.

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

If a setting comes from configuration, read it first and use `TryConfigure` (see the rules below):

```csharp
string schema = builder.Configuration["Db:Schema"] ?? "";
JauntyConfig.TryConfigure(c => c.SchemaNameResolver = _ => schema);
```

### Reflection mapping

If your project references Extrode.Jaunty.Extensions.Reflection, `Configure` switches reflection
mapping on before your callback runs, exactly as Jaunty did on its own before. You only need
`c.UseReflectionMapping()` where that automatic step cannot run, which is a trimmed or NativeAOT
publish, or to put the hooks back after replacing one of them by hand. If you never call
`Configure`, Jaunty still switches reflection mapping on by itself on first use.

## Rules to know

1. **Call `Configure` once, at startup, before anything touches Jaunty.** Running a query, a Fluent
   query, reading a generated member such as `Product.Jaunty.TableName`, or reading a mapping
   setting such as `JauntyConfig.DefaultEnumStorage` counts as first use. `Configure` after that
   throws:

   ```text
   InvalidOperationException: JauntyConfig.Configure was called after Extrode.Jaunty had already
   read its settings (a query, a generated TableName, a Fluent query, or a read of a JauntyConfig
   mapping setting ran first).
   ```

   A query running on another thread while `Configure` runs is detected the same way: `Configure`
   throws, puts the previous settings back, and that query may fail too.

2. **A second call with the same settings does nothing; with different settings it throws.** "The
   same" means every delegate refers to the same method on the same target. Method groups
   (`c.ColumnNameResolver = Snake;`) and lambdas that capture nothing compare equal, and type
   handlers compare by their type, so `c.RegisterTypeHandler(new MoneyHandler())` is the same on
   every run. That also means two handlers of one class built with different constructor
   arguments count as the same, and the second call silently keeps the first. A lambda that captures a variable is a new delegate on every call, so make that call
   through `JauntyConfig.TryConfigure`, which applies the first call and skips the rest without
   running them. A repeated `Configure` runs your callback again to compare, so keep it free of
   side effects.

3. **Settings cannot change while the app runs.** Per-tenant or per-request table names are not
   supported: they would need settings scoped to a connection, which Jaunty does not have. If you
   switched a resolver at runtime, the usual replacement is one resolver that handles every case,
   or separate schemas chosen by the connection string.

## Integration tests that start the app more than once

`WebApplicationFactory`, Aspire and similar hosts can run `Program.cs` several times in one test
process. That keeps working when the settings are the same each time (rule 2). If they differ, or
use captured values, call `TryConfigure` instead of `Configure`; it is safe when hosts start on
parallel threads. The first host's settings then apply to every host in that process.

Rule 1 applies to the whole test process: if any test runs a query before the first host starts,
every later `Configure` or `TryConfigure` throws, and which test runs first depends on ordering. Set
the settings once for the test assembly before any test runs, for example in a module initializer,
and let the hosts' own call become a no-op:

```csharp
internal static class JauntyTestSetup
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Init() => JauntyConfig.TryConfigure(Startup.ConfigureJaunty);
}
```

where `Startup.ConfigureJaunty` is the same method `Program.cs` passes to `Configure`. Two limits:

- This works only if `Program.cs` passes that same method, or calls `TryConfigure`. If
  `Program.cs` builds its settings from `builder.Configuration`, the module initializer cannot see
  them, and a plain `Configure` in `Program.cs` then throws "different settings". Use
  `TryConfigure` in `Program.cs`, and the module initializer's settings win.
- `[ModuleInitializer]` needs C# 9. On net472 or netstandard2.0 declare the attribute yourself
  (`namespace System.Runtime.CompilerServices { internal sealed class ModuleInitializerAttribute : Attribute { } }`)
  or use a polyfill package.

Fixtures in a separate shared assembly that query before the test assembly's module initializer
runs are not supported: the order across assemblies cannot be controlled. Serialize those fixtures
or configure from the shared assembly instead.

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
