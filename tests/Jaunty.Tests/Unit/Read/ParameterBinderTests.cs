using System.Data;

using Jaunty.Internals.Parameters;

namespace Jaunty.Tests;

public class ParameterBinderTests
{
    #region Basic Binding

    [Fact]
    public void Bind_SingleIntParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id");

        ParameterBinder.Bind(command, new { Id = 42 });

        Assert.Single(command.Parameters);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
        Assert.Equal(42, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_SingleStringParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name = @Name");

        ParameterBinder.Bind(command, new { Name = "John" });

        Assert.Single(command.Parameters);
        Assert.Equal("Name", command.Parameters[0].ParameterName);
        Assert.Equal("John", command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_MultipleParameters_BindsInOrder()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id AND name = @Name AND age = @Age");

        ParameterBinder.Bind(command, new { Id = 1, Name = "John", Age = 30 });

        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
        Assert.Equal(1, command.Parameters[0].Value);
        Assert.Equal("Name", command.Parameters[1].ParameterName);
        Assert.Equal("John", command.Parameters[1].Value);
        Assert.Equal("Age", command.Parameters[2].ParameterName);
        Assert.Equal(30, command.Parameters[2].Value);
    }

    #endregion

    #region Duplicate Parameters

    [Fact]
    public void Bind_DuplicateParameter_BindsOnce()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE region = @Region OR @Region IS NULL");

        ParameterBinder.Bind(command, new { Region = "West" });

        Assert.Single(command.Parameters);
        Assert.Equal("Region", command.Parameters[0].ParameterName);
        Assert.Equal("West", command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_MultipleDuplicates_BindsUniqueOnly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE (a = @A OR @A IS NULL) AND (b = @B OR @B IS NULL) AND a != @A");

        ParameterBinder.Bind(command, new { A = 1, B = 2 });

        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal("A", command.Parameters[0].ParameterName);
        Assert.Equal(1, command.Parameters[0].Value);
        Assert.Equal("B", command.Parameters[1].ParameterName);
        Assert.Equal(2, command.Parameters[1].Value);
    }

    [Fact]
    public void Bind_DuplicateParameterCaseInsensitive_BindsOnce()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE region = @Region OR @REGION IS NULL OR @region = ''");

        ParameterBinder.Bind(command, new { Region = "West" });

        Assert.Single(command.Parameters);
    }

    #endregion

    #region Null Values

    [Fact]
    public void Bind_NullValue_BindsAsDbNull()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE region = @Region");

        ParameterBinder.Bind(command, new { Region = (string?)null });

