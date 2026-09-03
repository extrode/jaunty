# NativeAOT-CustomMapper

Row mapping written by hand and passed in through `CommandOptions<T>.WithMapper(...)` —
no source generator involved in the mapping, and no reflection:

```csharp
static Employee MapEmployee(IDataReader reader) => new()
{
    EmployeeId = reader.GetInt32(reader.GetOrdinal("employee_id")),
    FirstName  = reader.GetString(reader.GetOrdinal("first_name")),
    // ...
};

var options = CommandOptions<Employee>.WithMapper(MapEmployee);
var employees = connection.Query<Employee>("SELECT ...", options: options);
```

This is the escape hatch for the cases the generator does not cover: a type you do not own, a
shape that is not a straight column-to-property map, or a mapping you want to hand-tune. It is
AOT-safe for the same reason the generated mapper is — the ordinals and conversions are
ordinary compiled code.

The generator is still referenced as an analyzer in the `.csproj`. It is not doing the mapping
here; keeping it means the two approaches can coexist in one project, which is the realistic
case.

## Run it

```bash
dotnet run --project samples/NativeAOT-CustomMapper -f net10.0
```

In-memory SQLite, schema and three employees created by the sample, so no database and no
configuration are needed.

## Publish it AOT

```bash
dotnet publish samples/NativeAOT-CustomMapper -c Release -f net10.0 -r linux-x64
```

Substitute your own RID. A clean publish with no `IL2xxx`/`IL3xxx` warnings is the point.

## See also

- [`NativeAOT-Basic`](../NativeAOT-Basic) — the same queries mapped by the source generator
- [`docs/02-architecture/reflection-and-trimming.md`](../../docs/02-architecture/reflection-and-trimming.md)
