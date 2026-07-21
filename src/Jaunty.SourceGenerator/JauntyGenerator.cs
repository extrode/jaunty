using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Jaunty.SourceGenerator;

/// <summary>
/// Source generator that creates entity mappers for classes marked with table mapping attributes.
/// </summary>
[Generator]
public class JauntyGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Initializes the source generator by registering syntax providers and source output callbacks.
    /// </summary>
    /// <param name="context">The initialization context for configuring the generator.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => GetSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        IncrementalValueProvider<(Compilation, ImmutableArray<ClassDeclarationSyntax>)> compilationAndClasses
            = context.CompilationProvider.Combine(classDeclarations.Collect());

        context.RegisterSourceOutput(compilationAndClasses,
            static (spc, source) => Execute(source.Item1, source.Item2, spc));
    }

    /// <summary>
    /// Returns <see langword="true"/> when a syntax node is a class declaration with at least one attribute list.
    /// This is a fast syntactic pre-filter applied before the more expensive semantic check.
    /// </summary>
    /// <param name="node">The syntax node to inspect.</param>
    static bool IsSyntaxTargetForGeneration(SyntaxNode node)
        => node is ClassDeclarationSyntax { AttributeLists.Count: > 0 };

    /// <summary>
    /// Returns the <see cref="ClassDeclarationSyntax"/> when the class carries a recognized
    /// <c>[Table]</c> attribute (Jaunty or DataAnnotations), otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="context">The generator syntax context supplying semantic information.</param>
    static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;

        if (context.SemanticModel.GetDeclaredSymbol(classDeclaration) is not INamedTypeSymbol classSymbol)
            return null;

        return HasAttribute(classSymbol, "TableAttribute") ? classDeclaration : null;
    }

    /// <summary>
    /// Iterates over discovered entity classes and emits a generated mapper source file for each.
    /// </summary>
    /// <param name="compilation">The current compilation.</param>
    /// <param name="classes">The set of candidate class declarations collected by the syntax provider.</param>
    /// <param name="context">The source production context used to add generated source files.</param>
    static void Execute(Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes, SourceProductionContext context)
    {
        if (classes.IsDefaultOrEmpty)
            return;

        foreach (ClassDeclarationSyntax? classSyntax in classes.Distinct())
        {
            SemanticModel model = compilation.GetSemanticModel(classSyntax.SyntaxTree);
            if (model.GetDeclaredSymbol(classSyntax) is not INamedTypeSymbol classSymbol)
                continue;

            var source = GenerateMapper(classSymbol);
            context.AddSource($"{GetHintName(classSymbol)}.JauntyMapper.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    /// <summary>
    /// Builds a collision-resistant hint name for the generated source file from the entity's
    /// fully-qualified type name, so two <c>[Table]</c> classes with the same simple name in
    /// different namespaces don't produce a duplicate hint name (which fails the build).
    /// </summary>
    /// <param name="classSymbol">The entity class symbol.</param>
    private static string GetHintName(INamedTypeSymbol classSymbol)
    {
        var fullName = classSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (fullName.StartsWith("global::", StringComparison.Ordinal))
            fullName = fullName.Substring("global::".Length);

        var sb = new StringBuilder(fullName.Length);
        foreach (char c in fullName)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

        return sb.ToString();
    }

    /// <summary>
    /// Generates the complete source text for a Jaunty entity mapper partial class,
    /// including <c>ReadEntity</c>, <c>BindInsert</c>, <c>BindUpdate</c>, <c>BindDelete</c>,
    /// an ordinal-caching <c>OrdinalMap</c>, and column-info static properties.
    /// </summary>
    /// <param name="classSymbol">The named type symbol for the entity class to generate a mapper for.</param>
    /// <returns>The generated C# source code as a string.</returns>
    static string GenerateMapper(INamedTypeSymbol classSymbol)
    {
        var namespaceName = classSymbol.ContainingNamespace.ToDisplayString();
        var isGlobalNamespace = classSymbol.ContainingNamespace.IsGlobalNamespace;
        var className = classSymbol.Name;

        var allProperties = classSymbol.GetMembers().OfType<IPropertySymbol>()
            .Where(p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public)
            .ToList();

        var properties = new List<PropertyMetadata>();
        foreach (IPropertySymbol? prop in allProperties)
        {
            // Support [Ignore] and [NotMapped]
            if (HasAttribute(prop, "IgnoreAttribute") || HasAttribute(prop, "NotMappedAttribute")) continue;

            // A get-only property has no SetMethod at all; an init-only property has one, but it
            // can only be assigned inside an object initializer, not via the post-construction
            // `entity.Prop = value` assignments this generator emits in ReadEntity/CreateRowMapper/
            // the ColumnInfo and EntityColumnInfo setter lambdas. Either would emit an assignment
            // that fails to compile (CS0200/CS8852), so treat both as implicitly [Ignore]d.
            if (prop.SetMethod is null || prop.SetMethod.IsInitOnly) continue;

            // Support [Column] from both
            AttributeData? columnAttr = GetAttribute(prop, "ColumnAttribute");
            var columnName = columnAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString() ?? prop.Name;

            // Support [Key] from both, plus conventions
            var isKey = HasAttribute(prop, "KeyAttribute") || prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || prop.Name.Equals($"{className}Id", StringComparison.OrdinalIgnoreCase);

            // Support [DatabaseGenerated] from both
            AttributeData? dbGenAttr = GetAttribute(prop, "DatabaseGeneratedAttribute");
            var isIdentity = false;
            var isComputed = false;
            if (dbGenAttr != null)
            {
                TypedConstant arg = dbGenAttr.ConstructorArguments.FirstOrDefault();
                // Both Jaunty and DataAnnotations use 1 for Identity, 2 for Computed
                if (arg.Value is int val)
                {
                    if (val == 1) isIdentity = true;
                    else if (val == 2) isComputed = true;
                }
            }
            else if (isKey && (prop.Type.SpecialType == SpecialType.System_Int32 || prop.Type.SpecialType == SpecialType.System_Int64))
            {
                isIdentity = true;
            }

            // FullyQualifiedFormat (global::-prefixed for non-special types) avoids a subtle
            // ambiguity: a bare ToDisplayString() for a property type declared in a namespace
            // whose leading segment collides with a type name elsewhere in the compilation
            // (e.g. a "Jaunty.*" entity namespace colliding with the "Jaunty" extension-methods
            // class) resolves the leading segment as that type instead of the namespace,
            // producing a CS0426 in the generated code. Built-in/special types (int, string,
            // decimal?, etc.) are unaffected - FullyQualifiedFormat keeps their keyword form.
            properties.Add(new PropertyMetadata(prop.Name, columnName, isKey, isIdentity, isComputed, prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
        }

        (var tableName, var schemaName) = GetTableNameAndSchema(classSymbol);
        var primaryKeyColumnNames = properties.Where(p => p.IsPrimaryKey).Select(p => p.ColumnName).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Data.Common;");
        sb.AppendLine("using System.Runtime.CompilerServices;");
        sb.AppendLine("using Jaunty.Interfaces;");
        sb.AppendLine();
        if (!isGlobalNamespace)
        {
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
        }
        sb.AppendLine($"    public partial class {className} : IMapped<{className}>, IEntityMetadataSource");
        sb.AppendLine("    {");
        sb.AppendLine("        public readonly struct ColumnInfo");
        sb.AppendLine("        {");
        sb.AppendLine("            public ColumnInfo(string columnName, string propertyName, bool isPrimaryKey, bool isIdentity, System.Type propertyType, System.Func<object, object?> getter, System.Action<object, object?> setter)");
        sb.AppendLine("            {");
        sb.AppendLine("                ColumnName = columnName;");
        sb.AppendLine("                PropertyName = propertyName;");
        sb.AppendLine("                IsPrimaryKey = isPrimaryKey;");
        sb.AppendLine("                IsIdentity = isIdentity;");
        sb.AppendLine("                PropertyType = propertyType;");
        sb.AppendLine("                Getter = getter;");
        sb.AppendLine("                Setter = setter;");
        sb.AppendLine("            }");
        sb.AppendLine("            public string ColumnName { get; }");
        sb.AppendLine("            public string PropertyName { get; }");
        sb.AppendLine("            public bool IsPrimaryKey { get; }");
        sb.AppendLine("            public bool IsIdentity { get; }");
        sb.AppendLine("            public System.Type PropertyType { get; }");
        sb.AppendLine("            public System.Func<object, object?> Getter { get; }");
        sb.AppendLine("            public System.Action<object, object?> Setter { get; }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // 1. ReadEntity - dual path for DbDataReader (fast) vs IDataReader (fallback)
        // Cache the reader type once to avoid per-row type check overhead
        sb.AppendLine("        #if NET8_0_OR_GREATER");
        sb.AppendLine($"        public static {className} ReadEntity(IDataReader reader)");
        sb.AppendLine("        #else");
        sb.AppendLine($"        public {className} ReadEntity(IDataReader reader)");
        sb.AppendLine("        #endif");
        sb.AppendLine("        {");
        sb.AppendLine("            var ord = OrdinalMap.Resolve(reader);");
        sb.AppendLine($"            var entity = new {className}();");
        sb.AppendLine("            bool isDbReader = reader is DbDataReader;");
        sb.AppendLine("            if (isDbReader)");
        sb.AppendLine("            {");
        sb.AppendLine("                var dbReader = (DbDataReader)reader;");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            ReaderTypeInfo typeInfo = GetReaderTypeInfo(p.TypeName);
            var typeForGetFieldValue = typeInfo.TypeForGetFieldValue;
            var needsNullCheck = typeInfo.NeedsNullCheck;

            // DbDataReader path: direct typed getters (no generic dispatch);
            // GetFieldValue<T> only for GetValue-fallback types (TimeSpan/DateTimeOffset/unknown).
            string dbAccess = typeInfo.Getter == "reader.GetValue"
                ? $"dbReader.GetFieldValue<{typeForGetFieldValue}>"
                : "dbReader." + typeInfo.Getter.Substring("reader.".Length);
            if (needsNullCheck)
            {
                // default must be typed to the property, not the getter: an untyped default in
                // the ternary binds to the getter type, so DBNull would map to 0/false for
                // nullable value types instead of null.
                sb.AppendLine($"                entity.{p.PropertyName} = dbReader.IsDBNull(ord[{i}]) ? default({p.TypeName})! : {dbAccess}(ord[{i}]);");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {dbAccess}(ord[{i}]);");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            ReaderTypeInfo typeInfo = GetReaderTypeInfo(p.TypeName);
            // IDataReader has no generic GetFieldValue<T>; GetValue returns object, so an
            // explicit cast to the property type is required or the generated code doesn't
            // compile (CS0266) for TimeSpan/DateTimeOffset/enum/other GetValue-fallback types.
            var getter = typeInfo.Getter == "reader.GetValue" ? $"({p.TypeName})reader.GetValue" : typeInfo.Getter;
            var needsNullCheck = typeInfo.NeedsNullCheck;

            // Fallback IDataReader path
            if (needsNullCheck)
            {
                sb.AppendLine($"                if (!reader.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                    entity.{p.PropertyName} = {getter}(ord[{i}]);");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {getter}(ord[{i}]);");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            return entity;");
        sb.AppendLine("        }");

        // 1b. CreateRowMapper - per-result-set factory: validates shape ONCE via
        // OrdinalMap.Resolve, then returns a closure that maps rows with zero
        // per-row validation. A FieldCount guard falls back to the fully
        // re-validating ReadEntity if the reader shape changes underneath a
        // stale delegate (PRD-001 safety preserved; resolution happens per
        // result set in DrDispatcher callers).
        sb.AppendLine($"        public static Func<IDataReader, {className}> CreateRowMapper(IDataReader reader)");
        sb.AppendLine("        {");
        sb.AppendLine("            var ord = OrdinalMap.Resolve(reader);");
        sb.AppendLine("            int fieldCount = reader.FieldCount;");
        sb.AppendLine("            if (reader is DbDataReader)");
        sb.AppendLine("            {");
        sb.AppendLine("                return r =>");
        sb.AppendLine("                {");
        sb.AppendLine("                    var rr = (DbDataReader)r;");
        sb.AppendLine("                    if (rr.FieldCount != fieldCount)");
        sb.AppendLine("        #if NET8_0_OR_GREATER");
        sb.AppendLine("                        return ReadEntity(r);");
        sb.AppendLine("        #else");
        sb.AppendLine($"                        return new {className}().ReadEntity(r);");
        sb.AppendLine("        #endif");
        sb.AppendLine($"                    var entity = new {className}();");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            ReaderTypeInfo typeInfo = GetReaderTypeInfo(p.TypeName);
            var typeForGetFieldValue = typeInfo.TypeForGetFieldValue;
            string dbAccess = typeInfo.Getter == "reader.GetValue"
                ? $"rr.GetFieldValue<{typeForGetFieldValue}>"
                : "rr." + typeInfo.Getter.Substring("reader.".Length);
            if (typeInfo.NeedsNullCheck)
            {
                sb.AppendLine($"                    entity.{p.PropertyName} = rr.IsDBNull(ord[{i}]) ? default({p.TypeName})! : {dbAccess}(ord[{i}]);");
            }
            else
            {
                sb.AppendLine($"                    entity.{p.PropertyName} = {dbAccess}(ord[{i}]);");
            }
        }
        sb.AppendLine("                    return entity;");
        sb.AppendLine("                };");
        sb.AppendLine("            }");
        sb.AppendLine("            return r =>");
        sb.AppendLine("            {");
        sb.AppendLine("                if (r.FieldCount != fieldCount)");
        sb.AppendLine("        #if NET8_0_OR_GREATER");
        sb.AppendLine("                    return ReadEntity(r);");
        sb.AppendLine("        #else");
        sb.AppendLine($"                    return new {className}().ReadEntity(r);");
        sb.AppendLine("        #endif");
        sb.AppendLine($"                var entity = new {className}();");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            ReaderTypeInfo typeInfo = GetReaderTypeInfo(p.TypeName);
            var getter = typeInfo.Getter == "reader.GetValue" ? $"({p.TypeName})r.GetValue" : "r." + typeInfo.Getter.Substring("reader.".Length);
            if (typeInfo.NeedsNullCheck)
            {
                sb.AppendLine($"                if (!r.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                    entity.{p.PropertyName} = {getter}(ord[{i}]);");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {getter}(ord[{i}]);");
            }
        }
        sb.AppendLine("                return entity;");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        // 2. BindInsert
        sb.AppendLine($"        public static void BindInsert(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => !x.IsIdentity && !x.IsComputed))
        {
            sb.AppendLine($"            AddParam(command, p, \"@{EscapeStringLiteral(p.ColumnName)}\", entity.{p.PropertyName});");
        }
        sb.AppendLine("        }");

        // 3. BindUpdate
        sb.AppendLine($"        public static void BindUpdate(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => !x.IsPrimaryKey && !x.IsIdentity && !x.IsComputed))
        {
            sb.AppendLine($"            AddParam(command, p, \"@{EscapeStringLiteral(p.ColumnName)}\", entity.{p.PropertyName});");
        }
        foreach (PropertyMetadata p in properties.Where(x => x.IsPrimaryKey))
        {
            sb.AppendLine($"            AddParam(command, p, \"@{EscapeStringLiteral(p.ColumnName)}\", entity.{p.PropertyName});");
        }
        sb.AppendLine("        }");

        // 4. BindDelete
        sb.AppendLine($"        public static void BindDelete(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => x.IsPrimaryKey))
        {
            sb.AppendLine($"            AddParam(command, p, \"@{EscapeStringLiteral(p.ColumnName)}\", entity.{p.PropertyName});");
        }
        sb.AppendLine("        }");

        sb.AppendLine("        private static void AddParam(IDbCommand cmd, IDataParameterCollection pc, string name, object? value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = cmd.CreateParameter();");
        sb.AppendLine("            p.ParameterName = name;");
        sb.AppendLine("            p.Value = value ?? DBNull.Value;");
        sb.AppendLine("            pc.Add(p);");
        sb.AppendLine("        }");

        sb.AppendLine("        private static class OrdinalMap");
        sb.AppendLine("        {");
        sb.AppendLine("            private static readonly ConditionalWeakTable<IDataReader, CacheEntry> _cache = new();");
        sb.AppendLine("            private static CacheEntry? _last;");
        sb.AppendLine();
        sb.AppendLine($"            public static int[] Resolve(IDataReader reader)");
        sb.AppendLine("            {");
        sb.AppendLine("                var last = _last;");
        sb.AppendLine("                if (last is not null && last.Matches(reader))");
        sb.AppendLine("                    return last.Ordinals;");
        sb.AppendLine();
        sb.AppendLine("                if (_cache.TryGetValue(reader, out var cached) && cached.Matches(reader))");
        sb.AppendLine("                {");
        sb.AppendLine("                    _last = cached;");
        sb.AppendLine("                    return cached.Ordinals;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                var ords = new int[{properties.Count}];");
        for (int i = 0; i < properties.Count; i++)
        {
            sb.AppendLine($"                ords[{i}] = reader.GetOrdinal(\"{EscapeStringLiteral(properties[i].ColumnName)}\");");
        }
        sb.AppendLine("                var entry = new CacheEntry(reader, ords);");
        sb.AppendLine("                _cache.Remove(reader);");
        sb.AppendLine("                _cache.Add(reader, entry);");
        sb.AppendLine("                _last = entry;");
        sb.AppendLine("                return ords;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            private sealed class CacheEntry");
        sb.AppendLine("            {");
        sb.AppendLine("                private readonly IDataReader _reader;");
        sb.AppendLine("                private readonly int _fieldCount;");
        sb.AppendLine();
        sb.AppendLine("                public CacheEntry(IDataReader reader, int[] ordinals)");
        sb.AppendLine("                {");
        sb.AppendLine("                    _reader = reader;");
        sb.AppendLine("                    _fieldCount = reader.FieldCount;");
        sb.AppendLine("                    Ordinals = ordinals;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                public int[] Ordinals { get; }");
        sb.AppendLine();
        sb.AppendLine("                public bool Matches(IDataReader reader)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!ReferenceEquals(reader, _reader))");
        sb.AppendLine("                        return false;");
        sb.AppendLine();
        sb.AppendLine("                    if (reader.FieldCount != _fieldCount)");
        sb.AppendLine("                        return false;");
        sb.AppendLine();
        for (int i = 0; i < properties.Count; i++)
        {
            sb.AppendLine($"                    var ord{i} = Ordinals[{i}];");
            sb.AppendLine($"                    if ((uint)ord{i} >= (uint)reader.FieldCount)");
            sb.AppendLine("                        return false;");
            sb.AppendLine($"                    if (!string.Equals(reader.GetName(ord{i}), \"{EscapeStringLiteral(properties[i].ColumnName)}\", StringComparison.OrdinalIgnoreCase))");
            sb.AppendLine("                        return false;");
            sb.AppendLine();
        }
        sb.AppendLine("                    return true;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        }");

        sb.AppendLine();
        sb.AppendLine($"        public static string TableName {{ get; }} = \"{EscapeStringLiteral(tableName)}\";");
        sb.AppendLine($"        public static string? SchemaName {{ get; }} = {(schemaName is null ? "null" : $"\"{EscapeStringLiteral(schemaName)}\"")};");
        sb.AppendLine("        public static System.Collections.Generic.IReadOnlyList<string> PrimaryKeyColumnNames { get; }");
        sb.AppendLine("            = new string[] {");
        foreach (var pkColumnName in primaryKeyColumnNames)
        {
            sb.AppendLine($"            \"{EscapeStringLiteral(pkColumnName)}\",");
        }
        sb.AppendLine("        };");

        var insertProps = properties.Where(x => !x.IsIdentity && !x.IsComputed).ToList();
        var updateProps = properties.Where(x => !x.IsPrimaryKey && !x.IsIdentity && !x.IsComputed).ToList();
        var deleteProps = properties.Where(x => x.IsPrimaryKey).ToList();

        string ColumnInfoCtor(PropertyMetadata p)
            => $"new ColumnInfo(\"{EscapeStringLiteral(p.ColumnName)}\", \"{p.PropertyName}\", {p.IsPrimaryKey.ToString().ToLower()}, {p.IsIdentity.ToString().ToLower()}, " +
               $"typeof({p.TypeName}), e => (object?)(({className})e).{p.PropertyName}, (e, v) => (({className})e).{p.PropertyName} = ({p.TypeName})v!)";

        sb.AppendLine();
        sb.AppendLine("        public static System.Collections.Generic.IReadOnlyList<ColumnInfo> InsertColumns { get; }");
        sb.AppendLine("            = new ColumnInfo[] {");
        for (int i = 0; i < insertProps.Count; i++)
        {
            sb.AppendLine($"            {ColumnInfoCtor(insertProps[i])},");
        }
        sb.AppendLine("        };");

        sb.AppendLine();
        sb.AppendLine("        public static System.Collections.Generic.IReadOnlyList<ColumnInfo> UpdateColumns { get; }");
        sb.AppendLine("            = new ColumnInfo[] {");
        for (int i = 0; i < updateProps.Count; i++)
        {
            sb.AppendLine($"            {ColumnInfoCtor(updateProps[i])},");
        }
        sb.AppendLine("        };");

        sb.AppendLine();
        sb.AppendLine("        public static System.Collections.Generic.IReadOnlyList<ColumnInfo> DeleteColumns { get; }");
        sb.AppendLine("            = new ColumnInfo[] {");
        for (int i = 0; i < deleteProps.Count; i++)
        {
            sb.AppendLine($"            {ColumnInfoCtor(deleteProps[i])},");
        }
        sb.AppendLine("        };");

        sb.AppendLine();
        sb.AppendLine("        public static System.Collections.Generic.Dictionary<string, ColumnInfo> ParameterMap { get; }");
        sb.AppendLine("            = new System.Collections.Generic.Dictionary<string, ColumnInfo>(System.StringComparer.OrdinalIgnoreCase)");
        sb.AppendLine("            {");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            sb.AppendLine($"            [\"{EscapeStringLiteral(p.ColumnName)}\"] = {ColumnInfoCtor(p)},");
        }
        sb.AppendLine("        };");

        // IEntityMetadataSource - public, reflection-free metadata surface for Jaunty core.
        // Explicit interface implementation for TableName/SchemaName since the public static
        // properties of the same name already exist above (a class cannot have both a static
        // and an instance member sharing one name).
        string EntityColumnInfoCtor(PropertyMetadata p)
            => $"new EntityColumnInfo(\"{EscapeStringLiteral(p.ColumnName)}\", \"{p.PropertyName}\", {p.IsPrimaryKey.ToString().ToLower()}, {p.IsIdentity.ToString().ToLower()}, {p.IsComputed.ToString().ToLower()}, " +
               $"typeof({p.TypeName}), e => (object?)(({className})e).{p.PropertyName}, (e, v) => (({className})e).{p.PropertyName} = ({p.TypeName})v!)";

        sb.AppendLine();
        sb.AppendLine("        public static System.Collections.Generic.IReadOnlyList<EntityColumnInfo> EntityColumns { get; }");
        sb.AppendLine("            = new EntityColumnInfo[] {");
        for (int i = 0; i < properties.Count; i++)
        {
            sb.AppendLine($"            {EntityColumnInfoCtor(properties[i])},");
        }
        sb.AppendLine("        };");
        sb.AppendLine();
        sb.AppendLine("        string IEntityMetadataSource.TableName => TableName;");
        sb.AppendLine("        string? IEntityMetadataSource.SchemaName => SchemaName;");
        sb.AppendLine("        System.Collections.Generic.IReadOnlyList<EntityColumnInfo> IEntityMetadataSource.Columns => EntityColumns;");

        sb.AppendLine("    }");
        if (!isGlobalNamespace)
            sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Returns <see langword="true"/> when the given symbol has an attribute whose class name
    /// matches <paramref name="attributeName"/> (simple name comparison, no namespace).
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="attributeName">The simple attribute class name (e.g. <c>"KeyAttribute"</c>).</param>
    private static bool HasAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().Any(a => a.AttributeClass?.Name == attributeName);

    /// <summary>
    /// Returns the first <see cref="AttributeData"/> on <paramref name="symbol"/> whose class name
    /// matches <paramref name="attributeName"/>, or <see langword="null"/> if none is found.
    /// </summary>
    /// <param name="symbol">The symbol to inspect.</param>
    /// <param name="attributeName">The simple attribute class name (e.g. <c>"ColumnAttribute"</c>).</param>
    private static AttributeData? GetAttribute(ISymbol symbol, string attributeName)
        => symbol.GetAttributes().FirstOrDefault(a => a.AttributeClass?.Name == attributeName);

    /// <summary>
    /// Escapes a value for safe interpolation inside a generated C# string literal, doubling
    /// backslashes and escaping embedded double quotes so a table/column name (sourced from a
    /// [Table]/[Column] attribute, not a compiler-validated identifier) cannot break out of the
    /// literal and produce invalid or semantically different generated code.
    /// </summary>
    /// <param name="value">The raw value to escape.</param>
    private static string EscapeStringLiteral(string value)
        => value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    /// <summary>
    /// Resolves the table name and optional schema name for an entity class from its
    /// <c>[Table]</c> attribute (Jaunty native or DataAnnotations, matched by simple class
    /// name so either is accepted). Falls back to the class name when no attribute is present.
    /// Schema is read from either a positional second constructor argument (Jaunty's
    /// <c>TableAttribute(name, schema)</c>) or a named <c>Schema</c> argument (DataAnnotations
    /// style, or Jaunty via named argument).
    /// </summary>
    /// <param name="classSymbol">The entity class symbol.</param>
    private static (string TableName, string? SchemaName) GetTableNameAndSchema(INamedTypeSymbol classSymbol)
    {
        var tableName = classSymbol.Name;
        string? schemaName = null;

        AttributeData? tableAttr = GetAttribute(classSymbol, "TableAttribute");
        if (tableAttr is null)
            return (tableName, schemaName);

        if (tableAttr.ConstructorArguments.Length > 0 && tableAttr.ConstructorArguments[0].Value is string name && !string.IsNullOrEmpty(name))
            tableName = name;

        if (tableAttr.ConstructorArguments.Length > 1 && tableAttr.ConstructorArguments[1].Value is string ctorSchema && !string.IsNullOrEmpty(ctorSchema))
            schemaName = ctorSchema;

        foreach (KeyValuePair<string, TypedConstant> namedArg in tableAttr.NamedArguments)
        {
            if (namedArg.Key == "Schema" && namedArg.Value.Value is string namedSchema && !string.IsNullOrEmpty(namedSchema))
                schemaName = namedSchema;
        }

        return (tableName, schemaName);
    }

    /// <summary>
    /// Maps a C# type name to reader accessor details used when generating <c>ReadEntity</c> body.
    /// Returns the appropriate <c>IDataReader</c> getter method name, the type argument for
    /// <c>GetFieldValue&lt;T&gt;</c>, and whether a DBNull check is required.
    /// </summary>
    /// <param name="typeName">The fully-qualified or short C# type name (e.g. <c>"int?"</c>, <c>"string"</c>).</param>
    /// <returns>A <see cref="ReaderTypeInfo"/> describing the reader access pattern for the type.</returns>
    private static ReaderTypeInfo GetReaderTypeInfo(string typeName)
    {
        return typeName switch
        {
            // Non-nullable value types - no null check needed
            "int" or "Int32" or "System.Int32" => new("reader.GetInt32", "int", false),
            "long" or "Int64" or "System.Int64" => new("reader.GetInt64", "long", false),
            "bool" or "Boolean" or "System.Boolean" => new("reader.GetBoolean", "bool", false),
            "decimal" or "Decimal" or "System.Decimal" => new("reader.GetDecimal", "decimal", false),
            "double" or "Double" or "System.Double" => new("reader.GetDouble", "double", false),
            "float" or "Single" or "System.Single" => new("reader.GetFloat", "float", false),
            "short" or "Int16" or "System.Int16" => new("reader.GetInt16", "short", false),
            "byte" or "Byte" or "System.Byte" => new("reader.GetByte", "byte", false),
            "Guid" or "System.Guid" or "global::System.Guid" => new("reader.GetGuid", "Guid", false),
            "DateTime" or "System.DateTime" or "global::System.DateTime" => new("reader.GetDateTime", "DateTime", false),
            "TimeSpan" or "System.TimeSpan" or "global::System.TimeSpan" => new("reader.GetValue", "TimeSpan", false),
            "DateTimeOffset" or "System.DateTimeOffset" or "global::System.DateTimeOffset" => new("reader.GetValue", "DateTimeOffset", false),

            // Nullable value types - needs null check
            "int?" or "Int32?" => new("reader.GetInt32", "int", true),
            "long?" or "Int64?" => new("reader.GetInt64", "long", true),
            "bool?" or "Boolean?" => new("reader.GetBoolean", "bool", true),
            "decimal?" or "Decimal?" => new("reader.GetDecimal", "decimal", true),
            "double?" or "Double?" => new("reader.GetDouble", "double", true),
            "float?" or "Single?" => new("reader.GetFloat", "float", true),
            "short?" or "Int16?" => new("reader.GetInt16", "short", true),
            "byte?" or "Byte?" => new("reader.GetByte", "byte", true),
            "Guid?" or "System.Guid?" or "global::System.Guid?" => new("reader.GetGuid", "Guid", true),
            "DateTime?" or "System.DateTime?" or "global::System.DateTime?" => new("reader.GetDateTime", "DateTime", true),
            "TimeSpan?" or "System.TimeSpan?" or "global::System.TimeSpan?" => new("reader.GetValue", "TimeSpan", true),
            "DateTimeOffset?" or "System.DateTimeOffset?" or "global::System.DateTimeOffset?" => new("reader.GetValue", "DateTimeOffset", true),

            // Reference types - needs null check
            "string" or "String" or "string?" or "String?" or "System.String" => new("reader.GetString", "string", true),

            // Unknown types (enums, byte[], etc.) - needs null check, fall back to GetValue.
            // TypeForGetFieldValue must be the actual (non-nullable-suffixed) type so the
            // DbDataReader fast path's GetFieldValue<T> call is directly assignable to the
            // property without an invalid cast from object (CS0266).
            _ => new("reader.GetValue", typeName.TrimEnd('?'), true)
        };
    }

    /// <summary>
    /// Captures the reader accessor method, the type argument for <c>GetFieldValue&lt;T&gt;</c>,
    /// and whether a DBNull guard is required for a given C# property type.
    /// </summary>
    private readonly struct ReaderTypeInfo(string getter, string typeForGetFieldValue, bool needsNullCheck)
    {
        public string Getter => getter;
        public string TypeForGetFieldValue => typeForGetFieldValue;
        public bool NeedsNullCheck => needsNullCheck;
    }

    /// <summary>
    /// Holds the resolved mapping metadata for a single property of an entity class.
    /// </summary>
    private struct PropertyMetadata(string propertyName, string columnName, bool isPrimaryKey, bool isIdentity, bool isComputed, string typeName)
    {
        public string PropertyName = propertyName;
        public string ColumnName = columnName;
        public bool IsPrimaryKey = isPrimaryKey;
        public bool IsIdentity = isIdentity;
        public bool IsComputed = isComputed;
        public string TypeName = typeName;
    }
}