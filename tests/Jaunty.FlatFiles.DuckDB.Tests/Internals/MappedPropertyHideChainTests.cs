using System.Reflection;

using Jaunty.Attributes;
using Jaunty.FlatFiles.DuckDB.Internals;

namespace Jaunty.FlatFiles.DuckDB.Tests.Internals;

/// <summary>
/// AUD-R35-028. <c>GetMappedProperties</c> applied <c>IsMapped</c> per declaration and collapsed the
/// hide chain afterwards, so a <c>new</c>-shadowed declaration carrying <c>[Ignore]</c> or
/// <c>[NotMapped]</c> was dropped and the base declaration - which the attribute was written to
/// suppress - was admitted in its place. The property was mapped anyway, under the base's CLR type.
/// </summary>
public class MappedPropertyHideChainTests
{
    private class IgnoreBase
    {
        public int Id { get; set; }
        public object Code { get; set; } = "";
    }

    private sealed class IgnoreDerived : IgnoreBase
    {
        [Ignore]
        public new string Code { get; set; } = "";
    }

    private class NotMappedBase
    {
        public int Id { get; set; }
        public object Code { get; set; } = "";
    }

    private sealed class NotMappedDerived : NotMappedBase
    {
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public new string Code { get; set; } = "";
    }

    private class ReadWriteBase
    {
        public int Id { get; set; }
        public object Code { get; set; } = "";
    }

    private sealed class ReadOnlyDerived : ReadWriteBase
    {
        public new string Code => "";
    }

    private class MappedBase
    {
        public int Id { get; set; }
        public object Code { get; set; } = "";
    }

    private sealed class MappedDerived : MappedBase
    {
        public new string Code { get; set; } = "";
    }

    private static PropertyInfo? CodeOf(Type type) =>
        MappedPropertyFilter.GetMappedProperties(type).SingleOrDefault(p => p.Name == "Code");

    [Fact]
    public void AnIgnoredShadowSuppressesTheWholeProperty() =>
        Assert.Null(CodeOf(typeof(IgnoreDerived)));

    [Fact]
    public void ANotMappedShadowSuppressesTheWholeProperty() =>
        Assert.Null(CodeOf(typeof(NotMappedDerived)));

    [Fact]
    public void AReadOnlyShadowSuppressesTheWholeProperty() =>
        Assert.Null(CodeOf(typeof(ReadOnlyDerived)));

    /// <summary>
    /// The other half, and the reason the order matters rather than the filter: when the shadow is
    /// mapped, it is the shadow that must win - AUD-R26's rule, which must survive this change.
    /// </summary>
    [Fact]
    public void AMappedShadowStillWinsOverItsBase()
    {
        PropertyInfo? code = CodeOf(typeof(MappedDerived));

        Assert.NotNull(code);
        Assert.Equal(typeof(string), code.PropertyType);
        Assert.Equal(typeof(MappedDerived), code.DeclaringType);
    }

    [Fact]
    public void TheRestOfTheEntityIsUnaffected()
    {
        List<PropertyInfo> mapped = MappedPropertyFilter.GetMappedProperties(typeof(IgnoreDerived));

        Assert.Single(mapped);
        Assert.Equal("Id", mapped[0].Name);
    }

    [Fact]
    public void AnUnshadowedIgnoreIsStillHonoured()
    {
        List<PropertyInfo> mapped = MappedPropertyFilter.GetMappedProperties(typeof(IgnoreBase));

        Assert.Equal(new[] { "Id", "Code" }.Order(), mapped.Select(p => p.Name).Order());
    }
}
