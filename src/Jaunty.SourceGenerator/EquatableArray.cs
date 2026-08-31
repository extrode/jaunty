using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Jaunty.SourceGenerator;

/// <summary>
/// A value-equatable wrapper around <see cref="ImmutableArray{T}"/>.
/// </summary>
/// <remarks>
/// AUD-R25 (B8-7): an incremental generator's model objects are compared by the Roslyn driver to
/// decide whether downstream steps can be skipped, and <see cref="ImmutableArray{T}"/> implements
/// <see cref="IEquatable{T}"/> by comparing the *underlying array reference*. A model holding a
/// bare <see cref="ImmutableArray{T}"/> therefore never compares equal to a freshly-built one with
/// identical contents, so every comparison reports "changed" and every downstream step re-runs -
/// exactly the outcome the model exists to avoid. This wrapper compares element by element.
/// </remarks>
/// <typeparam name="T">The element type, which must itself be value-equatable.</typeparam>
internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _values;

    public EquatableArray(ImmutableArray<T> values) => _values = values;

    public int Count => _values.IsDefault ? 0 : _values.Length;

    public T this[int index] => _values[index];

    public bool Equals(EquatableArray<T> other)
    {
        if (_values.IsDefault || other._values.IsDefault)
            return _values.IsDefault && other._values.IsDefault;

        if (_values.Length != other._values.Length)
            return false;

        for (int i = 0; i < _values.Length; i++)
        {
            if (!_values[i].Equals(other._values[i]))
                return false;
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_values.IsDefault)
            return 0;

        // Unchecked because overflow is the intended wraparound, not an error; the constants are
        // the usual 17/31 seed-and-multiply pair.
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < _values.Length; i++)
                hash = (hash * 31) + (_values[i]?.GetHashCode() ?? 0);
            return hash;
        }
    }

    /// <summary>
    /// Returns <see cref="ImmutableArray{T}"/>'s own struct enumerator, which <c>foreach</c> binds
    /// to by pattern before it considers <see cref="IEnumerable{T}"/>.
    /// </summary>
    /// <remarks>
    /// AUD-R35-225. This used to cast to <see cref="IEnumerable{T}"/> first, which boxes the struct
    /// enumerator - one allocation per <c>foreach</c>, paid by every emit-time loop over an entity's
    /// properties, containing types and dropped properties. The interface implementations below
    /// still box, but only for callers that reach the type through an interface; nothing in
    /// <c>JauntyGenerator</c> does. A default instance is materialised as empty rather than being
    /// enumerated directly, which would throw.
    /// </remarks>
    public ImmutableArray<T>.Enumerator GetEnumerator()
        => (_values.IsDefault ? ImmutableArray<T>.Empty : _values).GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
        => ((IEnumerable<T>)(_values.IsDefault ? ImmutableArray<T>.Empty : _values)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable)(_values.IsDefault ? ImmutableArray<T>.Empty : _values)).GetEnumerator();

    public static bool operator ==(EquatableArray<T> left, EquatableArray<T> right) => left.Equals(right);

    public static bool operator !=(EquatableArray<T> left, EquatableArray<T> right) => !left.Equals(right);
}
