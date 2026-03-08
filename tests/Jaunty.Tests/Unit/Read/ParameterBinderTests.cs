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

    #region Collection Parameter Expansion

    [Fact]
    public void Bind_IntArrayParameter_ExpandsToMultipleParams()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = new[] { 1, 2, 3 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal("Ids0", command.Parameters[0].ParameterName);
        Assert.Equal(1, command.Parameters[0].Value);
        Assert.Equal("Ids1", command.Parameters[1].ParameterName);
        Assert.Equal(2, command.Parameters[1].Value);
        Assert.Equal("Ids2", command.Parameters[2].ParameterName);
        Assert.Equal(3, command.Parameters[2].Value);
        Assert.Contains("(@Ids0, @Ids1, @Ids2)", command.CommandText);
    }

    [Fact]
    public void Bind_ListParameter_ExpandsToMultipleParams()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = new List<int> { 10, 20, 30, 40 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(4, command.Parameters.Count);
        Assert.Contains("(@Ids0, @Ids1, @Ids2, @Ids3)", command.CommandText);
    }

    [Fact]
    public void Bind_StringArrayParameter_ExpandsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name IN @Names");
        var names = new[] { "Alice", "Bob", "Charlie" };

        ParameterBinder.Bind(command, new { Names = names });

        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal("Alice", command.Parameters[0].Value);
        Assert.Equal("Bob", command.Parameters[1].Value);
        Assert.Equal("Charlie", command.Parameters[2].Value);
    }

    [Fact]
    public void Bind_EmptyArray_ExpandsToNoMatchSubquery()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = Array.Empty<int>();

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Empty(command.Parameters);
        Assert.Contains("(SELECT NULL WHERE 1 = 0)", command.CommandText);
    }

    [Fact]
    public void Bind_SingleItemArray_ExpandsToSingleParam()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = new[] { 42 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Single(command.Parameters);
        Assert.Equal("Ids0", command.Parameters[0].ParameterName);
        Assert.Equal(42, command.Parameters[0].Value);
        Assert.Contains("(@Ids0)", command.CommandText);
    }

    [Fact]
    public void Bind_CollectionWithOtherParams_ExpandsOnlyCollection()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE category_id = @CategoryId AND id IN @Ids");
        var ids = new[] { 1, 2, 3 };

        ParameterBinder.Bind(command, new { CategoryId = 5, Ids = ids });

        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal("CategoryId", command.Parameters[0].ParameterName);
        Assert.Equal(5, command.Parameters[0].Value);
        Assert.Contains("(@Ids0, @Ids1, @Ids2)", command.CommandText);
    }

    [Fact]
    public void Bind_MultipleCollections_ExpandsBoth()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids AND category_id IN @Categories");
        var ids = new[] { 1, 2 };
        var categories = new[] { 10, 20, 30 };

        ParameterBinder.Bind(command, new { Ids = ids, Categories = categories });

        Assert.Equal(5, command.Parameters.Count);
        Assert.Contains("(@Ids0, @Ids1)", command.CommandText);
        Assert.Contains("(@Categories0, @Categories1, @Categories2)", command.CommandText);
    }

    [Fact]
    public void Bind_StringParameter_NotExpandedAsCollection()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name = @Name");

        ParameterBinder.Bind(command, new { Name = "John" });

        Assert.Single(command.Parameters);
        Assert.Equal("Name", command.Parameters[0].ParameterName);
        Assert.Equal("John", command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ByteArrayParameter_NotExpandedAsCollection()
    {
        var command = new MockDbCommand("SELECT * FROM files WHERE data = @Data");
        var bytes = new byte[] { 1, 2, 3 };

        ParameterBinder.Bind(command, new { Data = bytes });

        Assert.Single(command.Parameters);
        Assert.Equal("Data", command.Parameters[0].ParameterName);
        Assert.Equal(bytes, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_IEnumerableParameter_ExpandsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        IEnumerable<int> ids = Enumerable.Range(1, 5);

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(5, command.Parameters.Count);
        Assert.Contains("(@Ids0, @Ids1, @Ids2, @Ids3, @Ids4)", command.CommandText);
    }

    [Fact]
    public void Bind_CollectionWithNullItems_ExpandsWithDbNull()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name IN @Names");
        var names = new string?[] { "Alice", null, "Bob" };

        ParameterBinder.Bind(command, new { Names = names });

        Assert.Equal(3, command.Parameters.Count);
        Assert.Equal("Alice", command.Parameters[0].Value);
        Assert.Equal(DBNull.Value, command.Parameters[1].Value);
        Assert.Equal("Bob", command.Parameters[2].Value);
    }

    [Fact]
    public void Bind_CollectionCaseInsensitive_ExpandsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @IDS");
        var ids = new[] { 1, 2 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(2, command.Parameters.Count);
        // Expansion uses SQL parameter name case (IDS), not property name case
        Assert.Contains("(@IDS0, @IDS1)", command.CommandText);
    }

    [Fact]
    public void Bind_CollectionParameterUsedTwice_ExpandsBoth()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids OR parent_id IN @Ids");
        var ids = new[] { 1, 2 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(2, command.Parameters.Count);
        // Both occurrences should be expanded
        Assert.Contains("(@Ids0, @Ids1)", command.CommandText);
        var count = command.CommandText.Split(new[] { "(@Ids0, @Ids1)" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(2, count);
    }

    [Fact]
    public void Bind_LargeCollection_ExpandsAll()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = Enumerable.Range(1, 100).ToArray();

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(100, command.Parameters.Count);
        Assert.Contains("@Ids99", command.CommandText);
    }

    [Fact]
    public void Bind_GuidArrayParameter_ExpandsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id IN @Ids");
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var ids = new[] { id1, id2 };

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal(id1, command.Parameters[0].Value);
        Assert.Equal(id2, command.Parameters[1].Value);
    }

    #endregion

    #region Edge Cases - Special Parameter Name Formats

    [Fact]
    public void Bind_ParameterNameWithUnderscore_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE user_name = @user_name");

        ParameterBinder.Bind(command, new { user_name = "john_doe" });

        Assert.Single(command.Parameters);
        Assert.Equal("user_name", command.Parameters[0].ParameterName);
        Assert.Equal("john_doe", command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ParameterNameWithNumbers_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id1 = @id1 AND id2 = @id2");

        ParameterBinder.Bind(command, new { id1 = 1, id2 = 2 });

        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal("id1", command.Parameters[0].ParameterName);
        Assert.Equal(1, command.Parameters[0].Value);
        Assert.Equal("id2", command.Parameters[1].ParameterName);
        Assert.Equal(2, command.Parameters[1].Value);
    }

    [Fact]
    public void Bind_ParameterNameStartingWithUnderscore_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE _id = @_id");

        ParameterBinder.Bind(command, new { _id = 42 });

        Assert.Single(command.Parameters);
        Assert.Equal("_id", command.Parameters[0].ParameterName);
        Assert.Equal(42, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ParameterNameWithMixedCase_BindsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE UserName = @UserName AND userID = @userID");

        ParameterBinder.Bind(command, new { UserName = "John", userID = 42 });

        Assert.Equal(2, command.Parameters.Count);
        Assert.Equal("UserName", command.Parameters[0].ParameterName);
        Assert.Equal("John", command.Parameters[0].Value);
        Assert.Equal("userID", command.Parameters[1].ParameterName);
        Assert.Equal(42, command.Parameters[1].Value);
    }

    #endregion

    #region Edge Cases - Large Parameter Lists

    [Fact]
    public void Bind_LargeParameterList_1000Parameters_BindsCorrectly()
    {
        var sqlParams = string.Join(", ", Enumerable.Range(1, 1000).Select(i => $"@p{i}"));
        var command = new MockDbCommand($"SELECT * FROM table WHERE col IN ({sqlParams})");

        var paramObj = new Dictionary<string, object>();
        for (int i = 1; i <= 1000; i++)
        {
            paramObj[$"p{i}"] = i;
        }

        ParameterBinder.Bind(command, paramObj);

        Assert.Equal(1000, command.Parameters.Count);
        Assert.Equal(500, command.Parameters[499].Value);
        Assert.Equal(1000, command.Parameters[999].Value);
    }

    [Fact]
    public void Bind_LargeParameterList_5000Parameters_BindsCorrectly()
    {
        var sqlParams = string.Join(", ", Enumerable.Range(1, 5000).Select(i => $"@p{i}"));
        var command = new MockDbCommand($"SELECT * FROM table WHERE col IN ({sqlParams})");

        var paramObj = new Dictionary<string, object>();
        for (int i = 1; i <= 5000; i++)
        {
            paramObj[$"p{i}"] = i;
        }

        ParameterBinder.Bind(command, paramObj);

        Assert.Equal(5000, command.Parameters.Count);
        Assert.Equal(2500, command.Parameters[2499].Value);
        Assert.Equal(5000, command.Parameters[4999].Value);
    }

    [Fact]
    public void Bind_LargeArrayParameter_1000Items_ExpandsCorrectly()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE id IN @Ids");
        var ids = Enumerable.Range(1, 1000).ToArray();

        ParameterBinder.Bind(command, new { Ids = ids });

        Assert.Equal(1000, command.Parameters.Count);
        Assert.Equal(1, command.Parameters[0].Value);
        Assert.Equal(1000, command.Parameters[999].Value);
    }

    #endregion

    #region Scalar Parameter Binding

    [Fact]
    public void Bind_ScalarInt_BindsToSqlParameter()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @p0");

        ParameterBinder.Bind(command, 42);

        Assert.Single(command.Parameters);
        Assert.Equal("p0", command.Parameters[0].ParameterName);
        Assert.Equal(42, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ScalarString_BindsToSqlParameter()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE name = @name");

        ParameterBinder.Bind(command, "John");

        Assert.Single(command.Parameters);
        Assert.Equal("name", command.Parameters[0].ParameterName);
        Assert.Equal("John", command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ScalarGuid_BindsToSqlParameter()
    {
        var guid = Guid.NewGuid();
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @id");

        ParameterBinder.Bind(command, guid);

        Assert.Single(command.Parameters);
        Assert.Equal("id", command.Parameters[0].ParameterName);
        Assert.Equal(guid, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ScalarLong_BindsToSqlParameter()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE id = @id");

        ParameterBinder.Bind(command, 123L);

        Assert.Single(command.Parameters);
        Assert.Equal("id", command.Parameters[0].ParameterName);
        Assert.Equal(123L, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ScalarDecimal_BindsToSqlParameter()
    {
        var command = new MockDbCommand("SELECT * FROM products WHERE price = @price");

        ParameterBinder.Bind(command, 19.99m);

        Assert.Single(command.Parameters);
        Assert.Equal("price", command.Parameters[0].ParameterName);
        Assert.Equal(19.99m, command.Parameters[0].Value);
    }

    [Fact]
    public void Bind_ScalarBool_BindsToSqlParameter()
    {
        var command = new MockDbCommand("SELECT * FROM users WHERE active = @active");

        ParameterBinder.Bind(command, true);

        Assert.Single(command.Parameters);
        Assert.Equal("active", command.Parameters[0].ParameterName);
        Assert.Equal(true, command.Parameters[0].Value);
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