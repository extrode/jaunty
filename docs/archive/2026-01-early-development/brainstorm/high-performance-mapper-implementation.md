# High-Performance Direct IDataReader Mapping Implementation

## The Challenge
The key challenge is creating a strongly-typed, reflection-free, high-performance mapping utility that works generically without knowing the specific type at compile time.

## Solution: Code Generation with Source Generators

The most effective approach is to use C# Source Generators to generate type-specific mapping code at compile time:

### 1. Source Generator Implementation

```csharp
// This would be implemented as a Source Generator
[Generator]
public class DataReaderMapperGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        // Find all types that need mapping
        var types = context.Compilation
            .SyntaxTrees
            .SelectMany(tree => tree.GetRoot().DescendantNodes())
            .OfType<ClassDeclarationSyntax>()
            .Where(cls => cls.AttributeLists
                .SelectMany(attrList => attrList.Attributes)
                .Any(attr => attr.Name.ToString().Contains("DataReaderMapper")))
            .ToList();

        foreach (var typeSyntax in types)
        {
            var semanticModel = context.Compilation.GetSemanticModel(typeSyntax.SyntaxTree);
            var typeSymbol = semanticModel.GetDeclaredSymbol(typeSyntax) as INamedTypeSymbol;
            
            if (typeSymbol != null)
            {
                var mapperCode = GenerateMapperCode(typeSymbol);
                context.AddSource($"{typeSymbol.Name}DataReaderMapper.g.cs", mapperCode);
            }
        }
    }

    private string GenerateMapperCode(INamedTypeSymbol typeSymbol)
    {
        var properties = typeSymbol.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(p => p.SetMethod?.DeclaredAccessibility == Accessibility.Public);

        var className = typeSymbol.Name;
        var namespaceName = typeSymbol.ContainingNamespace.ToDisplayString();

        var propertyMappings = new StringBuilder();
        foreach (var prop in properties)
        {
            var columnName = GetColumnName(prop); // Could use attributes like [Column]
            var propertyType = prop.Type.ToDisplayString();
            
            propertyMappings.AppendLine($"        if (reader.GetOrdinal(\"{columnName}\") >= 0 && !reader.IsDBNull(\"{columnName}\"))");
            propertyMappings.AppendLine($"            result.{prop.Name} = reader.Get{GetDataReaderMethod(propertyType)}(\"{columnName}\");");
        }

        return $@"
using System;
using System.Data;

namespace {namespaceName}
{{
    public static partial class DataReaderMapperExtensions
    {{
        public static {className} ReadAs{className}(this IDataReader reader)
        {{
            var result = new {className}();
{propertyMappings.ToString().TrimEnd()}
            return result;
        }}
    }}
}}";
    }

    private string GetColumnName(IPropertySymbol property)
    {
        // Could check for [Column] attribute, or use property name
        return property.Name; // Simplified
    }

    private string GetDataReaderMethod(string propertyType)
    {
        return propertyType switch
        {
            "int" or "int?" => "Int32",
            "long" or "long?" => "Int64", 
            "string" => "String",
            "decimal" or "decimal?" => "Decimal",
            "DateTime" or "DateTime?" => "DateTime",
            "bool" or "bool?" => "Boolean",
            "Guid" or "Guid?" => "Guid",
            _ => "Value" // Fallback
        };
    }

    public void Initialize(GeneratorInitializationContext context) { }
}
```

### 2. Usage with Source Generator

```csharp
// User marks their class for code generation
[DataReaderMapper] // Custom attribute to mark for source generation
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedDate { get; set; }
}

// Usage - completely reflection-free and strongly typed
using var reader = connection.ExecuteReader("SELECT id, name, price, created_date FROM products");
while (reader.Read())
{
    var product = reader.ReadAsProduct(); // Generated method, no reflection!
    // Process product
}
```

## Alternative Solution: Expression Trees with Caching

If source generators aren't available, use compiled expression trees with caching:

