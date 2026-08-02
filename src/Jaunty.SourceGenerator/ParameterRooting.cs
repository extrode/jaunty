using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Jaunty.SourceGenerator;

/// <summary>
/// Spec 011. Emits trimmer-rooting calls for the parameter objects the consumer passes to Jaunty, so
/// their property getters survive a trimmed or NativeAOT publish.
/// </summary>
/// <remarks>
/// <para>
/// Jaunty reads a parameters object by reflecting over its public properties. Nothing in that path
/// tells the trimmer the getters are needed, so a NativeAOT publish removes them and binding fails at
/// runtime with <c>No property found on type 'X' matching SQL parameter '@Id'. Available properties:</c>
/// - the list empty, every getter gone. Spec 009 fixed the same class of defect for mappers and
/// binders by having the generated entity hold a static reference to its own members; that trick is
/// unavailable here, because a parameters object is not an entity, is frequently anonymous, and is
/// often a type Jaunty has never been told about.
/// </para>
/// <para>
/// What is available is the call site. The generator can see every <c>Query</c>/<c>Execute</c>
/// invocation in the consumer's own compilation and ask the semantic model what the parameters
/// argument's static type is. Emitting one
/// <c>JauntyAot.PreserveParameters&lt;ThatType&gt;()</c> per distinct type, from a
/// <c>[ModuleInitializer]</c>, preserves them all - with no change to consumer source.
/// </para>
/// <para>
/// Three measured facts shape this (<c>samples/NativeAOT-Basic</c>, osx-arm64, 2026-07-30):
/// </para>
/// <list type="number">
/// <item><description>
/// A <c>[DynamicallyAccessedMembers(PublicProperties)]</c> annotation on a generic type parameter
/// preserves its type argument program-wide. It does <b>not</b> have to be at the call site - which is
/// what makes this approach possible at all.
/// </description></item>
/// <item><description>
/// The rooting call must be in <b>reachable</b> code. The first version of this emitted the same calls
/// into an ordinary method nobody invoked; the trimmer removed the method, and the parameter types
/// failed exactly as before. Hence <c>[ModuleInitializer]</c>.
/// </description></item>
/// <item><description>
/// Anonymous type identity is <b>structural and per-assembly</b>, so <c>new { Id = default(int) }</c>
/// emitted here is the same type as <c>new { Id = 1 }</c> written in the consumer's file. That is the
/// only way to root a type that cannot be named - <c>PreserveParameters&lt;T&gt;()</c> and
/// <c>[DynamicDependency]</c> both need a name, and an anonymous type has none.
/// </description></item>
/// </list>
/// <para>
/// The limit is honest and unavoidable: this sees <em>static types at call sites</em>. A parameters
/// object held in an <c>object</c>-typed variable, or built by reflection, cannot be rooted from here,
/// and <c>JAUNTYGEN003</c> says so at the call site rather than letting it fail after publish.
/// </para>
/// </remarks>
public partial class JauntyGenerator
{
    /// <summary>Metadata name of the attribute that makes the emitted rooting method reachable.</summary>
    private const string ModuleInitializerAttribute = "System.Runtime.CompilerServices.ModuleInitializerAttribute";

