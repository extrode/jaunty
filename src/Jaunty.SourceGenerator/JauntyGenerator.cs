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
public partial class JauntyGenerator : IIncrementalGenerator
{
    /// <summary>
    /// Reported when two mapped properties resolve to the same effective column name (a literal
    /// duplicate or a case-variant, since <c>ParameterMap</c>'s dictionary comparer is
    /// <see cref="StringComparer.OrdinalIgnoreCase"/>). Left unreported, the generated
    /// <c>ParameterMap</c> collection initializer would contain a duplicate key and throw
    /// <see cref="ArgumentException"/> at the entity's static-constructor time instead of failing
    /// visibly at generate time.
    /// </summary>
    private static readonly DiagnosticDescriptor DuplicateColumnNameDescriptor = new(
        id: "JAUNTYGEN001",
        title: "Duplicate mapped column name",
        messageFormat: "Entity '{0}' has more than one property mapped to column '{1}'; only the first ('{2}') will be used in ParameterMap",
        category: "JauntySourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Reported when a type implements <c>IMapped&lt;T&gt;</c> by hand instead of being
    /// source-generated. Spec 009.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A source-generated entity implements <c>IGeneratedAccessors&lt;T&gt;</c>, so Jaunty takes its
    /// mapper as a delegate and the generated <c>ReadEntity</c> is statically referenced - the trimmer
    /// keeps it. A hand-written <c>IMapped&lt;T&gt;</c> has no such reference, so
    /// <c>MappedCache&lt;T&gt;</c> falls back to <c>typeof(T).GetMethod("ReadEntity")</c>, and on a
    /// trimmed or NativeAOT publish that member can be removed. The failure then surfaces at runtime
    /// as <c>No mapper found for type 'X'</c>, with nothing connecting it to trimming - which is
    /// exactly the defect spec 009 exists to remove. Reporting it here converts a runtime surprise
    /// into a build-time warning that names the type.
    /// </para>
    /// <para>
    /// Warning rather than error on purpose: a hand-written <c>IMapped&lt;T&gt;</c> is legitimate and
    /// works fine on the JIT, so this must not break those builds. It does fire for JIT-only consumers
    /// who will never trim; if that proves noisy, the fix is to gate it on the consumer's
    /// <c>PublishTrimmed</c>/<c>IsTrimmable</c> via a <c>CompilerVisibleProperty</c> shipped in the
    /// package's .props, not to lower the severity - the condition it reports is real.
    /// </para>
    /// </remarks>
    private static readonly DiagnosticDescriptor HandWrittenMapperNotTrimSafeDescriptor = new(
        id: "JAUNTYGEN002",
        title: "Hand-written IMapped<T> will not survive trimming",
        messageFormat: "Type '{0}' implements IMapped<{0}> by hand, so Jaunty locates its ReadEntity by reflection and a trimmed or NativeAOT publish can remove it, failing at runtime with \"No mapper found for type '{0}'\". Add [Table] so Jaunty generates the mapper, implement IGeneratedAccessors<{0}>, or root the member with [DynamicDependency].",
        category: "JauntySourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Reported when a <c>[Table]</c> entity is nested inside a type the generated partial cannot be
    /// re-declared in, so no mapper is emitted for it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R33-006. A nested entity is discovered like any other, but the emitted file used to
    /// reconstruct only <c>namespace + class</c>, putting the partial at namespace scope - where it
    /// is a <i>different type</i> from the entity. Every member it emitted then named something that
    /// type does not declare (CS1061), and a nested <c>private</c>/<c>protected</c> entity emitted an
    /// accessibility illegal at namespace scope (CS1527), all inside a <c>.g.cs</c> the consumer
    /// cannot edit and with nothing saying why. Nesting is now reconstructed properly; this
    /// diagnostic covers what remains impossible - an enclosing type that is not <c>partial</c>
    /// (CS0260 if we emitted anyway) or is generic (its type parameters would have to be threaded
    /// through the mapper's signatures).
    /// </para>
    /// <para>
    /// Warning rather than error, and generation is skipped: without a generated mapper
    /// <c>MappedCache&lt;T&gt;</c> falls back to reflection, which handles nested entities fine. The
    /// entity keeps working, it just loses the AOT-safe path - which is the same trade-off
    /// JAUNTYGEN002 reports, so it gets the same severity.
    /// </para>
    /// </remarks>
    private static readonly DiagnosticDescriptor UnsupportedNestingDescriptor = new(
        id: "JAUNTYGEN004",
        title: "Entity is nested in a type the mapper cannot be generated into",
        messageFormat: "No mapper was generated for entity '{0}' because {1}. Jaunty will map it by reflection instead, which a trimmed or NativeAOT publish can break.",
        category: "JauntySourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Reported when the generated mapper drops a property the reflection mapper maps, so referencing
    /// the generator package silently changes which columns an entity has.
    /// </summary>
    /// <remarks>
    /// <para>
    /// AUD-R34-030, from the round-33 carry-forward. Two skips in <c>BuildEntityModel</c> are
    /// invisible divergences rather than agreed exclusions. An <c>init</c>-only setter cannot be
    /// assigned by the post-construction <c>entity.Prop = value</c> this generator emits (CS8852),
    /// and a setter that exists but is not accessible from the entity class gives CS0272
    /// (AUD-R33-007) - so both have to be skipped here. Neither is a problem for the reflection
    /// twin: <c>MetadataBuilder</c> tests <c>property.CanWrite</c>, which is true for both, and
    /// <c>SetValue</c> reaches an init-only or non-public setter happily. The column is therefore
    /// read, written and mapped under reflection and silently absent once the generator package is
    /// referenced - the same silent-column-loss shape as the inherited-property defect AUD-R26
    /// fixed, and not something a green test suite catches, because the entity still maps.
    /// </para>
    /// <para>
    /// Warning rather than a fix on either side: closing the gap properly means emitting an object
    /// initializer, which cannot express "leave the property alone when the column is NULL" (the
    /// behaviour AUD-R33-009 settled on and the reflection twin shares), and dropping the property
    /// from the reflection path instead would remove working behaviour from entities that never see
    /// the generator. So the divergence stands and is made loud: <c>[Ignore]</c>/<c>[NotMapped]</c>
    /// silences it and records that the exclusion is intended.
    /// </para>
    /// </remarks>
    private static readonly DiagnosticDescriptor PropertyDroppedByGeneratorDescriptor = new(
        id: "JAUNTYGEN005",
        title: "Property is mapped by reflection but dropped by the generated mapper",
        messageFormat: "Property '{0}.{1}' is not mapped by the generated mapper because {2}, but Jaunty's reflection mapper does map it - so referencing the generator package silently drops this column. Give it a plain accessible setter, or mark it [Ignore] to record that the exclusion is intended.",
        category: "JauntySourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>The two <c>[Table]</c> attributes the generator recognizes, by metadata name.</summary>
    private const string JauntyTableAttribute = "Jaunty.Attributes.TableAttribute";
    private const string DataAnnotationsTableAttribute = "System.ComponentModel.DataAnnotations.Schema.TableAttribute";

    /// <summary>The interface a hand-written mapper implements, by metadata name.</summary>
    private const string MappedInterfaceMetadataName = "IMapped`1";

    /// <summary>The namespace <see cref="MappedInterfaceMetadataName"/> must live in to count.</summary>
    private const string JauntyInterfacesNamespace = "Jaunty.Interfaces";

    /// <summary>
    /// Initializes the source generator by registering syntax providers and source output callbacks.
    /// </summary>
    /// <remarks>
    /// AUD-R25 (B8-7): the pipeline used to be a <c>CreateSyntaxProvider</c> yielding
    /// <c>ClassDeclarationSyntax</c>, collected and <c>Combine</c>d with
    /// <see cref="IncrementalGeneratorInitializationContext.CompilationProvider"/>, feeding a single
    /// source output that emitted every entity in the project. Both halves of that input defeat the
    /// driver's caching: syntax nodes have reference equality, and the compilation is a new object
    /// after every keystroke <i>anywhere</i>. So a character typed in a file containing no entity at
    /// all still re-ran semantic analysis for, and re-emitted, every <c>[Table]</c> class in the
    /// project - the generator's whole cost, on every keystroke, in the IDE's typing loop.
    ///
    /// <para>
    /// Three changes fix it. <c>ForAttributeWithMetadataName</c> replaces the hand-written
    /// predicate/transform pair - the compiler indexes attribute usages, so candidates are found
    /// without running a semantic check over every attributed class. The transform yields
    /// <see cref="EntityModel"/>, a value-equatable snapshot holding no symbols, so an edit that does
    /// not change an entity's mapping produces an equal model and everything downstream is skipped.
    /// And the source output is registered per entity rather than over the collected array, so
    /// editing one entity does not re-emit its neighbours.
    /// </para>
    ///
    /// <para>
    /// One deliberate narrowing comes with it. Recognition was previously by simple name, so <i>any</i>
    /// attribute called <c>TableAttribute</c> - including one a consumer declared themselves - marked
    /// a class as an entity. <c>ForAttributeWithMetadataName</c> matches fully-qualified names, so
    /// only the two documented attributes are recognized now. That is what the XML docs and the
    /// attribute reference always claimed; a third same-named attribute being silently honoured was
    /// undocumented behaviour, not a feature. Aliased usage is unaffected - the match is on the
    /// resolved symbol, not on the spelling at the use site.
    /// </para>
    /// </remarks>
    /// <param name="context">The initialization context for configuring the generator.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<EntityModel> jauntyTables = context.SyntaxProvider.ForAttributeWithMetadataName(
            JauntyTableAttribute,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => BuildEntityModel(ctx));

        IncrementalValuesProvider<EntityModel> annotatedTables = context.SyntaxProvider.ForAttributeWithMetadataName(
            DataAnnotationsTableAttribute,
            predicate: static (node, _) => node is ClassDeclarationSyntax,
            transform: static (ctx, _) => BuildEntityModel(ctx));

        IncrementalValuesProvider<EntityModel> entities = jauntyTables.Collect()
            .Combine(annotatedTables.Collect())
            .SelectMany(static (both, _) => Deduplicate(both.Left, both.Right));

        context.RegisterSourceOutput(entities, static (spc, entity) =>
        {
            // AUD-R33-006. Emitting a partial that cannot compile is strictly worse than emitting
            // nothing: the reflection fallback still maps the entity, whereas broken generated
            // source fails the consumer's build in a file they cannot open.
            if (entity.UnsupportedNestingReason is not null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    UnsupportedNestingDescriptor,
                    entity.DiagnosticLocation?.ToLocation(),
                    entity.ClassName,
                    entity.UnsupportedNestingReason));
                return;
            }

            // AUD-R34-030: only once a mapper is actually emitted. If generation was skipped above,
            // the reflection fallback maps the entity in full and there is no divergence to report.
            foreach (DroppedPropertyInfo dropped in entity.DroppedProperties)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    PropertyDroppedByGeneratorDescriptor,
                    dropped.Location?.ToLocation() ?? entity.DiagnosticLocation?.ToLocation(),
                    entity.ClassName,
                    dropped.PropertyName,
                    dropped.Reason));
            }

