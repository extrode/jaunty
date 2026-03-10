namespace Jaunty.Fluent.Internals;

/// <summary>
/// Cache key for SQL generation caching.
/// </summary>
internal readonly struct CacheKey : IEquatable<CacheKey>
{
    public readonly Type EntityType;
    public readonly string[] Columns;
    public readonly string WhereSignature;
    public readonly string OrderBySignature;
    public readonly bool Distinct;
    public readonly bool HasSkip;
    public readonly bool HasTake;
    public readonly int HashCode;

    public CacheKey(
        Type entityType,
        string[] columns,
        string whereSignature,
        string orderBySignature,
        bool distinct,
        bool hasSkip,
        bool hasTake)
    {
        EntityType = entityType;
        Columns = columns;
        WhereSignature = whereSignature;
        OrderBySignature = orderBySignature;
        Distinct = distinct;
        HasSkip = hasSkip;
        HasTake = hasTake;

        // Pre-compute hash code for performance
        HashCode = ComputeHashCode();
    }

    public bool Equals(CacheKey other)
    {
        return EntityType == other.EntityType
            && Columns.SequenceEqual(other.Columns)
            && WhereSignature == other.WhereSignature
            && OrderBySignature == other.OrderBySignature
            && Distinct == other.Distinct
            && HasSkip == other.HasSkip
            && HasTake == other.HasTake;
    }

    public override bool Equals(object? obj) => obj is CacheKey other && Equals(other);

    public override int GetHashCode() => HashCode;

    private int ComputeHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + EntityType.GetHashCode();
            
            foreach (var col in Columns)
            {
                hash = hash * 31 + col.GetHashCode();
            }
            
            hash = hash * 31 + WhereSignature.GetHashCode();
            hash = hash * 31 + OrderBySignature.GetHashCode();
            hash = hash * 31 + Distinct.GetHashCode();
            hash = hash * 31 + HasSkip.GetHashCode();
            hash = hash * 31 + HasTake.GetHashCode();
            
            return hash;
        }
    }
}
