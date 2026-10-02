using System.Data;
using System.Reflection;

using Extrode.Jaunty.Attributes;
using Extrode.Jaunty.Configuration;
using Extrode.Jaunty.Import;
using Extrode.Jaunty.Internals;
using Extrode.Jaunty.Tests.Helpers;
using Extrode.Jaunty.TypeHandlers;

using Xunit;

namespace Extrode.Jaunty.Tests.Unit.Configuration;

[Collection("Type Handler Operations")]
public class JauntyConfigConfigureTests : IDisposable
{
    public JauntyConfigConfigureTests() => JauntyConfig.Reset();

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        JauntyConfig.Reset();
        TestInitializer.Initialize();
    }

    private sealed class Money
    {
        public decimal Amount { get; set; }
    }

    private sealed class MoneyHandler : TypeHandler<Money>
    {
        public override Money? Parse(object? value) => value is null ? null : new Money { Amount = Convert.ToDecimal(value) };
        public override object? ToDbValue(Money? value) => value?.Amount;
    }

    private static readonly MoneyHandler SharedHandler = new();

    private static string Prefixed(Type type) => "t_" + type.Name;

    private static string Upper(string name) => name.ToUpperInvariant();

    private static string Schema(Type type) => "app";

    private static ICopyImportWriter? NoCopy(IDbConnection connection, string copyCommand) => null;

    private static void Startup(JauntyConfigBuilder c)
    {
        c.TableNameResolver = Prefixed;
        c.ColumnNameResolver = Upper;
        c.RegisterTypeHandler(SharedHandler);
    }

    [Fact]
    public void Configure_AppliesEverySetting()
    {
        CopyImportFactory factory = (_, _) => null;

        JauntyConfig.Configure(c =>
        {
            c.SchemaNameResolver = Schema;
            c.TableNameResolver = Prefixed;
            c.ColumnNameResolver = Upper;
            c.DefaultEnumStorage = EnumStorage.String;
            c.CopyImportFactory = factory;
            c.RegisterTypeHandler(SharedHandler);
        });

        Assert.True(JauntyConfig.IsConfigured);
        Assert.Equal("app", JauntyConfig.SchemaNameResolver!(typeof(Money)));
        Assert.Equal("t_Money", JauntyConfig.TableNameResolver!(typeof(Money)));
        Assert.Equal("AMOUNT", JauntyConfig.ColumnNameResolver!("Amount"));
        Assert.Equal(EnumStorage.String, JauntyConfig.DefaultEnumStorage);
        Assert.Same(factory, JauntyConfig.CopyImportFactory);
        Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(Money), out ITypeHandler? handler));
        Assert.Equal(12.5m, handler!.ToDbValue(new Money { Amount = 12.5m }));
    }

    [Fact]
    public void IsConfigured_IsFalseUntilConfigureCompletes()
    {
        Assert.False(JauntyConfig.IsConfigured);

        JauntyConfig.Configure(c => Assert.False(JauntyConfig.IsConfigured));

        Assert.True(JauntyConfig.IsConfigured);
    }

    [Fact]
    public void Configure_NullCallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => JauntyConfig.Configure(null!));
        Assert.False(JauntyConfig.IsConfigured);
    }

    [Fact]
    public void SecondCall_WithTheSameSettings_IsANoOp()
    {
        JauntyConfig.Configure(Startup);
        int generation = ConfigurationGeneration.Current;

        JauntyConfig.Configure(Startup);

        Assert.Equal(generation, ConfigurationGeneration.Current);
        Assert.Equal("t_Money", JauntyConfig.TableNameResolver!(typeof(Money)));
    }

    [Fact]
    public void SecondCall_WithDifferentSettings_ThrowsAndKeepsTheFirst()
    {
        JauntyConfig.Configure(Startup);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            JauntyConfig.Configure(c => c.TableNameResolver = t => "other_" + t.Name));

        Assert.Contains("different settings", ex.Message);
        Assert.Contains("IsConfigured", ex.Message);
        Assert.Equal("t_Money", JauntyConfig.TableNameResolver!(typeof(Money)));
    }

    public static TheoryData<string> Differences() => new()
    {
        "schema", "enum", "copy", "handler-instance", "extra-handler", "column",
    };

    [Theory]
    [MemberData(nameof(Differences))]
    public void SecondCall_DifferingInOneSetting_Throws(string difference)
    {
        JauntyConfig.Configure(Startup);

        Assert.Throws<InvalidOperationException>(() => JauntyConfig.Configure(c =>
        {
            Startup(c);
            switch (difference)
            {
                case "schema": c.SchemaNameResolver = Schema; break;
                case "enum": c.DefaultEnumStorage = EnumStorage.String; break;
                case "copy": c.CopyImportFactory = NoCopy; break;
                case "handler-instance": c.RegisterTypeHandler(new MoneyHandler()); break;
                case "extra-handler": c.RegisterTypeHandler<Guid>(v => Guid.Empty, v => v); break;
                case "column": c.ColumnNameResolver = null; break;
            }
        }));
    }

    [Fact]
    public void ConcurrentCalls_ExactlyOneWins()
    {
        const int threads = 8;
        using var start = new Barrier(threads);
        int wins = 0;
        int refusals = 0;
        string?[] names = new string?[threads];

        Thread[] workers = Enumerable.Range(0, threads).Select(i => new Thread(() =>
        {
            string name = "winner_" + i;
            start.SignalAndWait();
            try
            {
                JauntyConfig.Configure(c => c.TableNameResolver = _ => name);
                names[i] = name;
                Interlocked.Increment(ref wins);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref refusals);
            }
        })).ToArray();

        foreach (Thread t in workers) t.Start();
        foreach (Thread t in workers) t.Join();

        Assert.Equal(1, wins);
        Assert.Equal(threads - 1, refusals);
        Assert.Equal(names.Single(n => n is not null), JauntyConfig.TableNameResolver!(typeof(Money)));
    }

    public static TheoryData<string> FirstReads() => new()
    {
        "table-resolver", "enum-storage", "has-handlers", "try-get-handler", "generation", "copy-factory",
    };

    [Theory]
    [MemberData(nameof(FirstReads))]
    public void Configure_AfterAnyRead_Throws(string read)
    {
        switch (read)
        {
            case "table-resolver": _ = JauntyConfig.TableNameResolver; break;
            case "enum-storage": _ = JauntyConfig.DefaultEnumStorage; break;
            case "has-handlers": _ = TypeHandlerRegistry.HasHandlers; break;
            case "try-get-handler": TypeHandlerRegistry.TryGetHandler(typeof(Money), out _); break;
            case "generation": _ = ConfigurationGeneration.Current; break;
            case "copy-factory": _ = JauntyConfig.CopyImportFactory; break;
        }

        var ex = Assert.Throws<InvalidOperationException>(() => JauntyConfig.Configure(Startup));

        Assert.Contains("already read its settings", ex.Message);
        Assert.False(JauntyConfig.IsConfigured);
        Assert.Null(JauntyConfig.TableNameResolver);
    }

    [Fact]
    public void Reset_AllowsConfigureAgain()
    {
        JauntyConfig.Configure(Startup);
        _ = JauntyConfig.TableNameResolver;

        JauntyConfig.Reset();
        JauntyConfig.Configure(c => c.TableNameResolver = t => "again_" + t.Name);

        Assert.True(JauntyConfig.IsConfigured);
        Assert.Equal("again_Money", JauntyConfig.TableNameResolver!(typeof(Money)));
        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Money), out _));
    }

    [Fact]
    public void Reset_ClearsTheSettingsAndTheReadRecord()
    {
        JauntyConfig.Configure(Startup);
        _ = JauntyConfig.TableNameResolver;

        JauntyConfig.Reset();

        Assert.False(JauntyConfig.IsConfigured);
        Assert.False(ConfigurationGeneration.HasBeenRead);
        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Money), out _));
    }

    [Fact]
    public void Configure_SwitchesOnReflectionMappingBeforeTheCallbackRuns()
    {
        Func<Type, object>? seenInCallback = null;

        JauntyConfig.Configure(c => seenInCallback = c.ReflectionTableMetadataResolver);

        Assert.NotNull(seenInCallback);
        Assert.Equal("JauntyReflectionExtensions", JauntyConfig.ReflectionTableMetadataResolver!.Method.DeclaringType!.Name);
        Assert.NotNull(JauntyConfig.ReflectionMapperResolver);
        Assert.NotNull(JauntyConfig.SpecialTypeMapperResolver);
    }

    [Fact]
    public void Configure_TheCallbackWinsOverAutomaticReflectionMapping()
    {
        Func<Type, object> mine = _ => null!;

        JauntyConfig.Configure(c =>
        {
            c.ReflectionTableMetadataResolver = mine;
            c.SpecialTypeMapperResolver = null;
        });

        Assert.Same(mine, JauntyConfig.ReflectionTableMetadataResolver);
        Assert.Null(JauntyConfig.SpecialTypeMapperResolver);
    }

    [Fact]
    public void StaticConstructorPath_DoesNothingOnceConfigured()
    {
        JauntyConfig.Configure(c => c.ReflectionTableMetadataResolver = null);

        Exception? failure = Jaunty.TryEnableReflectionMapping(Assembly.Load);

        Assert.Null(failure);
        Assert.Null(JauntyConfig.ReflectionTableMetadataResolver);
        Assert.True(JauntyConfig.IsConfigured);
    }

    [Fact]
    public void StaticConstructorPath_StillEnablesReflection_WhenADerivedCacheWasReadFirst()
    {
        _ = ConfigurationGeneration.Current;
        Assert.True(ConfigurationGeneration.HasBeenRead);

        Jaunty.TryEnableReflectionMapping();

        Assert.NotNull(JauntyConfig.ReflectionTableMetadataResolver);
        Assert.NotNull(JauntyConfig.ReflectionInsertBinderResolver);
        Assert.NotNull(JauntyConfig.SpecialTypeMapperResolver);
        Assert.False(JauntyConfig.IsConfigured);
        Assert.Throws<InvalidOperationException>(() => JauntyConfig.Configure(Startup));
    }

    [Fact]
    public void StaticConstructorPath_KeepsHooksAlreadySet()
    {
        Func<Type, object> mine = _ => null!;
        JauntyConfig.Reconfigure(c => c.ReflectionTableMetadataResolver = mine);

        Jaunty.TryEnableReflectionMapping(Assembly.Load);

        Assert.Same(mine, JauntyConfig.ReflectionTableMetadataResolver);
        Assert.NotNull(JauntyConfig.ReflectionMapperResolver);
    }

    [Fact]
    public void Configure_BumpsTheGenerationOnce()
    {
        int before = ConfigurationGeneration.Current;
        ConfigurationGeneration.ClearRead();

        JauntyConfig.Configure(c =>
        {
            Startup(c);
            c.RegisterTypeHandler<Guid>(v => Guid.Empty, v => v);
        });

        Assert.Equal(before + 1, ConfigurationGeneration.Current);
    }

    [Fact]
    public void Configure_FromInsideItsOwnCallback_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JauntyConfig.Configure(_ => JauntyConfig.Configure(Startup)));

        Assert.Contains("inside its own callback", ex.Message);
        Assert.False(JauntyConfig.IsConfigured);
        JauntyConfig.Configure(Startup);
        Assert.True(JauntyConfig.IsConfigured);
    }

    [Fact]
    public void Configure_WhenTheCallbackThrows_LeavesItUnconfigured()
    {
        var boom = new InvalidOperationException("boom");

        Assert.Same(boom, Assert.Throws<InvalidOperationException>(() =>
            JauntyConfig.Configure(c =>
            {
                c.TableNameResolver = Prefixed;
                throw boom;
            })));

        Assert.False(JauntyConfig.IsConfigured);
        Assert.Null(JauntyConfig.TableNameResolver);
        ConfigurationGeneration.ClearRead();
        JauntyConfig.Configure(Startup);
        Assert.True(JauntyConfig.IsConfigured);
    }

    [Fact]
    public void Configure_ReplacesHandlersRegisteredBefore()
    {
        JauntyConfig.Reconfigure(c => c.RegisterTypeHandler<Guid>(v => Guid.Empty, v => v));
        ConfigurationGeneration.ClearRead();

        JauntyConfig.Configure(Startup);

        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Guid), out _));
        Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(Money), out _));
    }

    [Fact]
    public void Reconfigure_KeepsTheRestAndRegistersOnlyTheNewHandler()
    {
        JauntyConfig.Configure(Startup);
        TypeHandlerRegistry.Remove<Money>();

        JauntyConfig.Reconfigure(c =>
        {
            c.DefaultEnumStorage = EnumStorage.String;
            c.RegisterTypeHandler<Guid>(v => Guid.Empty, v => v);
        });

        Assert.Equal("t_Money", JauntyConfig.TableNameResolver!(typeof(Money)));
        Assert.Equal(EnumStorage.String, JauntyConfig.DefaultEnumStorage);
        Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(Guid), out _));
        Assert.False(TypeHandlerRegistry.TryGetHandler(typeof(Money), out _));
    }

    [Fact]
    public void Builder_RegisterTypeHandler_RejectsNulls()
    {
        JauntyConfig.Configure(c =>
        {
            Assert.Throws<ArgumentNullException>(() => c.RegisterTypeHandler<Money>(null!));
            Assert.Throws<ArgumentNullException>(() => c.RegisterTypeHandler<Money>(null!, m => m));
            Assert.Throws<ArgumentNullException>(() => c.RegisterTypeHandler<Money>(v => null!, null!));
        });
    }

    [Fact]
    public void Builder_NullableHandler_IsKeyedOnTheUnderlyingType()
    {
        JauntyConfig.Configure(c => c.RegisterTypeHandler<int?>(v => 7, v => v));

        Assert.True(TypeHandlerRegistry.TryGetHandler(typeof(int), out ITypeHandler? handler));
        Assert.Equal(7, handler!.Parse("x"));
    }
}
