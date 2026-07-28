using System;

namespace Jaunty.SourceGenerator.Tests.Entities;

// AUD-R25 (B8-3): the second declaration of GenSplitEntity - see GenSplitEntity.cs for the finding.
// It deliberately carries an attribute that is *not* [Table], because that is what makes it a
// candidate: the syntactic pre-filter only asks for AttributeLists.Count > 0, and the semantic
// filter asks the merged symbol, which does carry [Table] from the other part.
[Serializable]
public partial class GenSplitEntity
{
    public string Describe() => $"{Id}:{Name}";
}