    /// <summary>
    /// Reported when a Jaunty call site passes a parameters object whose type cannot be rooted from
    /// generated code, so its property getters may be trimmed. Spec 011.
    /// </summary>
    /// <remarks>
    /// Warning, not error, and for the same reason as <c>JAUNTYGEN002</c>: the code is correct on the
    /// JIT, and only a trimmed or NativeAOT publish is affected. It fires for JIT-only consumers who
    /// will never trim; if that proves noisy the fix is to gate it on the consumer's
    /// <c>PublishTrimmed</c>/<c>IsTrimmable</c> through a <c>CompilerVisibleProperty</c>, not to lower
    /// the severity, because the condition it reports is real.
    /// </remarks>
    private static readonly DiagnosticDescriptor UnrootableParameterTypeDescriptor = new(
        id: "JAUNTYGEN003",
        title: "Parameter object type cannot be preserved for trimming",
        messageFormat: "Jaunty binds this parameters object by reflecting over its public properties, but its static type here is '{0}', so the generator cannot tell the trimmer which properties to keep. A trimmed or NativeAOT publish can remove them, failing at runtime with \"No property found on type ... Available properties:\" and an empty list. Wrap the argument in JauntyAot.Parameters(...) at this call site, or call JauntyAot.PreserveParameters<T>() once at startup for the concrete type.",
        category: "JauntySourceGenerator",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Registers the parameter-rooting half of the generator. Called from <c>Initialize</c>.
    /// </summary>
    private static void RegisterParameterRooting(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<ParameterSite?> sites = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => IsCandidateParameterCall(node),
            transform: static (ctx, _) => FindParameterSite(ctx));

        // Emitting a module initializer needs both the attribute and C# 9. Selecting a bool out of the
        // compilation keeps this from defeating incremental caching: CompilationProvider yields a new
        // object on every keystroke, but a bool that has not changed compares equal, so downstream
        // work is skipped anyway.
        IncrementalValueProvider<bool> canEmit = context.CompilationProvider.Select(static (compilation, _) =>
            compilation.GetTypeByMetadataName(ModuleInitializerAttribute) is not null
            && compilation is CSharpCompilation { LanguageVersion: >= LanguageVersion.CSharp9 });

        IncrementalValueProvider<ImmutableArray<ParameterRoot>> roots = sites
            .Where(static site => site is { Root: not null })
            .Select(static (site, _) => site!.Value.Root!.Value)
            .Collect();

        context.RegisterSourceOutput(roots.Combine(canEmit), static (spc, pair) =>
        {
            if (!pair.Right || pair.Left.IsDefaultOrEmpty)
                return;

            spc.AddSource("JauntyAotParameterRoots.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(
                GenerateParameterRoots(pair.Left), Encoding.UTF8));
        });

