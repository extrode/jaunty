using Jaunty.Attributes;
using Jaunty.Configuration;
using Jaunty.Core;

using Xunit;

namespace Jaunty.Tests.Unit;

public class GeneratedBindingSupportTests
{
    private enum SupportState
    {
        Idle = 0,
        Busy = 1
    }

    private readonly struct HandledToken(string text)
    {
        public string Text { get; } = text;
    }

    [Fact]
    public void ToDbValue_Null_PassesThrough()
    {
        Assert.Null(GeneratedBindingSupport.ToDbValue(null));
    }

    [Fact]
    public void ToDbValue_NoHandler_PassesThrough()
    {
        Assert.Equal(42, GeneratedBindingSupport.ToDbValue(42));
    }

    [Fact]
    public void ToDbValue_RegisteredHandler_Wins()
    {
        JauntyConfig.RegisterTypeHandler<HandledToken>(
            fromDb: v => new HandledToken((string)v!),
            toDb: v => v.Text);
        try
        {
            Assert.Equal("tok", GeneratedBindingSupport.ToDbValue(new HandledToken("tok")));
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<HandledToken>();
        }
    }

    [Fact]
    public void ToDbEnumValue_ExplicitString_WritesTheName()
    {
        Assert.Equal("Busy", GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.String));
    }

    [Fact]
    public void ToDbEnumValue_ExplicitNumeric_PassesTheEnumThrough()
    {
        Assert.Equal(SupportState.Busy, GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.Numeric));
    }

    [Fact]
    public void ToDbEnumValue_NullStorage_ReadsTheConfigDefaultPerCall()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.DefaultEnumStorage = EnumStorage.String;
            Assert.Equal("Idle", GeneratedBindingSupport.ToDbEnumValue(SupportState.Idle, null));

            JauntyConfig.DefaultEnumStorage = EnumStorage.Numeric;
            Assert.Equal(SupportState.Idle, GeneratedBindingSupport.ToDbEnumValue(SupportState.Idle, null));
        }
        finally
        {
            JauntyConfig.DefaultEnumStorage = original;
        }
    }

    [Fact]
    public void ToDbEnumValue_Null_StaysNull()
    {
        Assert.Null(GeneratedBindingSupport.ToDbEnumValue(null, EnumStorage.String));
    }

    [Fact]
    public void ToDbEnumValue_RegisteredHandler_WinsOverStorage()
    {
        JauntyConfig.RegisterTypeHandler<SupportState>(
            fromDb: v => (SupportState)Convert.ToInt32(v),
            toDb: v => (int)v * 100);
        try
        {
            Assert.Equal(100, GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.String));
        }
        finally
        {
            JauntyConfig.RemoveTypeHandler<SupportState>();
        }
    }
}