```csharp
public static class FastDataReaderMapper<T> where T : new()
{
    private static readonly Func<IDataReader, T> _compiledMapper = CreateMapper();

    private static Func<IDataReader, T> CreateMapper()
    {
        var readerParam = Expression.Parameter(typeof(IDataReader), "reader");
        var newExpr = Expression.New(typeof(T));

        var bindings = new List<MemberAssignment>();
        var properties = typeof(T).GetProperties()
            .Where(p => p.CanWrite && p.SetMethod?.IsPublic == true);

        foreach (var prop in properties)
        {
            var ordinalField = Expression.Field(null, typeof(T).GetField($"_{prop.Name}Ordinal", 
                BindingFlags.Static | BindingFlags.NonPublic) ?? CreateOrdinalField(prop.Name));

            var ordinalExpr = Expression.Field(null, $"_{prop.Name}Ordinal");
            var isDBNullCall = Expression.Call(readerParam, "IsDBNull", null, ordinalExpr);
            
            var getValueCall = CreateValueExpression(readerParam, ordinalExpr, prop.PropertyType);
            
            var condition = Expression.Condition(
                isDBNullCall,
                Expression.Default(prop.PropertyType),
                getValueCall
            );

            bindings.Add(Expression.Bind(prop, condition));
        }

        var memberInit = Expression.MemberInit(newExpr, bindings);
        var lambda = Expression.Lambda<Func<IDataReader, T>>(memberInit, readerParam);

        return lambda.Compile();
    }

    private static Expression CreateValueExpression(Expression readerParam, Expression ordinalExpr, Type targetType)
    {
        // This would create the appropriate GetXXX method call based on target type
        var methodName = GetDataReaderMethod(targetType);
        var method = typeof(IDataReader).GetMethod($"Get{methodName}") 
                    ?? typeof(IDataReader).GetMethod($"GetFieldValue").MakeGenericMethod(targetType);
        
        return Expression.Call(readerParam, method, ordinalExpr);
    }

    public static T Read(IDataReader reader) => _compiledMapper(reader);
}

// Extension method for convenience
public static class DataReaderExtensions
{
    public static T ReadAs<T>(this IDataReader reader) where T : new()
    {
        return FastDataReaderMapper<T>.Read(reader);
    }
}
```

## Third Solution: Interface-Based Approach

Create a strongly-typed interface that the user implements:

```csharp
public interface IDataReaderMapper<T>
{
    T Map(IDataReader reader);
}

// User implements for their specific type
public class ProductDataReaderMapper : IDataReaderMapper<Product>
{
    public Product Map(IDataReader reader)
    {
        return new Product
        {
            Id = reader.GetInt32("id"),
            Name = reader.IsDBNull("name") ? null : reader.GetString("name"),
            Price = reader.GetDecimal("price"),
            CreatedDate = reader.GetDateTime("created_date")
        };
    }
}

// Generic extension using the mapper
public static class DataReaderExtensions
{
    public static T ReadAs<T>(this IDataReader reader) where T : new()
    {
        // Use dependency injection or service locator to get the mapper
        var mapper = GetMapper<T>(); // This would come from DI container
        return mapper.Map(reader);
    }

    private static IDataReaderMapper<T> GetMapper<T>()
    {
        // This would be resolved from DI container
        // In a real implementation, you'd register mappers in your DI container
        if (typeof(T) == typeof(Product))
            return (IDataReaderMapper<T>)(object)new ProductDataReaderMapper();
        
        throw new NotSupportedException($"No mapper registered for type {typeof(T)}");
    }
}
```

## Fourth Solution: Record-Based Approach with Source Generation

Use C# 9+ records with source generation for the cleanest approach:

```csharp
// User defines a record
public record Product(int Id, string Name, decimal Price, DateTime CreatedDate);

// Source generator creates:
public static partial class ProductDataReaderExtensions
{
    public static Product ReadAsProduct(this IDataReader reader)
    {
        return new Product(
            reader.GetInt32("Id"),           // Direct, no reflection
            reader.IsDBNull("Name") ? null : reader.GetString("Name"), // Direct, no reflection  
            reader.GetDecimal("Price"),      // Direct, no reflection
            reader.GetDateTime("CreatedDate") // Direct, no reflection
        );
    }
}
```

## Recommended Approach

The **Source Generator approach** is the best solution because it:

1. **Completely eliminates reflection** - code is generated at compile time
2. **Provides maximum performance** - direct property assignments
3. **Maintains strong typing** - compile-time type safety
4. **Requires minimal user code** - just mark classes with an attribute
5. **Generates optimal code** - no runtime type checking needed
6. **Handles nullable types properly** - generated code includes null checks
7. **Works with any property types** - handles complex mappings at compile time

The source generator analyzes the target type at compile time and generates optimized mapping code that directly calls the appropriate `IDataReader` methods without any reflection or boxing/unboxing overhead.