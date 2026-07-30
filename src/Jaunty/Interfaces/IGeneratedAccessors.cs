using System.Data;

namespace Jaunty.Interfaces;

/// <summary>
/// Exposes a source-generated entity's mapper and parameter binders as delegates, so Jaunty can
/// reach them by interface dispatch instead of by reflection over method names.
/// </summary>
/// <typeparam name="T">The entity type this accessor belongs to.</typeparam>
/// <remarks>
/// <para>
/// Spec 009. <c>MappedCache&lt;T&gt;</c> and <c>WriteParameterCache&lt;T&gt;</c> used to locate the
/// generated <c>ReadEntity</c>, <c>CreateRowMapper</c> and <c>BindInsert</c>/<c>BindUpdate</c>/
/// <c>BindDelete</c> members with <c>typeof(T).GetMethod(name)</c>. Nothing arranged for those
/// members to survive trimming, so on a NativeAOT or trimmed publish the trimmer removed the very
/// methods the generator had emitted. Measured before this interface existed: <c>NativeAOT-Basic</c>
/// published with <c>PublishAot=true</c> threw <c>No mapper found for type 'Product'</c>, and an
/// <c>Insert</c> threw <c>No parameter binder found for type 'Widget'</c> - while the same code on
/// the JIT worked perfectly, confirming the members were generated and then trimmed.
/// </para>
/// <para>
/// Every implementation of a member below is a <em>static method group reference</em> - an ordinary
/// IL call the trimmer has to honour. That is the entire preservation mechanism: the members stay
/// because something statically references them, not because an attribute asserts they will.
/// </para>
/// <para>
/// This follows <see cref="IEntityMetadataSource"/>, which has always exposed generated entity
/// metadata the same way and consequently never needed a trim annotation or a suppression - see
/// <c>SourceGeneratedMetadataResolver.TryBuild&lt;T&gt;</c>. Metadata took the safe route; mappers
/// and binders took the reflective one and collected three
/// <c>[UnconditionalSuppressMessage]</c> attributes. This makes the second follow the first.
/// </para>
/// <para>
/// The members are <em>instance</em> members rather than <c>static abstract</c> ones deliberately.
/// An instance interface reached by a cast works identically on every target, including
/// netstandard2.0, where static abstract members do not exist and
/// <c>DynamicallyAccessedMembersAttribute</c> is unavailable. One mechanism on all targets is worth
/// more than a net8-only path that only AOT consumers ever exercise. Resolving it costs a single
/// <c>new T()</c> per closed generic - a cost <c>WriteParameterCache</c>'s metadata resolution
/// already pays.
/// </para>
/// <para>
/// Implemented automatically by the Jaunty source generator for every <c>[Table]</c> entity.
/// Hand-written implementations are supported but not expected; a type that implements
/// <see cref="IMapped{T}"/> without this interface still resolves through the reflection fallback,
/// and the generator reports <c>JAUNTYGEN002</c> to say that such a type will not survive trimming.
/// </para>
/// </remarks>
/// <seealso cref="IMapped{T}"/>
/// <seealso cref="IEntityMetadataSource"/>
public interface IGeneratedAccessors<T> where T : new()
{
    /// <summary>
    /// Maps the reader's current row to a new <typeparamref name="T"/>. Wraps the generated
    /// <c>ReadEntity</c>.
    /// </summary>
    /// <remarks>
    /// On targets where <c>ReadEntity</c> is a static member this is the method group itself. Where
    /// it is an instance member - anything below net8.0, per <see cref="IMapped{T}"/> - the delegate
    /// must construct a fresh <typeparamref name="T"/> <em>per row</em>, matching what the
    /// reflection fallback did. Reusing one instance across rows would change behaviour for any
    /// implementation that touches <c>this</c>.
    /// </remarks>
    Func<IDataReader, T> RowMapper { get; }

    /// <summary>
    /// Validates the reader's shape once and returns a per-row mapper for it. Wraps the generated
    /// <c>CreateRowMapper</c>; preferred over <see cref="RowMapper"/> by the dispatcher because it
    /// moves ordinal resolution out of the per-row path.
    /// </summary>
    Func<IDataReader, Func<IDataReader, T>> RowMapperFactory { get; }

    /// <summary>Binds an entity's values to an INSERT command. Wraps the generated <c>BindInsert</c>.</summary>
    Action<IDbCommand, T> InsertBinder { get; }

    /// <summary>Binds an entity's values to an UPDATE command. Wraps the generated <c>BindUpdate</c>.</summary>
    Action<IDbCommand, T> UpdateBinder { get; }

    /// <summary>Binds an entity's key values to a DELETE command. Wraps the generated <c>BindDelete</c>.</summary>
    Action<IDbCommand, T> DeleteBinder { get; }
}
