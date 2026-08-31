namespace NativeAOT.FluentQuery;

// A grouped-projection result type. Unlike Product this is not an entity: it has no [Table], the
// source generator emits nothing for it, and Jaunty reaches its members by reflection in
// GroupedJoinedResultMapper.
//
// It stays AOT-safe because the Select<TResult> overloads annotate TResult with
// [DynamicallyAccessedMembers(PublicProperties | PublicConstructors)], so the trimmer keeps these
// setters. Nothing needs to be declared here - which is the point of putting the annotation on the
// API rather than asking every consumer to root their own DTOs.
public class PriceBand
{
    public bool Discontinued { get; set; }

    public int Count { get; set; }

    public decimal Highest { get; set; }
}