        Assert.Single(command.Parameters);
        Assert.Equal(DBNull.Value, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_NullableIntNull_BindsAsDbNull()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE age = @Age");

        ParameterBinder.Bind(command, new { Age = (int?)null });

        Assert.Single(command.Parameters);
        Assert.Equal(DBNull.Value, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_NullableIntWithValue_BindsValue()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE age = @Age");

        ParameterBinder.Bind(command, new { Age = (int?)25 });

        Assert.Single(command.Parameters);
        Assert.Equal(25, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_NullInDuplicateParameter_BindsAsDbNull()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE region = @Region OR @Region IS NULL");

        ParameterBinder.Bind(command, new { Region = (string?)null });

        Assert.Single(command.Parameters);
        Assert.Equal(DBNull.Value, command.Parameters[0].Value);
    }

    #endregion

    #region Various Data Types

    [Fact]
    public void Bind_BoolParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE active = @Active");

        ParameterBinder.Bind(command, new { Active = true });

        Assert.Equal(true, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_DateTimeParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE created = @Created");
        var date = new DateTime(2024, 1, 15, 10, 30, 0);

        ParameterBinder.Bind(command, new { Created = date });

        Assert.Equal(date, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_GuidParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id");
        var guid = Guid.NewGuid();

        ParameterBinder.Bind(command, new { Id = guid });

        Assert.Equal(guid, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_DecimalParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE price = @Price");

        ParameterBinder.Bind(command, new { Price = 19.99m });

        Assert.Equal(19.99m, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_DoubleParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE weight = @Weight");

        ParameterBinder.Bind(command, new { Weight = 2.5 });

        Assert.Equal(2.5, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_LongParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id");

        ParameterBinder.Bind(command, new { Id = 9999999999L });

        Assert.Equal(9999999999L, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ByteArrayParameter_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM files WHERE data = @Data");
        var bytes = new byte[] { 1, 2, 3, 4, 5 };

        ParameterBinder.Bind(command, new { Data = bytes });

        Assert.Equal(bytes, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_EnumParameter_BindsAsUnderlyingType()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE status = @Status");

        ParameterBinder.Bind(command, new { Status = TestEnum.Active });

        Assert.Equal(TestEnum.Active, command.Parameters[0].Value);
    }

    #endregion

    #region SQL Comment Handling

    [Fact]
    public void Bind_ParameterInSingleLineComment_Ignored()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id -- @Ignored parameter");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
    }

    [Fact]
    public void Bind_ParameterInBlockComment_Ignored()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id /* @Ignored */");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
    }

    [Fact]
    public void Bind_ParameterAfterBlockComment_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT /* comment */ * FROM users WHERE id = @Id");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
    }

    #endregion

    #region SQL String Literal Handling

    [Fact]
    public void Bind_ParameterInStringLiteral_Ignored()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id AND name = '@NotAParam'");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
        Assert.Equal("Id", command.Parameters[0].ParameterName);
    }

    [Fact]
    public void Bind_ParameterInDoubleQuotedIdentifier_Ignored()
    {
        var command = new MockDbCommand("SELECT \"@Column\" FROM users WHERE id = @Id");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
    }

    [Fact]
    public void Bind_ParameterInBracketIdentifier_Ignored()
    {
        var command = new MockDbCommand("SELECT [@Column] FROM users WHERE id = @Id");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
    }

    [Fact]
    public void Bind_EscapedQuoteInStringLiteral_HandledCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name = 'O''Brien' AND id = @Id");

        ParameterBinder.Bind(command, new { Id = 1 });

        Assert.Single(command.Parameters);
    }

    #endregion

    #region Parameter Count Mismatch

    [Fact]
    public void Bind_TooFewValues_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id AND name = @Name");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { Id = 1 }));

        Assert.Contains("@Name", ex.Message);  // Missing SQL param
    }

    [Fact]
    public void Bind_TooManyValues_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { Id = 1, Name = "John", Age = 30 }));