            var source = GenerateMapper(entity, spc.ReportDiagnostic);
            spc.AddSource($"{entity.HintName}.JauntyMapper.g.cs", SourceText.From(source, Encoding.UTF8));
        });

        // Spec 009 / JAUNTYGEN002. No attribute to key on here, so this is the one place the
        // generator has to look at plain class declarations. The predicate stays syntax-only and
        // cheap - a class with a base list - and the transform yields a small value-equatable
        // struct (or null), so an edit that does not change the answer is cached like everything
        // else in the pipeline.
        IncrementalValuesProvider<HandWrittenMapper?> handWrittenMappers = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
            transform: static (ctx, _) => FindHandWrittenMapper(ctx));

        context.RegisterSourceOutput(handWrittenMappers, static (spc, mapper) =>
        {
            if (mapper is { } m)
                spc.ReportDiagnostic(Diagnostic.Create(HandWrittenMapperNotTrimSafeDescriptor, m.Location, m.TypeName));
        });

        // Spec 011. Unlike everything above, this reads the consumer's *call sites* rather than their
        // type declarations: the parameter objects passed to Query/Execute are frequently anonymous
        // and are never declared as entities, so the call site is the only place their types are
        // knowable. See ParameterRooting.cs.
        RegisterParameterRooting(context);
    }

    /// <summary>
    /// A hand-written <c>IMapped&lt;T&gt;</c> implementation worth warning about. A value type holding
    /// no symbols, so the incremental driver can compare two results and skip downstream work.
    /// </summary>
    private readonly struct HandWrittenMapper : IEquatable<HandWrittenMapper>
    {
        public HandWrittenMapper(string typeName, Location location)
        {
            TypeName = typeName;
            Location = location;
        }

        public string TypeName { get; }
        public Location Location { get; }

        public bool Equals(HandWrittenMapper other)
            => TypeName == other.TypeName && Location.Equals(other.Location);

        public override bool Equals(object? obj) => obj is HandWrittenMapper other && Equals(other);

        public override int GetHashCode() => unchecked((TypeName?.GetHashCode() ?? 0) * 397) ^ Location.GetHashCode();
    }

    /// <summary>
    /// Returns the class under the cursor when it implements <c>Jaunty.Interfaces.IMapped&lt;T&gt;</c>
    /// by hand, or <see langword="null"/> otherwise.
    /// </summary>
    /// <remarks>
    /// <c>[Table]</c> entities are excluded: the generator emits <c>IMapped&lt;T&gt;</c> and
    /// <c>IGeneratedAccessors&lt;T&gt;</c> for them, so they are trim-safe and warning about them
    /// would be a false positive. They also would not be caught semantically here - a generator does
    /// not see its own output - but the attribute check makes that independent of that detail rather
    /// than reliant on it.
    /// </remarks>
    private static HandWrittenMapper? FindHandWrittenMapper(GeneratorSyntaxContext ctx)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol symbol)
            return null;

        // AUD-R34-031: by fully-qualified name, matching discovery. A consumer's own unrelated
        // TableAttribute does not make this type generated, so it must not suppress the warning that
        // says it is not.
        if (GetRecognizedTableAttribute(symbol) is not null)
            return null;

        // Already trim-safe by declaring the accessors itself.
        if (symbol.AllInterfaces.Any(static i => i.MetadataName == "IGeneratedAccessors`1"
                && i.ContainingNamespace?.ToDisplayString() == JauntyInterfacesNamespace))
            return null;

        if (!symbol.AllInterfaces.Any(static i => i.MetadataName == MappedInterfaceMetadataName
                && i.ContainingNamespace?.ToDisplayString() == JauntyInterfacesNamespace))
            return null;

        return new HandWrittenMapper(symbol.Name, ctx.Node.GetLocation());
    }

    /// <summary>
    /// Collects the entity's public, settable-shaped instance properties, including those it
    /// inherits.
    /// </summary>
    /// <remarks>
    /// AUD-R26: this used to be <c>classSymbol.GetMembers()</c>, which returns only the members
    /// <b>declared on the type itself</b>. The reflection path enumerates
    /// <c>type.GetProperties(BindingFlags.Instance | BindingFlags.Public)</c>
    /// (<c>MetadataBuilder.cs:67</c>), which <b>does</b> include inherited properties. So a base
    /// class holding shared columns - the ordinary audit-columns pattern,
    /// <c>class Order : AuditableEntity</c> - produced two different mappings for one entity
    /// depending on whether the generator was referenced: reflection mapped <c>CreatedAt</c> and
    /// <c>CreatedBy</c>, the generated mapper silently did not. Measured, and pinned from both
    /// sides, in <c>InheritedPropertyTests</c>.
    ///
    /// <para>
    /// Silently is the operative word. The generated mapper simply omitted those columns - no
    /// diagnostic, no compile error - so an entity that round-tripped correctly under reflection
    /// would quietly stop persisting half its columns once the generator package was added.
    /// </para>
    ///
    /// <para>
    /// The hierarchy is walked derived-first and stops at <see cref="object"/>, whose members are
    /// never columns. The first declaration of a name wins, so an <c>override</c> or a
    /// <c>new</c> shadow is taken from the most derived type that declares it - the same
    /// most-derived-wins rule used by <c>MappedPropertyFilter</c> in Jaunty.FlatFiles.DuckDB.
    /// </para>
    /// </remarks>
    private static List<IPropertySymbol> GetMappableProperties(INamedTypeSymbol classSymbol)
    {
        var result = new List<IPropertySymbol>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (INamedTypeSymbol? type = classSymbol;
             type is not null && type.SpecialType != SpecialType.System_Object;
             type = type.BaseType)
        {
            foreach (IPropertySymbol property in type.GetMembers().OfType<IPropertySymbol>())
            {
                // AUD-R25: !IsIndexer is the third guard against the same hazard. An indexer
                // (`public object this[int i] { get; set; }`) surfaces as a public instance property
                // named "Item" with index parameters, and C# forbids naming an indexer through
                // member access - so it would be emitted as `entity.Item = ...` in
                // ReadEntity/CreateRowMapper and `((Order)e).Item` in the ColumnInfo/EntityColumnInfo
                // lambdas, none of which compile. The reflection path degrades gracefully here (the
                // indexer is simply not a column); the generated path would break the build inside a
                // .g.cs the user cannot edit. Both MetadataBuilder and ParameterCache already skip
                // these.
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public)
                    continue;

                // Walking derived-first means a name already recorded came from a more derived
                // type, which shadows or overrides this declaration.
                if (!seen.Add(property.Name))
                    continue;

                result.Add(property);
            }
        }

        return result;
    }

    /// <summary>
    /// Merges the two recognized-attribute streams into one entity per generated file, keyed on the
    /// hint name.
    /// </summary>
    /// <remarks>
    /// AUD-R25: two distinct routes reach the same hint name. A class carrying both a Jaunty and a
    /// DataAnnotations <c>[Table]</c> appears in both streams; a <c>partial</c> class whose parts each
    /// carry a <c>[Table]</c> appears twice in one of them. Calling <c>AddSource</c> twice with one
    /// hint name fails the build, so both are collapsed here - keeping the first occurrence, which
    /// makes the emitted table/schema agree with <c>GetTableNameAndSchema</c>'s own first-match rule.
    ///
    /// <para>
    /// This replaces a dedupe keyed on <see cref="INamedTypeSymbol"/>. Hint name is the right key now
    /// because it is what actually has to be unique, and because a symbol cannot be held in a cached
    /// model - see <see cref="EntityModel"/>. It is derived from the fully-qualified type name, so
    /// two entities collapse here only if they would have collided on output anyway.
    /// </para>
    /// </remarks>
    private static ImmutableArray<EntityModel> Deduplicate(
        ImmutableArray<EntityModel> jauntyTables, ImmutableArray<EntityModel> annotatedTables)
    {
        if (jauntyTables.IsDefaultOrEmpty && annotatedTables.IsDefaultOrEmpty)
            return ImmutableArray<EntityModel>.Empty;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        ImmutableArray<EntityModel>.Builder merged
            = ImmutableArray.CreateBuilder<EntityModel>(jauntyTables.Length + annotatedTables.Length);

        foreach (EntityModel entity in jauntyTables)
        {
            if (seen.Add(entity.HintName))
                merged.Add(entity);
        }

        foreach (EntityModel entity in annotatedTables)
        {
            if (seen.Add(entity.HintName))
                merged.Add(entity);
        }

        return merged.ToImmutable();
    }

    /// <summary>
    /// Extracts an entity's complete mapping metadata from the compilation into a value-equatable
    /// <see cref="EntityModel"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R25 (B8-7): this is the boundary between the half of the generator that touches Roslyn
    /// symbols and the half that only formats strings. Everything below this method must work from
    /// the model alone - see <see cref="EntityModel"/> for why.
    /// </remarks>
    /// <param name="context">The attribute syntax context for a matched <c>[Table]</c> class.</param>
    private static EntityModel BuildEntityModel(GeneratorAttributeSyntaxContext context)
    {
        var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var className = classSymbol.Name;

        // AUD-R25: !IsIndexer is the third guard against the same hazard. An indexer
        // (`public object this[int i] { get; set; }`) surfaces as a public instance property named
        // "Item" with index parameters, and C# forbids naming an indexer through member access - so
        // it would be emitted as `entity.Item = ...` in ReadEntity/CreateRowMapper and
        // `((Order)e).Item` in the ColumnInfo/EntityColumnInfo lambdas, none of which compile. The
        // reflection path degrades gracefully here (the indexer is simply not a column); the
        // generated path would break the build inside a .g.cs the user cannot edit. Both
        // MetadataBuilder and ParameterCache already skip these.
        List<IPropertySymbol> allProperties = GetMappableProperties(classSymbol);

        var properties = new List<PropertyMetadata>();
        var dropped = new List<DroppedPropertyInfo>();
        foreach (IPropertySymbol? prop in allProperties)
        {
            // Support [Ignore] and [NotMapped]
            if (HasAttribute(prop, "IgnoreAttribute") || HasAttribute(prop, "NotMappedAttribute")) continue;

            // A get-only property has no SetMethod at all; an init-only property has one, but it
            // can only be assigned inside an object initializer, not via the post-construction
            // `entity.Prop = value` assignments this generator emits in ReadEntity/CreateRowMapper/
            // the ColumnInfo and EntityColumnInfo setter lambdas. Either would emit an assignment
            // that fails to compile (CS0200/CS8852), so treat both as implicitly [Ignore]d.
            if (prop.SetMethod is null) continue;

            // AUD-R34-030: but init-only is a divergence, not an agreed exclusion - the reflection
            // twin maps it, so the column is silently lost here. Reported rather than fixed; see
            // PropertyDroppedByGeneratorDescriptor for why neither side can simply move.
            if (prop.SetMethod.IsInitOnly)
            {
                dropped.Add(new DroppedPropertyInfo(
                    prop.Name,
                    "its setter is 'init'-only and the generated mapper assigns properties after construction",
                    LocationInfo.From(prop.Locations.FirstOrDefault())));
                continue;
            }

            // AUD-R33-007: and a setter that exists but cannot be reached from the entity class is
            // the same defect one step along. Since GetMappableProperties walks base types (AUD-R26)
            // a base-class `public string CreatedBy { get; private set; }`, or an `internal set` on a
            // base type in another assembly, reaches this loop; the emitted `entity.CreatedBy = ...`
            // then fails with CS0272 inside a .g.cs. The accessibility question is not answerable
            // from DeclaredAccessibility alone - it depends on where the setter was declared and on
            // assembly boundaries and InternalsVisibleTo - so ask the compilation, which is what
            // decides it. The reflection twin has no equivalent problem: MetadataBuilder tests
            // property.CanWrite, which is true here, and SetValue does reach a non-public setter.
            if (!context.SemanticModel.Compilation.IsSymbolAccessibleWithin(prop.SetMethod, classSymbol))
            {
                // AUD-R34-030: same silent divergence as the init-only case above - CanWrite is true
                // and SetValue reaches a non-public setter, so reflection maps this column.
                dropped.Add(new DroppedPropertyInfo(
                    prop.Name,
                    "its setter is not accessible from the entity class, so the generated assignment would not compile",
                    LocationInfo.From(prop.Locations.FirstOrDefault())));
                continue;
            }

            // Support [Column] from both
            AttributeData? columnAttr = GetAttribute(prop, "ColumnAttribute");
            // AUD-R32-006: an empty [Column("")] falls back to the property name rather than
            // generating a mapping to "". The null-coalesce alone did not catch it, and the
            // reflection MetadataBuilder carries the matching guard so both modes agree.
            var columnAttrName = columnAttr?.ConstructorArguments.FirstOrDefault().Value?.ToString();
            var columnName = string.IsNullOrEmpty(columnAttrName) ? prop.Name : columnAttrName!;

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
            // AUD-R25: an int/long key with no explicit [DatabaseGenerated] is *inferred* to be an
            // identity column. Recorded as inferred rather than applied outright, because the
            // inference is only defensible for a single-key entity - see the post-pass below.
            var isIdentityInferred = false;
            if (dbGenAttr is null && isKey
                && (prop.Type.SpecialType == SpecialType.System_Int32 || prop.Type.SpecialType == SpecialType.System_Int64))
            {
                isIdentity = true;
                isIdentityInferred = true;
            }

            // FullyQualifiedFormat (global::-prefixed for non-special types) avoids a subtle
            // ambiguity: a bare ToDisplayString() for a property type declared in a namespace
            // whose leading segment collides with a type name elsewhere in the compilation
            // (e.g. a "Jaunty.*" entity namespace colliding with the "Jaunty" extension-methods
            // class) resolves the leading segment as that type instead of the namespace,
            // producing a CS0426 in the generated code. Built-in/special types (int, string,
            // decimal?, etc.) are unaffected - FullyQualifiedFormat keeps their keyword form.
            // AUD-R25: a nullable value type arrives as Nullable<T>, so unwrap before asking about
            // TypeKind - otherwise `Severity?` reads as a struct and misses the enum handling.
            ITypeSymbol underlyingType = prop.Type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType
                ? nullableType.TypeArguments[0]
                : prop.Type;

            // AUD-R30: [EnumStorage] participates in the write path (BindInsert/BindUpdate), so it
            // must be captured here like [Column]/[DatabaseGenerated]. Raw int rather than the enum
            // type keeps the model free of Jaunty-assembly references.
            int? enumStorageOverride = null;
            if (underlyingType.TypeKind == TypeKind.Enum
                && GetAttribute(prop, "EnumStorageAttribute")?.ConstructorArguments.FirstOrDefault().Value is int storageValue)
            {
                enumStorageOverride = storageValue;
            }

            properties.Add(new PropertyMetadata(
                prop.Name, columnName, isKey, isIdentity, isComputed,
                prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                underlyingType.TypeKind == TypeKind.Enum,
                isIdentityInferred,
                enumStorageOverride));
        }

        // AUD-R25: the implicit-identity inference applies only to a single-key entity.
        //
        // Applied per property, as it was, it marked *every* int/long key column identity - so a
        // composite-key entity like `[Key] int OrderId` + `[Key] int ProductId` had both key columns
        // dropped from InsertColumns and from BindInsert, and the generated INSERT wrote a row with
        // no key values at all. No database has two identity columns, so there is no schema for
        // which that emission is correct; it is a plain defect rather than a debatable convention.
        //
        // The convention itself is left alone for the single-key case, where it is defensible and
        // where the generator has always behaved this way. That case still diverges from the
        // reflection path, which infers nothing and leaves such a column in the INSERT - see the
        // matching note in MetadataBuilder.BuildMetadata. Converging the two is a product decision
        // (either direction silently changes the SQL of existing entities on one path), so it is
        // recorded and test-pinned rather than settled here.
        var keyCount = properties.Count(x => x.IsPrimaryKey);
        if (keyCount > 1)
        {
            for (int i = 0; i < properties.Count; i++)
            {
                if (properties[i].IsIdentityInferred)
                    properties[i] = properties[i] with { IsIdentity = false, IsIdentityInferred = false };
            }
        }

        (var tableName, var schemaName) = GetTableNameAndSchema(classSymbol);

        (EquatableArray<ContainingTypeInfo> containingTypes, var unsupportedNesting) = BuildContainingTypes(classSymbol);

        // AUD-R34-028: the entity's own declaration, which AUD-R33-006 checked only for enclosing
        // types. Same rule and same reporting: emitting a partial that cannot compile is strictly
        // worse than emitting nothing, because the reflection fallback still maps the entity.
        unsupportedNesting ??= UnsupportedDeclarationReason(classSymbol);

        return new EntityModel(
            Namespace: classSymbol.ContainingNamespace.IsGlobalNamespace
                ? null
                : classSymbol.ContainingNamespace.ToDisplayString(),
            ClassName: className,
            AccessibilityKeyword: AccessibilityKeyword(classSymbol.DeclaredAccessibility),
            HintName: GetHintName(classSymbol),
            TableName: tableName,
            SchemaName: schemaName,
            Properties: new EquatableArray<PropertyMetadata>(properties.ToImmutableArray()),
            DiagnosticLocation: LocationInfo.From(classSymbol.Locations.FirstOrDefault()),
            ContainingTypes: containingTypes,
            UnsupportedNestingReason: unsupportedNesting,
            DroppedProperties: new EquatableArray<DroppedPropertyInfo>(dropped.ToImmutableArray()));
    }

    /// <summary>
    /// A reason the entity's own declaration cannot carry the generated partial, or
    /// <see langword="null"/> when it can.
    /// </summary>
    /// <remarks>
    /// AUD-R34-028. <c>GenerateMapper</c> assumes a concrete, instantiable, non-generic,
    /// <c>partial</c>, non-file-local class, and every violation emitted a <c>.g.cs</c> that failed
    /// the consumer's build with nothing explaining it: <c>abstract</c> gives CS0144 on the emitted
    /// <c>new Order()</c> (the reflection twin throws a clear <c>InvalidOperationException</c>
    /// instead), a parameterized-only constructor gives CS1729, <c>static</c> cannot implement
    /// <c>IMapped&lt;T&gt;</c>, a generic <c>Repo&lt;T&gt;</c> generates an arity-0
    /// <c>partial class Repo</c> that is a different type so every member access is CS1061, a
    /// non-<c>partial</c> class gives CS0260, and a <c>file</c>-local one gets a distinct
    /// non-file-local partial in another file.
    /// </remarks>
    private static string? UnsupportedDeclarationReason(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.IsStatic)
            return "it is declared 'static' and a static class cannot implement the mapper interface";

        if (classSymbol.IsAbstract)
            return "it is abstract and the generated mapper has to construct it";

        if (classSymbol.IsGenericType)
            return "it is generic";

        if (classSymbol.IsFileLocal)
            return "it is declared 'file'-local, so the generated partial in another file would be a different type";

        if (!IsDeclaredPartial(classSymbol))
            return "it is not declared 'partial'";

        var hasParameterlessConstructor = false;

        foreach (IMethodSymbol constructor in classSymbol.InstanceConstructors)
        {
            if (constructor.Parameters.Length == 0
                && constructor.DeclaredAccessibility != Accessibility.Private
                && constructor.DeclaredAccessibility != Accessibility.Protected)
            {
                hasParameterlessConstructor = true;
                break;
            }
        }

        if (!hasParameterlessConstructor)
            return "it has no accessible parameterless constructor and the generated mapper has to construct it";

        return null;
    }

    /// <summary>
    /// Walks the chain of types enclosing <paramref name="classSymbol"/> and returns it outermost
    /// first, or a reason why the mapper cannot be generated into it.
    /// </summary>
    /// <remarks>
    /// AUD-R33-006. An enclosing type must be <c>partial</c> for a second declaration of it to be
    /// legal at all, and must be non-generic because the generated members would otherwise have to
    /// close over its type parameters. Both are checked here, at model-build time, so the decision
    /// travels in the value-equatable model rather than being re-derived from symbols at emit time.
    /// </remarks>
    private static (EquatableArray<ContainingTypeInfo> ContainingTypes, string? UnsupportedReason) BuildContainingTypes(INamedTypeSymbol classSymbol)
    {
        if (classSymbol.ContainingType is null)
            return (new EquatableArray<ContainingTypeInfo>(ImmutableArray<ContainingTypeInfo>.Empty), null);

        var chain = new List<ContainingTypeInfo>();

        for (INamedTypeSymbol? containing = classSymbol.ContainingType; containing is not null; containing = containing.ContainingType)
        {
            if (containing.IsGenericType)
                return (default, $"its enclosing type '{containing.Name}' is generic");

            if (!IsDeclaredPartial(containing))
                return (default, $"its enclosing type '{containing.Name}' is not declared 'partial'");

            string? keyword = TypeKeyword(containing);

            if (keyword is null)
                return (default, $"its enclosing type '{containing.Name}' is not a class, struct, record or interface");

            chain.Add(new ContainingTypeInfo(
                AccessibilityKeyword(containing.DeclaredAccessibility),
                keyword,
                containing.Name));
        }

        chain.Reverse();
        return (new EquatableArray<ContainingTypeInfo>(chain.ToImmutableArray()), null);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every source declaration of <paramref name="symbol"/>
    /// carries the <c>partial</c> modifier.
    /// </summary>
    private static bool IsDeclaredPartial(INamedTypeSymbol symbol)
    {
        foreach (SyntaxReference reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is not TypeDeclarationSyntax declaration)
                return false;

            var isPartial = false;

            foreach (SyntaxToken modifier in declaration.Modifiers)
            {
                if (modifier.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword))
                {
                    isPartial = true;
                    break;
                }
            }

            if (!isPartial)
                return false;
        }

        return symbol.DeclaringSyntaxReferences.Length > 0;
    }

    /// <summary>
    /// The C# keyword a type must be re-declared with, or <see langword="null"/> for a type kind
    /// that cannot enclose a partial declaration (an enum or delegate, which cannot nest types).
    /// </summary>
    private static string? TypeKeyword(INamedTypeSymbol symbol) => symbol.TypeKind switch
    {
        TypeKind.Class => symbol.IsRecord ? "record" : "class",
        TypeKind.Struct => symbol.IsRecord ? "record struct" : "struct",
        TypeKind.Interface => "interface",
        _ => null
    };

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

        // R16: escape original underscores as "__" so a literal '_' in a namespace/class name
        // can never be confused with the single '_' used below as the delimiter for every other
        // non-alphanumeric character (dots, generic brackets, etc.) - otherwise e.g. namespace
        // "MyApp.Foo" class "Bar_Baz" and namespace "MyApp.Foo.Bar" class "Baz" both flattened to
        // "MyApp_Foo_Bar_Baz", producing a duplicate hint name that fails the build.
        var sb = new StringBuilder(fullName.Length);
        foreach (char c in fullName)
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == '_')
                sb.Append("__");
            else
                sb.Append('_');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Renders an entity's declared accessibility as the C# keyword to emit on the generated partial
    /// declaration.
    /// </summary>
    /// <remarks>
    /// AUD-R25: this used to be a literal <c>public</c>. C# requires every partial declaration of a
    /// type to agree on accessibility, so <c>[Table("orders")] internal partial class Order</c>
    /// failed to compile with CS0262 - and nothing in the attribute docs, the generator or the
    /// scaffolder said entities had to be public. The reflection path imposes no such restriction:
    /// <c>MetadataBuilder.BuildMetadata</c> works on any <see cref="System.Type"/>.
    /// </remarks>
    private static string AccessibilityKeyword(Accessibility accessibility) => accessibility switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        Accessibility.Private => "private",
        // NotApplicable shouldn't reach here for a named type; internal is the C# default for a
        // type declaration with no modifier, so it is the safest thing to mirror.
        _ => "internal"
    };

    /// <summary>
    /// Generates the complete source text for a Jaunty entity mapper partial class,
    /// including <c>ReadEntity</c>, <c>BindInsert</c>, <c>BindUpdate</c>, <c>BindDelete</c>,
    /// an ordinal-caching <c>OrdinalMap</c>, and column-info static properties.
    /// </summary>
    /// <param name="entity">The extracted mapping metadata for the entity class.</param>
    /// <param name="reportDiagnostic">Callback used to surface generator diagnostics, e.g. a duplicate mapped column name.</param>
    /// <returns>The generated C# source code as a string.</returns>
    static string GenerateMapper(EntityModel entity, Action<Diagnostic> reportDiagnostic)
    {
        var namespaceName = entity.Namespace;
        var isGlobalNamespace = namespaceName is null;
        var className = entity.ClassName;
        var tableName = entity.TableName;
        var schemaName = entity.SchemaName;
        EquatableArray<PropertyMetadata> properties = entity.Properties;

        var primaryKeyColumnNames = properties.Where(p => p.IsPrimaryKey).Select(p => p.ColumnName).ToList();

        // ParameterMap is emitted as a Dictionary<string, ColumnInfo> collection initializer keyed
        // by column name (OrdinalIgnoreCase). A duplicate key there compiles but throws
        // ArgumentException at the entity's static-constructor time, so duplicates (including
        // case-variants) are reported as a diagnostic and only the first occurrence is kept for
        // that emission - the other emissions below (ReadEntity, Bind*, InsertColumns/UpdateColumns/
        // DeleteColumns/EntityColumns) are array/list-based and unaffected by the collision.
        var parameterMapProperties = new List<PropertyMetadata>();
        var seenColumnNames = new Dictionary<string, PropertyMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (PropertyMetadata p in properties)
        {
            if (seenColumnNames.TryGetValue(p.ColumnName, out PropertyMetadata first))
            {
                reportDiagnostic(Diagnostic.Create(
                    DuplicateColumnNameDescriptor,
                    entity.DiagnosticLocation?.ToLocation() ?? Location.None,
                    className, p.ColumnName, first.PropertyName));
                continue;
            }

            seenColumnNames.Add(p.ColumnName, p);
            parameterMapProperties.Add(p);
        }

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
        // AUD-R33-006: re-declare every enclosing type, or the partial below lands at namespace
        // scope and is a different type from the entity - see UnsupportedNestingDescriptor.
        foreach (ContainingTypeInfo containing in entity.ContainingTypes)
            sb.AppendLine($"    {containing.AccessibilityKeyword} partial {containing.Keyword} {containing.Name}").AppendLine("    {");

        sb.AppendLine($"    {entity.AccessibilityKeyword} partial class {className} : IMapped<{className}>, IEntityMetadataSource, IGeneratedAccessors<{className}>");
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

        // AUD-R25: every property type not named explicitly in GetReaderTypeInfo lands in its
        // catch-all arm, and the catch-all used to emit a raw unboxing cast -
        // `(global::MyApp.Severity)reader.GetValue(ord[i])` on the IDataReader path, and
        // `dbReader.GetFieldValue<global::MyApp.Severity>(ord[i])` on the DbDataReader path, whose
        // base DbDataReader implementation is the same `(T)GetValue(ordinal)` cast. Unboxing a
        // boxed Int32 to an enum type throws InvalidCastException unconditionally in .NET - a
        // language-level guarantee, not provider behaviour - so a source-generated entity with an
        // enum property failed on every provider on the IDataReader path, and on any provider that
        // does not specialise GetFieldValue<T> for enums on the other. It worked only because the
        // one enum test entity was read through Microsoft.Data.Sqlite, which does specialise.
        //
        // Enums are the sharpest case but not the only one: DateOnly, TimeOnly, char, uint, ulong,
        // sbyte, ushort and byte[] all land here too, and all four scaffolder type mappers emit
        // DateOnly for `date` and TimeOnly for `time` columns - so default scaffolder output
        // routinely produces properties that reach this arm.
        //
        // ReadFallback<T> converts instead of casting. The reflection path has handled enums
        // deliberately all along (Enum.Parse with a numeric fallback, in
        // MetadataCache.CreateStringEnumSetter/CreateConvertingSetter); this brings the generated
        // path to the same place. The cost is that fallback-typed properties now read through
        // GetValue (which boxes) rather than GetFieldValue<T> on providers that specialise it -
        // paid only by types that were previously broken or provider-dependent.
        var needsFallbackHelper = properties.Any(p => GetReaderTypeInfo(p.TypeName).Getter == "reader.GetValue");
        if (needsFallbackHelper)
        {
            sb.AppendLine("        /// <summary>Converts a value read via GetValue to the property's type. See AUD-R25.</summary>");
            sb.AppendLine("        private static T ReadFallback<T>(object value)");
            sb.AppendLine("        {");
            sb.AppendLine("            if (value is T typed)");
            sb.AppendLine("                return typed;");
            sb.AppendLine();
            sb.AppendLine("            System.Type target = typeof(T);");
            sb.AppendLine();
            sb.AppendLine("            if (target.IsEnum)");
            sb.AppendLine("            {");
            sb.AppendLine("                return value is string enumText");
            sb.AppendLine("                    ? (T)System.Enum.Parse(target, enumText, true)");
            sb.AppendLine("                    : (T)System.Enum.ToObject(target, System.Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("        #if NET6_0_OR_GREATER");
            sb.AppendLine("            if (target == typeof(System.DateOnly))");
            sb.AppendLine("            {");
            sb.AppendLine("                return (T)(object)(value is string dateText");
            sb.AppendLine("                    ? System.DateOnly.Parse(dateText, System.Globalization.CultureInfo.InvariantCulture)");
            sb.AppendLine("                    : System.DateOnly.FromDateTime(System.Convert.ToDateTime(value, System.Globalization.CultureInfo.InvariantCulture)));");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (target == typeof(System.TimeOnly))");
            sb.AppendLine("            {");
            sb.AppendLine("                if (value is string timeText)");
            sb.AppendLine("                    return (T)(object)System.TimeOnly.Parse(timeText, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine("                if (value is System.TimeSpan timeSpan)");
            sb.AppendLine("                    return (T)(object)System.TimeOnly.FromTimeSpan(timeSpan);");
            sb.AppendLine("                return (T)(object)System.TimeOnly.FromDateTime(System.Convert.ToDateTime(value, System.Globalization.CultureInfo.InvariantCulture));");
            sb.AppendLine("            }");
            sb.AppendLine("        #endif");
            sb.AppendLine();
            sb.AppendLine("            // Convert.ChangeType cannot reach these - none of them is IConvertible-convertible");
            sb.AppendLine("            // from string - and a provider on the IDataReader path may well hand back text.");
            sb.AppendLine("            if (value is string text)");
            sb.AppendLine("            {");
            sb.AppendLine("                if (target == typeof(System.TimeSpan))");
            sb.AppendLine("                    return (T)(object)System.TimeSpan.Parse(text, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine("                if (target == typeof(System.DateTimeOffset))");
            sb.AppendLine("                    return (T)(object)System.DateTimeOffset.Parse(text, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine("                if (target == typeof(System.Guid))");
            sb.AppendLine("                    return (T)(object)System.Guid.Parse(text);");
            sb.AppendLine("            }");
            sb.AppendLine();
            sb.AppendLine("            if (target == typeof(System.DateTimeOffset) && value is System.DateTime dateTime)");
            sb.AppendLine("                return (T)(object)new System.DateTimeOffset(dateTime);");
            sb.AppendLine();
            sb.AppendLine("            if (target == typeof(System.Guid) && value is byte[] guidBytes)");
            sb.AppendLine("                return (T)(object)new System.Guid(guidBytes);");
            sb.AppendLine();
            sb.AppendLine("            return (T)System.Convert.ChangeType(value, target, System.Globalization.CultureInfo.InvariantCulture);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

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
            // ReadFallback<T> over GetValue for the catch-all types (TimeSpan/DateTimeOffset/
            // enums/unknown) - see the AUD-R25 note on the emitted helper above.
            var dbValue = ReadExpression(typeInfo, typeForGetFieldValue, "dbReader", i, isDbDataReader: true, p.IsEnum);
            if (needsNullCheck)
            {
                // AUD-R33-009: this used to be
                // `entity.P = dbReader.IsDBNull(ord[i]) ? default(T)! : value;`, which is not what
                // the IDataReader branch twenty lines below does, nor what the reflection twin
                // (MetadataCache's PropertyAccessor.Set) does - both leave the property untouched.
                // For a property with an initializer or constructor default
                // (`public string Name { get; set; } = "";`) a NULL column therefore reset it to
                // null on one path and preserved it on the other, for the same entity and the same
                // row, decided by nothing the caller can see - which of the two reader interfaces
                // their provider happens to implement. Skipping is the behaviour that agrees with
                // both of the other two implementations, so it is the one kept.
                sb.AppendLine($"                if (!dbReader.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                    entity.{p.PropertyName} = {dbValue};");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {dbValue};");
            }
        }
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        for (int i = 0; i < properties.Count; i++)
        {
            PropertyMetadata p = properties[i];
            ReaderTypeInfo typeInfo = GetReaderTypeInfo(p.TypeName);
            // IDataReader has no generic GetFieldValue<T>, so catch-all types are read via GetValue
            // and converted by ReadFallback<T>. This used to be a raw `({p.TypeName})reader.GetValue`
            // cast, which is where enums threw InvalidCastException on every provider - see the
            // AUD-R25 note on the emitted helper above.
            var value = ReadExpression(typeInfo, typeInfo.TypeForGetFieldValue, "reader", i, isDbDataReader: false, p.IsEnum);
            var needsNullCheck = typeInfo.NeedsNullCheck;

            // Fallback IDataReader path
            if (needsNullCheck)
            {
                sb.AppendLine($"                if (!reader.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                    entity.{p.PropertyName} = {value};");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {value};");
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
            var rowValue = ReadExpression(typeInfo, typeInfo.TypeForGetFieldValue, "rr", i, isDbDataReader: true, p.IsEnum);
            if (typeInfo.NeedsNullCheck)
            {
                // AUD-R33-009, as in ReadEntity above: skip rather than reset, matching the plain
                // IDataReader closure below and the reflection twin.
                sb.AppendLine($"                    if (!rr.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                        entity.{p.PropertyName} = {rowValue};");
            }
            else
            {
                sb.AppendLine($"                    entity.{p.PropertyName} = {rowValue};");
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
            var rowValue = ReadExpression(typeInfo, typeInfo.TypeForGetFieldValue, "r", i, isDbDataReader: false, p.IsEnum);
            if (typeInfo.NeedsNullCheck)
            {
                sb.AppendLine($"                if (!r.IsDBNull(ord[{i}]))");
                sb.AppendLine($"                    entity.{p.PropertyName} = {rowValue};");
            }
            else
            {
                sb.AppendLine($"                entity.{p.PropertyName} = {rowValue};");
            }
        }
        sb.AppendLine("                return entity;");
        sb.AppendLine("            };");
        sb.AppendLine("        }");
        sb.AppendLine();

        // AUD-R30: the write path must apply the same value conversion the reflection binder's
        // BuildValueConverter does - TypeHandlerRegistry first, then [EnumStorage] /
        // JauntyConfig.DefaultEnumStorage for enums. Before this, BindInsert/BindUpdate/BindDelete
        // handed the raw boxed enum to IDbDataParameter.Value: a string-stored enum entity READ
        // correctly (the generated reader parses names) but WROTE its numeric value back, and
        // registered type handlers were ignored entirely - so adding the generator package
        // silently changed write behaviour for the same entity.
        string BindParamCall(PropertyMetadata p)
        {
            string name = $"\"@{EscapeStringLiteral(p.ColumnName)}\"";
            if (!p.IsEnum)
                return $"AddParam(command, p, {name}, entity.{p.PropertyName});";
            string storage = p.EnumStorageOverride switch
            {
                1 => "global::Jaunty.Attributes.EnumStorage.String",
                0 => "global::Jaunty.Attributes.EnumStorage.Numeric",
                _ => "(global::Jaunty.Attributes.EnumStorage?)null",
            };
            return $"AddEnumParam(command, p, {name}, entity.{p.PropertyName}, {storage});";
        }

        // 2. BindInsert
        sb.AppendLine($"        public static void BindInsert(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => !x.IsIdentity && !x.IsComputed))
        {
            sb.AppendLine($"            {BindParamCall(p)}");
        }
        sb.AppendLine("        }");

        // 3. BindUpdate
        sb.AppendLine($"        public static void BindUpdate(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => !x.IsPrimaryKey && !x.IsIdentity && !x.IsComputed))
        {
            sb.AppendLine($"            {BindParamCall(p)}");
        }
        foreach (PropertyMetadata p in properties.Where(x => x.IsPrimaryKey))
        {
            sb.AppendLine($"            {BindParamCall(p)}");
        }
        sb.AppendLine("        }");

        // 4. BindDelete
        sb.AppendLine($"        public static void BindDelete(IDbCommand command, {className} entity)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = command.Parameters;");
        foreach (PropertyMetadata p in properties.Where(x => x.IsPrimaryKey))
        {
            sb.AppendLine($"            {BindParamCall(p)}");
        }
        sb.AppendLine("        }");

        sb.AppendLine("        private static void AddParam(IDbCommand cmd, IDataParameterCollection pc, string name, object? value)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = cmd.CreateParameter();");
        sb.AppendLine("            p.ParameterName = name;");
        sb.AppendLine("            // AUD-R30: route through the same type-handler/enum-storage conversion the");
        sb.AppendLine("            // reflection binder applies, via the public bridge in Jaunty core.");
        sb.AppendLine("            p.Value = global::Jaunty.Core.GeneratedBindingSupport.ToDbValue(value) ?? DBNull.Value;");
        sb.AppendLine("            pc.Add(p);");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        private static void AddEnumParam(IDbCommand cmd, IDataParameterCollection pc, string name, object? value, global::Jaunty.Attributes.EnumStorage? storage)");
        sb.AppendLine("        {");
        sb.AppendLine("            var p = cmd.CreateParameter();");
        sb.AppendLine("            p.ParameterName = name;");
        sb.AppendLine("            p.Value = global::Jaunty.Core.GeneratedBindingSupport.ToDbEnumValue(value, storage) ?? DBNull.Value;");
        sb.AppendLine("            pc.Add(p);");
        sb.AppendLine("        }");

        sb.AppendLine("        private static class OrdinalMap");
        sb.AppendLine("        {");
        sb.AppendLine("            private static readonly ConditionalWeakTable<IDataReader, CacheEntry> _cache = new();");
        sb.AppendLine("#if !NET8_0_OR_GREATER");
        sb.AppendLine("            private static readonly object _cacheWriteLock = new();");
        sb.AppendLine("#endif");
        // Thread-local: a shared static _last is constantly overwritten under concurrent access
        // from multiple threads reading different readers of the same entity type, degrading the
        // fast path to an almost-always-miss (falls through to the ConditionalWeakTable lookup on
        // every call). [ThreadStatic] gives each thread its own slot so its own repeated reads of
        // the same reader still hit the fast path regardless of what other threads are doing.
        // AUD-R34-032: weak, because the slot is per thread and is only ever overwritten by the
        // next resolve of the same entity type on that same thread. A strong reference pinned the
        // CacheEntry - and through its _reader field the reader, its command and its connection -
        // for as long as the thread lived, so a pooled or idle thread that mapped one entity and
        // then went quiet held the whole chain past Dispose for an unbounded time. The
        // ConditionalWeakTable beside it is deliberately weak for exactly this reason; this was the
        // one strong root undoing it. Weak costs one TryGetTarget on the fast path and nothing else:
        // while the reader is alive the CWT keeps the entry alive, so the memo still hits.
        sb.AppendLine("            [ThreadStatic]");
        sb.AppendLine("            private static WeakReference<CacheEntry>? _last;");
        sb.AppendLine();
        sb.AppendLine($"            public static int[] Resolve(IDataReader reader)");
        sb.AppendLine("            {");
        sb.AppendLine("                var lastRef = _last;");
        sb.AppendLine("                if (lastRef is not null && lastRef.TryGetTarget(out var last) && last.Matches(reader))");
        sb.AppendLine("                    return last.Ordinals;");
        sb.AppendLine();
        sb.AppendLine("                if (_cache.TryGetValue(reader, out var cached) && cached.Matches(reader))");
        sb.AppendLine("                {");
        sb.AppendLine("                    SetLast(cached);");
        sb.AppendLine("                    return cached.Ordinals;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine($"                var ords = new int[{properties.Count}];");
        for (int i = 0; i < properties.Count; i++)
        {
            sb.AppendLine($"                ords[{i}] = reader.GetOrdinal(\"{EscapeStringLiteral(properties[i].ColumnName)}\");");
        }
        sb.AppendLine("                var entry = new CacheEntry(reader, ords);");
        // Remove-then-Add on a ConditionalWeakTable races with itself (two threads both miss,
        // both remove, both add, second Add throws ArgumentException) - and this pair lives in
        // generated code the consumer cannot edit. The #if is resolved in the consumer's own
        // compilation, mirroring MetadataCache.GetSetters' fix.
        sb.AppendLine("#if NET8_0_OR_GREATER");
        sb.AppendLine("                _cache.AddOrUpdate(reader, entry);");
        sb.AppendLine("#else");
        sb.AppendLine("                lock (_cacheWriteLock)");
        sb.AppendLine("                {");
        sb.AppendLine("                    _cache.Remove(reader);");
        sb.AppendLine("                    _cache.Add(reader, entry);");
        sb.AppendLine("                }");
        sb.AppendLine("#endif");
        sb.AppendLine("                SetLast(entry);");
        sb.AppendLine("                return ords;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            private static void SetLast(CacheEntry entry)");
        sb.AppendLine("            {");
        sb.AppendLine("                var lastRef = _last;");
        sb.AppendLine("                if (lastRef is null)");
        sb.AppendLine("                    _last = new WeakReference<CacheEntry>(entry);");
        sb.AppendLine("                else");
        sb.AppendLine("                    lastRef.SetTarget(entry);");
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
        for (int i = 0; i < parameterMapProperties.Count; i++)
        {
            PropertyMetadata p = parameterMapProperties[i];
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

        // IGeneratedAccessors<T> - spec 009. Hands the members emitted above to Jaunty as delegates
        // so MappedCache<T>/WriteParameterCache<T> reach them by interface dispatch instead of
        // typeof(T).GetMethod(name). Each body is a static method group reference, which is an
        // ordinary IL call the trimmer must honour - that reference, and nothing else, is what keeps
        // these members alive on a trimmed or NativeAOT publish. Measured before this existed:
        // NativeAOT-Basic threw "No mapper found for type 'Product'" and Insert threw "No parameter
        // binder found", while the identical code on the JIT worked.
        //
        // Explicit implementations: these are the consumer's own entity classes, so the accessors
        // stay off their public surface, exactly as IEntityMetadataSource.Columns does above.
        sb.AppendLine();
        sb.AppendLine("        #if NET8_0_OR_GREATER");
        sb.AppendLine($"        Func<IDataReader, {className}> IGeneratedAccessors<{className}>.RowMapper => ReadEntity;");
        sb.AppendLine("        #else");
        // Below net8.0 ReadEntity is an instance member (see IMapped<T>), so the delegate has to
        // construct one. Per row, not once: the reflection fallback this replaces did
        // `r => openDelegate(new T(), r)`, and reusing a single instance across rows would change
        // behaviour for any ReadEntity that touches `this`.
        sb.AppendLine($"        Func<IDataReader, {className}> IGeneratedAccessors<{className}>.RowMapper => r => new {className}().ReadEntity(r);");
        sb.AppendLine("        #endif");
        sb.AppendLine($"        Func<IDataReader, Func<IDataReader, {className}>> IGeneratedAccessors<{className}>.RowMapperFactory => CreateRowMapper;");
        sb.AppendLine($"        Action<IDbCommand, {className}> IGeneratedAccessors<{className}>.InsertBinder => BindInsert;");
        sb.AppendLine($"        Action<IDbCommand, {className}> IGeneratedAccessors<{className}>.UpdateBinder => BindUpdate;");
        sb.AppendLine($"        Action<IDbCommand, {className}> IGeneratedAccessors<{className}>.DeleteBinder => BindDelete;");

        sb.AppendLine("    }");

        for (int i = 0; i < entity.ContainingTypes.Count; i++)
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
    /// The <c>[Table]</c> attribute the generator recognizes - Jaunty's or DataAnnotations' - or
    /// <see langword="null"/> when the type carries neither.
    /// </summary>
    /// <remarks>
    /// AUD-R34-031 (round-33 carry-forward). Entity discovery keys on the two attributes by
    /// <em>fully-qualified metadata name</em> (<c>ForAttributeWithMetadataName</c>, narrowed in
    /// AUD-R25), but the two places that ask a symbol about its <c>[Table]</c> after discovery went
    /// through <see cref="GetAttribute(ISymbol, string)"/>, which compares the simple name only. A
    /// consumer's own unrelated <c>TableAttribute</c> - the name is common enough that several
    /// libraries define one - therefore had two effects it should not have. On a hand-written
    /// <c>IMapped&lt;T&gt;</c> it suppressed the JAUNTYGEN002 warning, which exists precisely because
    /// that type is <em>not</em> generated and so is not trim-safe; and on a real entity carrying
    /// both a recognized <c>[Table]</c> and a foreign one, <c>FirstOrDefault</c> could read the table
    /// name and schema off whichever came first in source order, generating SQL against the wrong
    /// table with nothing reported.
    /// </remarks>
    private static AttributeData? GetRecognizedTableAttribute(ISymbol symbol)
        => symbol.GetAttributes().FirstOrDefault(static a =>
        {
            var name = a.AttributeClass?.ToDisplayString();
            return name == JauntyTableAttribute || name == DataAnnotationsTableAttribute;
        });

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

        // AUD-R34-031: the recognized two by fully-qualified name, matching discovery. A simple-name
        // match could read the name and schema off a foreign TableAttribute that happened to come
        // first in source order.
        AttributeData? tableAttr = GetRecognizedTableAttribute(classSymbol);
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
        // AUD-R25: one spelling per type, because only one is reachable. typeName always comes
        // from prop.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) at the single
        // call site that builds PropertyMetadata, and that format renders special types in keyword
        // form (int, long, string, ...) and everything else global::-qualified. The arms this table
        // used to carry for the BCL simple names ("Int32", "Boolean", ...) and for
        // namespace-qualified spellings without the global:: prefix ("System.Int32", "System.Guid",
        // ...) could therefore never match; they were residue from before the FullyQualifiedFormat
        // change whose own comment, at that call site, explains why the format was switched. They
        // were harmless but actively misleading - the table read as if it accepted several
        // spellings per type, so anyone adding a type had no way to tell which spelling actually
        // has to be present. Note also that FullyQualifiedFormat does not include the nullable
        // *reference* type modifier, so a `string?` property arrives here as "string"; only value
        // types get a "?" suffix.
        return typeName switch
        {
            // Non-nullable value types - no null check needed
            "int" => new("reader.GetInt32", "int", false),
            "long" => new("reader.GetInt64", "long", false),
            "bool" => new("reader.GetBoolean", "bool", false),
            "decimal" => new("reader.GetDecimal", "decimal", false),
            "double" => new("reader.GetDouble", "double", false),
            "float" => new("reader.GetFloat", "float", false),
            "short" => new("reader.GetInt16", "short", false),
            "byte" => new("reader.GetByte", "byte", false),
            "global::System.Guid" => new("reader.GetGuid", "Guid", false),
            "global::System.DateTime" => new("reader.GetDateTime", "DateTime", false),
            "global::System.TimeSpan" => new("reader.GetValue", "TimeSpan", false),
            "global::System.DateTimeOffset" => new("reader.GetValue", "DateTimeOffset", false),

            // Nullable value types - needs null check
            "int?" => new("reader.GetInt32", "int", true),
            "long?" => new("reader.GetInt64", "long", true),
            "bool?" => new("reader.GetBoolean", "bool", true),
            "decimal?" => new("reader.GetDecimal", "decimal", true),
            "double?" => new("reader.GetDouble", "double", true),
            "float?" => new("reader.GetFloat", "float", true),
            "short?" => new("reader.GetInt16", "short", true),
            "byte?" => new("reader.GetByte", "byte", true),
            "global::System.Guid?" => new("reader.GetGuid", "Guid", true),
            "global::System.DateTime?" => new("reader.GetDateTime", "DateTime", true),
            "global::System.TimeSpan?" => new("reader.GetValue", "TimeSpan", true),
            "global::System.DateTimeOffset?" => new("reader.GetValue", "DateTimeOffset", true),

            // Reference types - needs null check
            "string" => new("reader.GetString", "string", true),

            // Everything else (enums, DateOnly/TimeOnly, char, uint, ulong, sbyte, ushort,
            // byte[], ...) - needs null check, read through GetValue and convert.
            // TypeForGetFieldValue must be the actual (non-nullable-suffixed) type so the
            // DbDataReader fast path's call is directly assignable to the property without an
            // invalid cast from object (CS0266).
            _ => new("reader.GetValue", typeName.TrimEnd('?'), true)
        };
    }

    /// <summary>
    /// Builds the expression that reads one column into a property, for a given reader variable.
    /// </summary>
    /// <remarks>
    /// AUD-R25: the four emission sites (ReadEntity and CreateRowMapper, each with a
    /// <c>DbDataReader</c> and an <c>IDataReader</c> branch) previously each spelled this out
    /// themselves, and the two pairs disagreed about how a catch-all type should be read - the
    /// <c>DbDataReader</c> branches used <c>GetFieldValue&lt;T&gt;</c> and the <c>IDataReader</c>
    /// branches used a cast over <c>GetValue</c>. Both were unboxing casts underneath, and both were
    /// wrong for enums; sharing one builder is what keeps all four in step now that catch-all types
    /// convert rather than cast.
    /// </remarks>
    /// <param name="typeInfo">The reader access pattern resolved for the property type.</param>
    /// <param name="typeArgument">The non-nullable type to read as.</param>
    /// <param name="readerVariable">The name of the reader variable in the emitted scope.</param>
    /// <param name="ordinalIndex">The property's index into the generated ordinal map.</param>
    /// <param name="isDbDataReader">
    /// Whether the emitted scope's reader is a <c>DbDataReader</c>, and so has
    /// <c>GetFieldValue&lt;T&gt;</c>.
    /// </param>
    /// <param name="isEnum">Whether the property's type (or its nullable underlying type) is an enum.</param>
    private static string ReadExpression(
        ReaderTypeInfo typeInfo, string typeArgument, string readerVariable, int ordinalIndex, bool isDbDataReader, bool isEnum)
    {
        // Typed getters are unchanged - GetInt32, GetString and friends were never in question.
        if (typeInfo.Getter != "reader.GetValue")
            return $"{readerVariable}.{typeInfo.Getter.Substring("reader.".Length)}(ord[{ordinalIndex}])";

        // A catch-all type on the DbDataReader path keeps GetFieldValue<T>, because a provider that
        // specialises it converts better than anything emitted here can: Microsoft.Data.Sqlite
        // stores TimeSpan as a string and parses it back in its GetFieldValue<TimeSpan>, which no
        // general-purpose conversion over GetValue's boxed string would get right. The exception is
        // enums, where the base DbDataReader.GetFieldValue<T> is `(T)GetValue(ordinal)` - an
        // unboxing cast that throws for every enum on every provider that doesn't special-case it.
        if (isDbDataReader && !isEnum)
            return $"{readerVariable}.GetFieldValue<{typeArgument}>(ord[{ordinalIndex}])";

        // IDataReader has no GetFieldValue<T> at all, so every catch-all type here used to be a raw
        // unboxing cast over GetValue. ReadFallback<T> converts instead.
        return $"ReadFallback<{typeArgument}>({readerVariable}.GetValue(ord[{ordinalIndex}]))";
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
}
