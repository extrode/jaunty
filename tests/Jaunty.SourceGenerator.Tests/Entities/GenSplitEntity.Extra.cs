using System;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-3): the second declaration of GenSplitEntity - see GenSplitEntity.cs for the finding.
// It carries an attribute that is *not* [Table], which is what used to make it a candidate: the old
// syntactic pre-filter only asked for AttributeLists.Count > 0, and the semantic filter then asked
// the merged symbol, which does carry [Table] from the other part. Under B8-7's
// ForAttributeWithMetadataName pipeline this part is simply not offered, which is the better
// outcome; the attribute is kept so the file still stands as the record of the shape that broke.
[Serializable]
public partial class GenSplitEntity
{
    public string Describe() => $"{Id}:{Name}";
}
