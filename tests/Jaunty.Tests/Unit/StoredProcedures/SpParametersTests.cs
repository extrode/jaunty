using System.Data;

using FluentAssertions;

using Jaunty.StoredProcedure;

namespace Jaunty.Tests.StoredProcedures;

/// <summary>
/// Unit tests for SpParameters fluent API.
/// </summary>
public class SpParametersTests
{
    #region AddInput Tests

    [Fact]
    public void AddInput_Simple_AddsParameter()
    {
        var parameters = new SpParameters();
        
        var result = parameters.AddInput("CategoryId", 1);
        
        Assert.Same(parameters, result);
        Assert.Single(parameters.Parameters);
        Assert.Equal("CategoryId", parameters.Parameters[0].Name);
        Assert.Equal(1, parameters.Parameters[0].Value);
        Assert.Equal(ParameterDirection.Input, parameters.Parameters[0].Direction);
    }

    [Fact]
    public void AddInput_WithDbType_AddsParameterWithType()
    {
        var parameters = new SpParameters();
        
        var result = parameters.AddInput("ProductName", "Widget", DbType.String, size: 100);
        
        Assert.Same(parameters, result);
        Assert.Single(parameters.Parameters);
        Assert.Equal("ProductName", parameters.Parameters[0].Name);
        Assert.Equal("Widget", parameters.Parameters[0].Value);
        Assert.Equal(DbType.String, parameters.Parameters[0].DbType);
        Assert.Equal(100, parameters.Parameters[0].Size);
    }

    [Fact]
    public void AddInput_NullValue_AddsParameterWithNull()
    {
        var parameters = new SpParameters();
        
        parameters.AddInput("NullableParam", null);
        
        Assert.Single(parameters.Parameters);
        Assert.Null(parameters.Parameters[0].Value);
    }

    #endregion

    #region AddOutput Tests

    [Fact]
    public void AddOutput_AddsOutputParameter()
    {
        var parameters = new SpParameters();
        
        var result = parameters.AddOutput("TotalCount", DbType.Int32);
        
        Assert.Same(parameters, result);
        Assert.Single(parameters.Parameters);
        Assert.Equal("TotalCount", parameters.Parameters[0].Name);
        Assert.Equal(DbType.Int32, parameters.Parameters[0].DbType);
        Assert.Equal(ParameterDirection.Output, parameters.Parameters[0].Direction);
    }

    [Fact]
    public void AddOutput_WithSize_AddsParameterWithSize()
    {
        var parameters = new SpParameters();
        
        parameters.AddOutput("ResultMessage", DbType.String, size: 500);
        
        Assert.Single(parameters.Parameters);
        Assert.Equal(DbType.String, parameters.Parameters[0].DbType);
        Assert.Equal(500, parameters.Parameters[0].Size);
    }

    #endregion

    #region AddInputOutput Tests

    [Fact]
    public void AddInputOutput_AddsInOutParameter()
    {
        var parameters = new SpParameters();

        var result = parameters.AddInputOutput("Counter", 0, DbType.Int32);

        Assert.Same(parameters, result);
        Assert.Single(parameters.Parameters);
        Assert.Equal("Counter", parameters.Parameters[0].Name);
        Assert.Equal(0, parameters.Parameters[0].Value);
        Assert.Equal(DbType.Int32, parameters.Parameters[0].DbType);
        Assert.Equal(ParameterDirection.InputOutput, parameters.Parameters[0].Direction);
    }

    #endregion

    #region SpParameter Constructor Tests

    [Fact]
    public void SpParameter_Constructor_WithAllParameters_SetsProperties()
    {
        var param = new SpParameter("TestParam", 42, ParameterDirection.Output, DbType.Int32, 100);

        param.Name.Should().Be("TestParam");
        param.Value.Should().Be(42);
        param.Direction.Should().Be(ParameterDirection.Output);
        param.DbType.Should().Be(DbType.Int32);
        param.Size.Should().Be(100);
    }

    [Fact]
    public void SpParameter_Constructor_WithNullValue_SetsNullValue()
    {
        var param = new SpParameter("NullParam", null, ParameterDirection.InputOutput, DbType.String, 50);

        param.Name.Should().Be("NullParam");
        param.Value.Should().BeNull();
        param.Direction.Should().Be(ParameterDirection.InputOutput);
        param.DbType.Should().Be(DbType.String);
        param.Size.Should().Be(50);
    }