        context.RegisterSourceOutput(
            sites.Where(static site => site is { UnrootableType: not null }),
            static (spc, site) => spc.ReportDiagnostic(Diagnostic.Create(
                UnrootableParameterTypeDescriptor, site!.Value.Location, site.Value.UnrootableType)));
    }

    /// <summary>
    /// One rooting call to emit. A value type holding no symbols, so the incremental driver can
    /// compare two results and skip re-emitting when nothing relevant changed.
    /// </summary>
    private readonly struct ParameterRoot : IEquatable<ParameterRoot>
    {
        public ParameterRoot(string expression, bool isWitness)
        {
            Expression = expression;
            IsWitness = isWitness;
        }

        /// <summary>
        /// Either a fully-qualified type name to use as a type argument, or - when
        /// <see cref="IsWitness"/> - an object-creation expression whose type is to be preserved.
        /// </summary>
        public string Expression { get; }

        /// <summary>
        /// True for an anonymous type, which has no name and so must be rooted through the overload
        /// that infers <c>T</c> from a throwaway instance of the same structural shape.
        /// </summary>
        public bool IsWitness { get; }

        public bool Equals(ParameterRoot other)
            => IsWitness == other.IsWitness && string.Equals(Expression, other.Expression, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is ParameterRoot other && Equals(other);

        public override int GetHashCode()
            => unchecked((Expression?.GetHashCode() ?? 0) * 397) ^ (IsWitness ? 1 : 0);
    }

    /// <summary>
    /// What one Jaunty call site's parameters argument turned out to be: something to root, something
    /// to warn about, or neither.
    /// </summary>
    private readonly struct ParameterSite : IEquatable<ParameterSite>
    {
        private ParameterSite(ParameterRoot? root, string? unrootableType, Location? location)
        {
            Root = root;
            UnrootableType = unrootableType;
            Location = location;
        }

        public static ParameterSite Rootable(string expression, bool isWitness)
            => new(new ParameterRoot(expression, isWitness), unrootableType: null, location: null);

        public static ParameterSite Unrootable(string typeDisplay, Location location)
            => new(root: null, typeDisplay, location);

        public ParameterRoot? Root { get; }
        public string? UnrootableType { get; }
        public Location? Location { get; }

        public bool Equals(ParameterSite other)
            => Nullable.Equals(Root, other.Root)
                && string.Equals(UnrootableType, other.UnrootableType, StringComparison.Ordinal)
                && Equals(Location, other.Location);

        public override bool Equals(object? obj) => obj is ParameterSite other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Root?.GetHashCode() ?? 0;
            hash = unchecked((hash * 397) ^ (UnrootableType?.GetHashCode() ?? 0));
            return unchecked((hash * 397) ^ (Location?.GetHashCode() ?? 0));
        }
    }

    /// <summary>
    /// Syntax-only, deliberately cheap first filter: an invocation of something named
    /// <c>Query…</c>, <c>Execute…</c> or <c>Values</c> with enough arguments to carry a parameters
    /// object.
    /// </summary>
    /// <remarks>
    /// This runs for every invocation in the compilation on every keystroke, so it must not touch the
    /// semantic model. Matching on the name at the use site is imprecise on purpose - a consumer's own
    /// <c>ExecuteFoo</c> passes this filter and is rejected by
    /// <see cref="FindParameterSite"/> once the symbol is resolved.
    /// </remarks>
    private static bool IsCandidateParameterCall(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        int arity = invocation.ArgumentList.Arguments.Count;
        if (arity == 0)
            return false;

        string? name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
            return false;

        // Fluent's Values(object) and Set(object) carry the parameters object as their only
        // argument; everything in core takes the SQL first, so it needs at least two.
        //
        // AUD-R34-025: Set and the three Raw predicates were missing. All of them reach
        // ParameterCache.Get(x.GetType()) and prop.Getter(x) - InsertBuilder.cs:61,
        // QueryBuilder.cs:1666/2053/2073 - i.e. exactly the reflection path this generator exists
        // to protect, and a consumer writing .Set(new { Name = "x" }) got a clean build, no
        // JAUNTYGEN003, and a trimmed publish that threw with an empty property list.
        return name.Equals("Values", StringComparison.Ordinal)
            || name.Equals("Set", StringComparison.Ordinal)
            || (arity >= 2
                && (name.StartsWith("Query", StringComparison.Ordinal)
                    || name.StartsWith("Execute", StringComparison.Ordinal)
                    || name.Equals("WhereRaw", StringComparison.Ordinal)
                    || name.Equals("AndRaw", StringComparison.Ordinal)
                    || name.Equals("OrRaw", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Resolves a candidate call site to the rooting it needs, a <c>JAUNTYGEN003</c> to report, or
    /// <see langword="null"/> when it is not a Jaunty parameter-binding call or needs nothing.
    /// </summary>
    private static ParameterSite? FindParameterSite(GeneratorSyntaxContext ctx)
    {
        var invocation = (InvocationExpressionSyntax)ctx.Node;

        if (ctx.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return null;

        if (!IsJauntyMethod(method))
            return null;

        if (FindParametersParameter(method, out bool isSequence) is not { } parameter)
            return null;

        if (FindArgumentFor(invocation, method, parameter) is not { } argument)
            return null;

        // A compile-time null needs nothing: the binder short-circuits before touching properties.
        // Asked of the semantic model rather than the syntax, because the library's own no-parameter
        // overloads spell it `(object?)null` - a cast expression, not a null literal - and a syntax
        // check missed all 20 of them, reporting JAUNTYGEN003 against Jaunty's own source.
        Optional<object?> constant = ctx.SemanticModel.GetConstantValue(argument);
        if (constant.HasValue && constant.Value is null)
            return null;

        ITypeSymbol? type = ctx.SemanticModel.GetTypeInfo(argument).Type;

        if (type is null || type.TypeKind == TypeKind.Error)
            return null;

        // AUD-R34-027: a batch call site binds each element, so it is the element type that has to
        // be rooted. An unknowable element type (`IEnumerable<object>` forwarded through) falls to
        // the erasure branch below and is excused or reported there, same as the scalar case.
        if (isSequence)
        {
            if (ElementTypeOf(type) is not { } element)
                return null;

            type = element;
        }

        // The two shapes that reach no property reflection at all, so nothing has to be preserved:
        // a dictionary (bound by key) and a scalar (bound by the name in the SQL text).
        if (IsDictionaryShape(type) || IsScalarShape(type))
            return null;

        // `object` and `dynamic` erase the very thing this needs; a type parameter is not knowable
        // here either. These are the cases only the consumer can fix - except when the call site is
        // merely forwarding, where nobody can fix it there and saying so is noise.
        if (type.SpecialType == SpecialType.System_Object
            || type.TypeKind is TypeKind.Dynamic or TypeKind.TypeParameter)
        {
            return IsForwardedParameter(ctx, argument)
                ? null
                : ParameterSite.Unrootable(type.ToDisplayString(), argument.GetLocation());
        }

        if (type.IsAnonymousType)
        {
            return BuildAnonymousWitness(type) is { } witness
                ? ParameterSite.Rootable(witness, isWitness: true)
                : ParameterSite.Unrootable(type.ToDisplayString(), argument.GetLocation());
        }

        return IsNameableFrom(type)
            ? ParameterSite.Rootable(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), isWitness: false)
            : ParameterSite.Unrootable(type.ToDisplayString(), argument.GetLocation());
    }

    /// <summary>
    /// Whether the argument is just the enclosing method's own parameter being passed along.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Forwarding is the overwhelmingly common shape for an <c>object</c>-typed parameters argument,
    /// and it is the one shape where <c>JAUNTYGEN003</c> would be useless: the concrete type is not
    /// knowable at a forwarding site, so there is nothing the author of that line can do about it. The
    /// place worth warning is where a concrete type existed and was erased - <c>object p = new { Id =
    /// 1 };</c> and then a call with <c>p</c> - which this still reports, because a local is not a
    /// parameter.
    /// </para>
    /// <para>
    /// Measured, and the reason this exists: without it the diagnostic fired 170 times inside Jaunty's
    /// own source - every internal overload that hands <c>object? parameters</c> to the next one - and,
    /// because the library builds warnings-as-errors, broke its own build. A consumer with any
    /// data-access wrapper would have seen the same thing.
    /// </para>
    /// <para>
    /// A forwarded <em>type parameter</em> is excused too, and that was not the first answer. Reporting
    /// it looked strictly better - a consumer's generic <c>Repository&lt;T&gt;</c> wrapper has a real,
    /// specific fix in putting <c>[DynamicallyAccessedMembers]</c> on its own type parameter - until it
    /// was measured against the library: Jaunty's own generic write plumbing hands the entity to
    /// <c>CommandObservation.Execute(sql, parameters, ...)</c>, whose <c>parameters</c> feeds
    /// interceptors and logging rather than binding. Trimmed getters there cost log detail, not a failed
    /// query. The two shapes are syntactically identical, so telling them apart would mean guessing, and
    /// the guess would fire six times inside Jaunty on the benign one.
    /// </para>
    /// <para>
    /// This leaves one gap that no rule here can close, and it is stated rather than hidden: a
    /// consumer method that takes the parameters object as <c>object</c> or as its own type parameter
    /// and forwards it is invisible from both ends - the inner call site is excused here, and the outer
    /// call site is a call to the consumer's own method, which this generator has no reason to look at.
    /// Such a call chain needs <c>JauntyAot.PreserveParameters&lt;T&gt;()</c>, and it is documented on
    /// <c>Jaunty.JauntyAot</c>.
    /// </para>
    /// </remarks>
    private static bool IsForwardedParameter(GeneratorSyntaxContext ctx, ExpressionSyntax argument)
    {
        if (Unwrap(argument) is not IdentifierNameSyntax identifier)
            return false;

        return ctx.SemanticModel.GetSymbolInfo(identifier).Symbol is IParameterSymbol;
    }

    /// <summary>
    /// Strips the syntax that wraps an identifier without changing what it refers to.
    /// </summary>
    /// <remarks>
    /// <c>parameters!</c> is a postfix null-forgiving expression, not an identifier, and missing that
    /// made the forwarding check fail for the single most likely spelling in nullable-enabled code -
    /// caught by a test whose probe happened to write it that way.
    /// </remarks>
    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (true)
        {
            switch (expression)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    expression = parenthesized.Expression;
                    break;

                case PostfixUnaryExpressionSyntax postfix when postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    expression = postfix.Operand;
                    break;

                default:
                    return expression;
            }
        }
    }

    /// <summary>Whether <paramref name="method"/> is one of Jaunty's own APIs.</summary>
    /// <remarks>
    /// Checked on the namespace rather than the assembly name, so that Jaunty's own test projects -
    /// whose assemblies are also called <c>Jaunty.*</c> - do not match their own helpers.
    /// </remarks>
    private static bool IsJauntyMethod(IMethodSymbol method)
    {
        string? ns = method.ContainingType?.ContainingNamespace?.ToDisplayString();

        return ns is not null
            && (ns.Equals("Jaunty", StringComparison.Ordinal) || ns.StartsWith("Jaunty.", StringComparison.Ordinal));
    }

    /// <summary>
    /// Finds the parameter that carries the parameters object, by the names the library uses for
    /// it. <paramref name="isSequence"/> is set when the parameter carries a <em>sequence</em> of
    /// parameter objects rather than one, in which case it is the element type that has to be
    /// rooted.
    /// </summary>
    /// <remarks>
    /// AUD-R34-027. <c>ExecuteBatch</c>/<c>ExecuteBatchAsync</c> pass the syntax predicate - the
    /// name starts with <c>Execute</c> and the arity is >= 2 - and then fell out here, because
    /// their parameter is <c>IEnumerable&lt;object&gt; parameterSets</c>: neither the type nor the
    /// name matched. Every set in the batch is bound by property reflection through the same
    /// <c>ParameterCache</c> path, and the element type is usually knowable at the call site
    /// (<c>ExecuteBatch(sql, new[] { new { Id = 1 } })</c>), so the site was rootable in principle
    /// and was instead silently skipped with no JAUNTYGEN003.
    /// </remarks>
    private static IParameterSymbol? FindParametersParameter(IMethodSymbol method, out bool isSequence)
    {
        isSequence = false;

        foreach (IParameterSymbol parameter in method.Parameters)
        {
            if (parameter.Type.SpecialType == SpecialType.System_Object
                && (parameter.Name.Equals("parameters", StringComparison.Ordinal)
                    || parameter.Name.Equals("values", StringComparison.Ordinal)))
            {
                return parameter;
            }

            if (parameter.Name.Equals("parameterSets", StringComparison.Ordinal)
                && ElementTypeOf(parameter.Type) is not null)
            {
                isSequence = true;
                return parameter;
            }
        }

        return null;
    }

    /// <summary>
    /// The element type of an array or of anything implementing <c>IEnumerable&lt;T&gt;</c>, or
    /// <see langword="null"/> when the type is neither. AUD-R34-027.
    /// </summary>
    private static ITypeSymbol? ElementTypeOf(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
            return array.ElementType;

        if (type is not INamedTypeSymbol named)
            return null;

        if (named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
            && named.TypeArguments.Length == 1)
        {
            return named.TypeArguments[0];
        }

        foreach (INamedTypeSymbol iface in named.AllInterfaces)
        {
            if (iface.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T
                && iface.TypeArguments.Length == 1)
            {
                return iface.TypeArguments[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Returns the argument expression bound to <paramref name="parameter"/>, or
    /// <see langword="null"/> if it cannot be determined.
    /// </summary>
    /// <remarks>
    /// Positional index into <c>method.Parameters</c> lines up with the argument list for both call
    /// forms: a reduced extension method excludes the receiver from both, and a static call includes
    /// it in both. Returning <see langword="null"/> when the shape is not understood loses a rooting
    /// rather than emitting a wrong one - the consumer sees the runtime failure they would have seen
    /// anyway, not a new compile error.
    /// </remarks>
    private static ExpressionSyntax? FindArgumentFor(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        IParameterSymbol parameter)
    {
        int index = method.Parameters.IndexOf(parameter);
        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;

        for (int i = 0; i < arguments.Count; i++)
        {
            ArgumentSyntax argument = arguments[i];

            if (argument.NameColon is { } named)
            {
                if (named.Name.Identifier.ValueText.Equals(parameter.Name, StringComparison.Ordinal))
                    return argument.Expression;
            }
            else if (i == index)
            {
                return argument.Expression;
            }
        }

        return null;
    }

    /// <summary>
    /// Rebuilds an anonymous type as an object-creation expression with the identical structural
    /// shape, which by C#'s anonymous type identity rules <em>is</em> the same type within the
    /// assembly. Returns <see langword="null"/> if any part of the shape cannot be written.
    /// </summary>
    /// <remarks>
    /// Property order is part of the identity, so <c>GetMembers()</c>'s declaration order is
    /// preserved. A property whose own type is anonymous recurses; <paramref name="depth"/> bounds
    /// that against a shape the generator cannot express rather than against real recursion, which
    /// C# does not permit.
    /// </remarks>
    private static string? BuildAnonymousWitness(ITypeSymbol type, int depth = 0)
    {
        if (depth > 4)
            return null;

        var builder = new StringBuilder("new { ");
        bool first = true;

        foreach (ISymbol member in type.GetMembers())
        {
            if (member is not IPropertySymbol property)
                continue;

            string? value = property.Type.IsAnonymousType
                ? BuildAnonymousWitness(property.Type, depth + 1)
                : IsNameableFrom(property.Type)
                    ? $"default({property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)})"
                    : null;

            if (value is null)
                return null;

            if (!first)
                builder.Append(", ");

            builder.Append(property.Name).Append(" = ").Append(value);
            first = false;
        }

        // A property-less anonymous type binds no parameters, so there is nothing to preserve.
        return first ? null : builder.Append(" }").ToString();
    }

    /// <summary>
    /// Whether the type can be written by name in a generated file compiled into the same assembly as
    /// the call site.
    /// </summary>
    /// <remarks>
    /// Generated code sits in its own file, so a <c>file</c>-local type is out of reach even though it
    /// is in the same assembly; a <c>private</c> or <c>protected</c> nested type is out of reach from
    /// a top-level class. <c>internal</c> is fine - same assembly - which is why this is not simply a
    /// public-only check.
    /// </remarks>
    private static bool IsNameableFrom(ITypeSymbol type)
    {
        // AUD-R34-026: an array type IS writable by name - `default(global::System.Int32[])`
        // compiles - and rejecting it made BuildAnonymousWitness abandon the witness for the whole
        // anonymous type, so `new { Ids = ids }` (the idiomatic shape for Jaunty's own IN-clause
        // expansion) was reported as JAUNTYGEN003 and nothing was rooted, while the same object
        // with a List<int> rooted fine. Accessibility is the element type's question, and it is
        // asked, exactly as TypeArguments already is below.
        if (type is IArrayTypeSymbol array)
            return IsNameableFrom(array.ElementType);

        if (type is IPointerTypeSymbol or ITypeParameterSymbol or IFunctionPointerTypeSymbol)
            return false;

        if (type is not INamedTypeSymbol named)
            return false;

        if (named.IsFileLocal || named.IsAnonymousType || named.IsTupleType && named.TupleElements.Length == 0)
            return false;

        for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
                return false;
        }

        foreach (ITypeSymbol argument in named.TypeArguments)
        {
            if (!IsNameableFrom(argument))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the binder will treat this as a dictionary of named values, which reaches no property
    /// reflection.
    /// </summary>
    private static bool IsDictionaryShape(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { MetadataName: "IDictionary`2" or "Dictionary`2" or "IReadOnlyDictionary`2" })
            return true;

        foreach (INamedTypeSymbol iface in type.AllInterfaces)
        {
            if (iface.MetadataName is "IDictionary`2" or "IReadOnlyDictionary`2")
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the binder will treat this as a single scalar value, taking the parameter name from the
    /// SQL text instead of from a property.
    /// </summary>
    /// <remarks>
    /// Kept in step with <c>ParameterBinder.IsScalarType</c>. Being wrong in the permissive direction
    /// costs a redundant rooting call, which is harmless; being wrong the other way would emit a
    /// witness expression for a type that never needed one, which is equally harmless. Neither can
    /// produce a wrong binding, so this does not have to be exact.
    /// </remarks>
    private static bool IsScalarShape(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
            return true;

        if (type.SpecialType is not SpecialType.None)
            return type.SpecialType != SpecialType.System_Object;

        // Nullable<T> of a scalar is a scalar.
        if (type is INamedTypeSymbol { MetadataName: "Nullable`1" } nullable && nullable.TypeArguments.Length == 1)
            return IsScalarShape(nullable.TypeArguments[0]);

        return type.ToDisplayString() is "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan"
            or "System.Guid" or "System.DateOnly" or "System.TimeOnly" or "System.Numerics.BigInteger"
            or "byte[]";
    }

    /// <summary>
    /// Emits the module initializer holding one rooting call per distinct parameter type.
    /// </summary>
    /// <remarks>
    /// Sorted and de-duplicated so the emitted text depends only on the set of types, not on the order
    /// the driver happened to visit files in - two builds of unchanged source must produce identical
    /// output.
    /// </remarks>
    private static string GenerateParameterRoots(ImmutableArray<ParameterRoot> roots)
    {
        var typeArguments = new SortedSet<string>(StringComparer.Ordinal);
        var witnesses = new SortedSet<string>(StringComparer.Ordinal);

        foreach (ParameterRoot root in roots)
        {
            _ = root.IsWitness ? witnesses.Add(root.Expression) : typeArguments.Add(root.Expression);
        }

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable disable");
        sb.AppendLine();
        sb.AppendLine("namespace Jaunty.Generated");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Tells the trimmer to keep the public properties of every type this assembly passes to a");
        sb.AppendLine("    /// Jaunty API as a parameters object. Generated; see Jaunty.JauntyAot and spec 011.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    internal static class JauntyAotParameterRoots");
        sb.AppendLine("    {");
        sb.AppendLine("        // [ModuleInitializer] is load-bearing, not tidiness: an annotation inside a method");
        sb.AppendLine("        // nothing calls is removed by the trimmer along with the method, and preserves nothing.");
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void PreserveParameterTypes()");
        sb.AppendLine("        {");

        foreach (string typeArgument in typeArguments)
            sb.AppendLine($"            global::Jaunty.JauntyAot.PreserveParameters<{typeArgument}>();");

        foreach (string witness in witnesses)
        {
            // Anonymous types cannot be named, so the type is supplied by inference from a throwaway
            // instance of the same structural shape - which C# unifies with the consumer's own.
            sb.AppendLine($"            global::Jaunty.JauntyAot.PreserveParameters({witness});");
        }

        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}
