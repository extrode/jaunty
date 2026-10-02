using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Core;
using Extrode.Jaunty.TypeHandlers;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit;

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
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<HandledToken>(
            fromDb: v => new HandledToken((string)v!),
            toDb: v => v.Text));
        try
        {
            Assert.Equal("tok", GeneratedBindingSupport.ToDbValue(new HandledToken("tok")));
        }
        finally
        {
            TypeHandlerRegistry.Remove<HandledToken>();
        }
    }

    [Fact]
    public void ToDbEnumValue_ExplicitString_WritesTheName()
    {
        Assert.Equal("Busy", GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.String));
    }

    [Fact]
    public void ToDbEnumValue_ExplicitNumeric_WritesTheUnderlyingInteger()
    {
        object? bound = GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.Numeric);

        Assert.IsType<int>(bound);
        Assert.Equal(1, bound);
    }

    [Fact]
    public void ToDbEnumValue_ByteBackedEnum_WritesAByte()
    {
        object? bound = GeneratedBindingSupport.ToDbEnumValue(Narrow.Two, EnumStorage.Numeric);

        Assert.IsType<byte>(bound);
        Assert.Equal((byte)2, bound);
    }

    private enum Narrow : byte
    {
        One = 1,
        Two = 2
    }

    [Fact]
    public void ToDbEnumValue_NullStorage_ReadsTheConfigDefaultPerCall()
    {
        EnumStorage original = JauntyConfig.DefaultEnumStorage;
        try
        {
            JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = EnumStorage.String);
            Assert.Equal("Idle", GeneratedBindingSupport.ToDbEnumValue(SupportState.Idle, null));

            JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = EnumStorage.Numeric);
            Assert.Equal(0, GeneratedBindingSupport.ToDbEnumValue(SupportState.Idle, null));
        }
        finally
        {
            JauntyConfig.Reconfigure(jc => jc.DefaultEnumStorage = original);
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
        JauntyConfig.Reconfigure(jc => jc.RegisterTypeHandler<SupportState>(
            fromDb: v => (SupportState)Convert.ToInt32(v),
            toDb: v => (int)v * 100));
        try
        {
            Assert.Equal(100, GeneratedBindingSupport.ToDbEnumValue(SupportState.Busy, EnumStorage.String));
        }
        finally
        {
            TypeHandlerRegistry.Remove<SupportState>();
        }
    }
}

[Collection("Type Handler Operations")]
public class GeneratedBindingSupportHandlerConversionTests : IDisposable
{
    private sealed class IntReturningHandler : ITypeHandler
    {
        public object? Parse(object? dbValue) => 7;

        public object? ToDbValue(object? value) => value;
    }

    public GeneratedBindingSupportHandlerConversionTests()
        => TypeHandlerRegistry.Register<long>(new IntReturningHandler());

    public void Dispose()
    {
        TypeHandlerRegistry.Remove<long>();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void FromDbValue_ConvertsAHandlersCompatibleResult()
    {
        Assert.Equal(7L, GeneratedBindingSupport.FromDbValue<long>("x"));
    }

    [Fact]
    public void FromDbValueNullable_ConvertsAHandlersCompatibleResult()
    {
        Assert.Equal(7L, GeneratedBindingSupport.FromDbValueNullable<long>("x"));
    }
}