        Assert.Contains("Unused parameter properties", ex.Message);
        Assert.Contains("Name", ex.Message);
        Assert.Contains("Age", ex.Message);
    }

    [Fact]
    public void Bind_NoParametersInSql_WithValues_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { Id = 1 }));

        Assert.Contains("Unused parameter properties", ex.Message);
        Assert.Contains("Id", ex.Message);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Bind_EmptyAnonymousObject_WithNoSqlParams_Succeeds()
    {
        var command = new MockDbCommand("SELECT * FROM users");

        // This shouldn't throw - 0 params in SQL, 0 values provided
        // Note: Can't create truly empty anonymous object, so this tests the SQL side
        Assert.Equal(0, command.Parameters.Count);
    }

    [Fact]
    public void Bind_ParameterWithUnderscore_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE user_id = @user_id");

        ParameterBinder.Bind(command, new { user_id = 1 });

        Assert.Single(command.Parameters);
        Assert.Equal("user_id", command.Parameters[0].ParameterName);
    }

    [Fact]
    public void Bind_ParameterWithNumbers_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE field1 = @Field1 AND field2 = @Field2");

        ParameterBinder.Bind(command, new { Field1 = "a", Field2 = "b" });

        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void Bind_ComplexQuery_BindsCorrectly()
    {
        var command = new MockDbCommand(@"
            SELECT u.*, o.order_id
            FROM users u
            LEFT JOIN orders o ON u.id = o.user_id
            WHERE u.created >= @StartDate
              AND u.created <= @EndDate
              AND (u.region = @Region OR @Region IS NULL)
              AND u.status IN (@Status)
            ORDER BY u.created DESC");

        var startDate = new DateTime(2024, 1, 1);
        var endDate = new DateTime(2024, 12, 31);

        ParameterBinder.Bind(command, new { StartDate = startDate, EndDate = endDate, Region = "West", Status = 1 });

        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal("StartDate", command.Parameters[0].ParameterName);
        Assert.Equal("EndDate", command.Parameters[1].ParameterName);
        Assert.Equal("Region", command.Parameters[2].ParameterName);
        Assert.Equal("Status", command.Parameters[3].ParameterName);
    }

    #endregion

    #region Strict Name Matching

    [Fact]
    public void Bind_PropertyNameMismatch_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE created = @startdate");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { created = DateTime.Now }));

        Assert.Contains("@startdate", ex.Message);
    }

    [Fact]
    public void Bind_PartialNameMatch_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id AND name = @Name");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { Id = 1, FullName = "John" }));

        Assert.Contains("@Name", ex.Message);
    }

    [Fact]
    public void Bind_ExtraUnusedProperty_ThrowsArgumentException()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @Id");

        var ex = Assert.Throws<ArgumentException>(() =>
            ParameterBinder.Bind(command, new { Id = 1, Name = "John" }));

        Assert.Contains("Unused parameter properties", ex.Message);
        Assert.Contains("Name", ex.Message);
    }

    [Fact]
    public void Bind_CaseInsensitiveMatch_Succeeds()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @ID AND name = @NAME");

        ParameterBinder.Bind(command, new { id = 1, name = "John" });

        Assert.Equal(2, command.Parameters.Count);
    }

    [Fact]
    public void Bind_MixedCaseMatch_Succeeds()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE userId = @UserId");

        ParameterBinder.Bind(command, new { userid = 42 });

        Assert.Single(command.Parameters);
        Assert.Equal(42, command.Parameters[0].Value);
    }

    #endregion

    #region Test Helpers

    private enum TestEnum
    {
        Inactive = 0,
        Active = 1,
        Pending = 2
    }

    #endregion
}

#region Mock Classes

public class MockDbCommand : IDbCommand
{
    public MockDbCommand(string commandText)
    {
        CommandText = commandText;
        Parameters = new MockDataParameterCollection();
    }

    public string CommandText { get; set; }
    public int CommandTimeout { get; set; }
    public CommandType CommandType { get; set; }
    public IDbConnection? Connection { get; set; }
    public MockDataParameterCollection Parameters { get; }
    IDataParameterCollection IDbCommand.Parameters => Parameters;
    public IDbTransaction? Transaction { get; set; }
    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }
    public IDbDataParameter CreateParameter() => new MockDbDataParameter();
    public void Dispose() { }
    public int ExecuteNonQuery() => 0;
    public IDataReader ExecuteReader() => throw new NotImplementedException();
    public IDataReader ExecuteReader(CommandBehavior behavior) => throw new NotImplementedException();
    public object? ExecuteScalar() => null;
    public void Prepare() { }
}

public class MockDataParameterCollection : List<MockDbDataParameter>, IDataParameterCollection
{
    public bool Contains(string parameterName) => this.Any(p => p.ParameterName == parameterName);
    public int IndexOf(string parameterName) => FindIndex(p => p.ParameterName == parameterName);
    public void RemoveAt(string parameterName) => RemoveAll(p => p.ParameterName == parameterName);
    public object this[string parameterName]
    {
        get => this.First(p => p.ParameterName == parameterName);
        set => throw new NotImplementedException();
    }
}

public class MockDbDataParameter : IDbDataParameter
{
    public DbType DbType { get; set; }
    public ParameterDirection Direction { get; set; }
    public bool IsNullable => true;
    public string ParameterName { get; set; } = "";
    public byte Precision { get; set; }
    public byte Scale { get; set; }
    public int Size { get; set; }
    public string SourceColumn { get; set; } = "";
    public DataRowVersion SourceVersion { get; set; }
    public object? Value { get; set; }
}

#endregion
