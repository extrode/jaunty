using Jaunty.SourceGenerator.Tests.Entities.HintCollision.NsA;
using Jaunty.SourceGenerator.Tests.Entities.HintCollision.NsA.Widget;

namespace Jaunty.SourceGenerator.Tests;

/// <summary>
/// R16 audit finding: GetHintName escaped every non-alphanumeric character (dots and literal
/// underscores alike) to a single '_', so a literal underscore in a class name could line up
/// with a namespace-segment dot elsewhere and flatten two distinct fully-qualified type names to
/// the same hint name, crashing AddSource with a duplicate-hint-name exception at compile time.
/// If GetHintName regressed, this test project would fail to build rather than fail a test - both
/// entities compiling and producing distinct generated TableName statics is the proof the
/// collision no longer happens.
/// </summary>
public sealed class HintNameCollisionTests
{
    [Fact]
    public void CollidingFullyQualifiedNames_BothGenerateDistinctMappers()
    {
        Assert.Equal("gen_hint_collision_widget_alpha", Widget_Alpha.TableName);
        Assert.Equal("gen_hint_collision_alpha", Alpha.TableName);
    }
}