    [Fact]
    public void SpParameter_Constructor_WithoutDbTypeAndSize_SetsNulls()
    {
        var param = new SpParameter("SimpleParam", "value", ParameterDirection.Input, null, null);

        param.Name.Should().Be("SimpleParam");
        param.Value.Should().Be("value");
        param.Direction.Should().Be(ParameterDirection.Input);
        param.DbType.Should().BeNull();
        param.Size.Should().BeNull();
    }

    #endregion

    #region AddReturnValue Tests

    [Fact]
    public void AddReturnValue_AddsReturnValueParameter()
    {
        var parameters = new SpParameters();
        
        var result = parameters.AddReturnValue();
        
        Assert.Same(parameters, result);
        Assert.Single(parameters.Parameters);
        Assert.Equal(ParameterDirection.ReturnValue, parameters.Parameters[0].Direction);
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_Int_ReturnsValue()
    {
        var parameters = new SpParameters()
            .AddOutput("Count", DbType.Int32);
        
        // Simulate what happens after SP execution
        var param = parameters.Parameters[0];
        param.DbParameter = CreateMockParameter(42);
        
        var result = parameters.Get<int>("Count");
        
        Assert.Equal(42, result);
    }

    [Fact]
    public void Get_String_ReturnsValue()
    {
        var parameters = new SpParameters()
            .AddOutput("Message", DbType.String);
        
        var param = parameters.Parameters[0];
        param.DbParameter = CreateMockParameter("Hello World");
        
        var result = parameters.Get<string>("Message");
        
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void Get_NullValue_ReturnsDefault()
    {
        var parameters = new SpParameters()
            .AddOutput("NullableValue", DbType.Int32);
        
        var param = parameters.Parameters[0];
        param.DbParameter = CreateMockParameter(null);
        
        var result = parameters.Get<int?>("NullableValue");
        
        Assert.Null(result);
    }

    [Fact]
    public void Get_NonExistentParameter_Throws()
    {
        var parameters = new SpParameters();
        
        Assert.Throws<ArgumentException>(() => parameters.Get<int>("NonExistent"));
    }

    #endregion

    #region GetReturnValue Tests

    [Fact]
    public void GetReturnValue_ReturnsValue()
    {
        var parameters = new SpParameters()
            .AddReturnValue();
        
        var param = parameters.Parameters[0];
        param.DbParameter = CreateMockParameter(1);
        
        var result = parameters.GetReturnValue();
        
        Assert.Equal(1, result);
    }

    [Fact]
    public void GetReturnValue_NoReturnValue_Throws()
    {
        var parameters = new SpParameters()
            .AddInput("Param", 1);
        
        Assert.Throws<InvalidOperationException>(() => parameters.GetReturnValue());
    }

    #endregion

    #region HasValue Tests

    [Fact]
    public void HasValue_True_WhenValueExists()
    {
        var parameters = new SpParameters()
            .AddOutput("Count", DbType.Int32);
        
        var param = parameters.Parameters[0];
        param.DbParameter = CreateMockParameter(42);
        
        Assert.True(parameters.HasValue("Count"));
    }

    [Fact]
    public void HasValue_False_WhenNoValue()
    {
        var parameters = new SpParameters()
            .AddOutput("Count", DbType.Int32);
        
        Assert.False(parameters.HasValue("Count"));
    }

    [Fact]
    public void HasValue_False_ForNonExistentParameter()
    {
        var parameters = new SpParameters();
        
        Assert.False(parameters.HasValue("NonExistent"));
    }

    #endregion

    #region Parameters Property Tests

    [Fact]
    public void Parameters_ReturnsReadOnlyList()
    {
        var parameters = new SpParameters()
            .AddInput("Param1", 1)
            .AddInput("Param2", 2);
        
        var readOnlyList = parameters.Parameters;
        
        Assert.Equal(2, readOnlyList.Count);
        Assert.Equal("Param1", readOnlyList[0].Name);
        Assert.Equal("Param2", readOnlyList[1].Name);
    }

    #endregion

    private static IDbDataParameter CreateMockParameter(object? value)
    {
        var mock = new MockDbParameter();
        mock.Value = value;
        return mock;
    }

    private class MockDbParameter : IDbDataParameter
    {
        public object? Value { get; set; }
        public ParameterDirection Direction { get; set; }
        public bool IsNullable { get; set; }
        public string ParameterName { get; set; } = "";
        public int Size { get; set; }
        public string SourceColumn { get; set; } = "";
        public DataRowVersion SourceVersion { get; set; }
        public DbType DbType { get; set; }
        public byte Precision { get; set; }
        public byte Scale { get; set; }
        
        public void ResetDbType() { }
    }
}
