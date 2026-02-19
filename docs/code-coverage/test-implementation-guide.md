# Test Implementation Guide

**Purpose**: Guide for agents and developers writing tests for FlatFile Editor  
**Created**: 2026-02-17  
**Last Updated**: 2026-02-17

> **CRITICAL RULES** - Before writing any tests:
> - **Never modify existing tests** without explicit user permission
> - **Never delete tests or code** to make tests pass
> - **Never change production code** unless fixing a documented bug
> - **Always read existing test files** before adding new tests
> - **Never overwrite test files** - append to existing files

This document provides patterns, examples, and best practices for writing tests to achieve 100% code coverage.

---

## Table of Contents

1. [Project Structure](#project-structure)
2. [Test Frameworks](#test-frameworks)
3. [Testing Patterns](#testing-patterns)
4. [Service Tests](#service-tests)
5. [ViewModel Tests](#viewmodel-tests)
6. [Common Patterns](#common-patterns)
7. [Troubleshooting](#troubleshooting)

---

## Project Structure

```
tests/
├── FlatFile.Services.Tests/       # Backend service tests
│   ├── CsvParserTests.cs
│   ├── CsvWriterTests.cs          # TODO
│   ├── SqliteDataServiceTests.cs
│   ├── RecentFilesServiceTests.cs # TODO
│   ├── TypeInferenceServiceTests.cs # TODO
│   └── LoadPerformanceTests.cs
│
├── FlatFile.UI.Tests/             # ViewModel tests (Avalonia.Headless)
│   └── ViewModels/
│       ├── PreferencesViewModelTests.cs
│       ├── DocumentViewModelTests.cs
│       ├── MainViewModelTests.cs  # TODO
│       └── DocumentLoadPerformanceTests.cs
│
└── FlatFile.E2E.Tests/            # End-to-end tests (FlaUI, Windows only)
    └── BasicSmokeTests.cs
```

---

## Test Frameworks

### FlatFile.Services.Tests

```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.2" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
```

### FlatFile.UI.Tests

```xml
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="Avalonia.Headless.XUnit" Version="11.3.0" />
<!-- Use [AvaloniaFact] for tests that need Avalonia dispatcher -->
```

### FlatFile.E2E.Tests (Windows only)

```xml
<PackageReference Include="FlaUI.Core" Version="4.0.0" />
<PackageReference Include="FlaUI.UIA3" Version="4.0.0" />
<!-- Target: net10.0-windows -->
```

---

## Testing Patterns

### Basic Test Structure

```csharp
using Xunit;

namespace FlatFile.Services.Tests;

public class MyServiceTests : IDisposable
{
    public MyServiceTests()
    {
        // Initialize test fixtures
    }

    public void Dispose()
    {
        // Clean up temp files, connections, etc.
    }

    [Fact]
    public void MethodName_Scenario_ExpectedResult()
    {
        var sut = new MyService();
        
        var result = sut.DoSomething();
        
        Assert.NotNull(result);
    }
}
```

**Comment Policy**: 
- Don't write obvious comments like `// Arrange`, `// Act`, `// Assert`
- The AAA structure should be clear from whitespace separation
- Only add comments to explain something unexpected or non-obvious
- Follow the same patterns and style as existing tests in each file

### Async Test Pattern

```csharp
[Fact]
public async Task MethodAsync_Scenario_ExpectedResult()
{
    var sut = new MyService();
    
    var result = await sut.DoSomethingAsync(CancellationToken.None);
    
    Assert.True(result);
}
```

### Avalonia UI Test Pattern

```csharp
using Avalonia.Headless.XUnit;

[AvaloniaFact]  // Required for UI thread operations
public async Task LoadFileAsync_ValidFile_LoadsSuccessfully()
{
    using var vm = new DocumentViewModel();
    var path = CreateTempCsv("a,b,c\n1,2,3");
    
    var result = await vm.LoadFileAsync(path);
    
    Assert.True(result);
}
```

---

## Service Tests

### CsvParser Tests

```csharp
// tests/FlatFile.Services.Tests/CsvParserTests.cs

[Fact]
public async Task ParseFileAsync_ValidCsv_ReturnsCorrectRowCount()
{
    var path = CreateTempCsv("Name,Age\nAlice,30\nBob,25");
    var parser = new CsvParser(new CsvParseOptions());
    
    var result = await parser.ParseFileAsync(path, CancellationToken.None);
    
    Assert.Equal(2, result.RowCount);
    Assert.Equal(2, result.Headers.Count);
}

[Fact]
public async Task ParseWithStreamingAsync_LargeFile_StreamsRows()
{
    var sb = new StringBuilder("Col1,Col2\n");
    for (int i = 0; i < 1000; i++)
        sb.AppendLine($"val{i},data{i}");
    var path = CreateTempCsv(sb.ToString());
    
    var parser = new CsvParser(new CsvParseOptions());
    var rowCount = 0;
    
    await foreach (var row in parser.StreamRowsAsync(path, CancellationToken.None))
    {
        rowCount++;
    }
    
    Assert.Equal(1000, rowCount);
}
```

### CsvWriter Tests

```csharp
// tests/FlatFile.Services.Tests/CsvWriterTests.cs

[Fact]
public async Task WriteFileAsync_ValidData_CreatesFile()
{
    var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    _tempFiles.Add(path);
    
    var writer = new CsvWriter(new CsvWriteOptions { Delimiter = ',' });
    var headers = new[] { "Name", "Age" };
    var rows = new List<string[]> { new[] { "Alice", "30" }, new[] { "Bob", "25" } };
    
    await writer.WriteFileAsync(path, headers, rows);
    
    Assert.True(File.Exists(path));
    var content = await File.ReadAllTextAsync(path);
    Assert.Contains("Name,Age", content);
    Assert.Contains("Alice,30", content);
}

[Fact]
public void EscapeField_ContainsComma_WrapsInQuotes()
{
    var writer = new CsvWriter(new CsvWriteOptions { Delimiter = ',' });
    
    var result = writer.WriteToString(
        new[] { "Name" },
        new List<string[]> { new[] { "Smith, John" } }
    );
    
    Assert.Contains("\"Smith, John\"", result);
}

[Fact]
public void EscapeField_ContainsQuote_EscapesQuote()
{
    var writer = new CsvWriter(new CsvWriteOptions());
    
    var result = writer.WriteToString(
        new[] { "Quote" },
        new List<string[]> { new[] { "He said \"Hello\"" } }
    );
    
    Assert.Contains("\"\"Hello\"\"", result);
}
```

### SqliteDataService Tests

```csharp
// tests/FlatFile.Services.Tests/SqliteDataServiceTests.cs

[Fact]
public void BeginEditTransaction_CreatesTransaction()
{
    var csvPath = CreateTempCsvPath();
    _service.Open(csvPath);
    _service.CreateDataTable(new[] { "Name" }.ToList());
    
    _service.BeginEditTransaction("Test Edit");
    _service.InsertRow(new[] { "Name" }.ToList(), new[] { "Value" });
    _service.CommitCurrentTransaction();
    
    var history = _service.GetTransactionHistory();
    Assert.Single(history);
    Assert.Equal("Test Edit", history[0].Description);
}

[Fact]
public void UndoTransaction_RevertsAllActions()
{
    var csvPath = CreateTempCsvPath();
    _service.Open(csvPath);
    _service.CreateDataTable(new[] { "Name" }.ToList());
    
    _service.BeginEditTransaction("Multi-row insert");
    _service.InsertRow(new[] { "Name" }.ToList(), new[] { "A" });
    _service.InsertRow(new[] { "Name" }.ToList(), new[] { "B" });
    _service.CommitCurrentTransaction();
    
    Assert.Equal(2, _service.GetTotalRowCount());
    
    var tx = _service.GetLastUndoableTransaction();
    _service.UndoTransaction(tx!.Value.TransactionId);
    
    Assert.Equal(0, _service.GetTotalRowCount());
}

[Fact]
public async Task CreateFtsIndexAsync_CreatesSearchableIndex()
{
    var csvPath = CreateTempCsvPath();
    _service.Open(csvPath);
    var headers = new List<string> { "Name", "Description" };
    _service.CreateDataTable(headers);
    _service.InsertRow(headers, new[] { "Alice", "Software Engineer" });
    _service.InsertRow(headers, new[] { "Bob", "Data Scientist" });
    
    await _service.CreateFtsIndexAsync(headers.ToArray(), CancellationToken.None);
    
    Assert.True(_service.HasFtsIndex());
}

[Fact]
public async Task SearchAsync_FindsMatchingRows()
{
    var csvPath = CreateTempCsvPath();
    _service.Open(csvPath);
    var headers = new List<string> { "Name", "Job" };
    _service.CreateDataTable(headers);
    _service.InsertRow(headers, new[] { "Alice", "Engineer" });
    _service.InsertRow(headers, new[] { "Bob", "Designer" });
    await _service.CreateFtsIndexAsync(headers.ToArray(), CancellationToken.None);
    
    var results = await _service.SearchAsync("Engineer", 10, CancellationToken.None);
    
    Assert.Single(results);
    Assert.Contains("Alice", results[0].Values);
}
```

### TypeInferenceService Tests

```csharp
// tests/FlatFile.Services.Tests/TypeInferenceServiceTests.cs

[Theory]
[InlineData("123", ColumnDataType.Integer)]
[InlineData("-456", ColumnDataType.Integer)]
[InlineData("12.34", ColumnDataType.Number)]
[InlineData("-0.5", ColumnDataType.Number)]
[InlineData("$100.00", ColumnDataType.Currency)]
[InlineData("75%", ColumnDataType.Percent)]
[InlineData("true", ColumnDataType.Boolean)]
[InlineData("false", ColumnDataType.Boolean)]
[InlineData("yes", ColumnDataType.Boolean)]
[InlineData("2024-01-15", ColumnDataType.Date)]
[InlineData("test@example.com", ColumnDataType.Email)]
[InlineData("https://example.com", ColumnDataType.Url)]
[InlineData("random text", ColumnDataType.String)]
public void DetectType_VariousInputs_ReturnsCorrectType(string value, ColumnDataType expected)
{
    var result = TypeInferenceService.DetectType(value);
    
    Assert.Equal(expected, result);
}

[Fact]
public void InferColumnTypes_MixedData_InfersCorrectTypes()
{
    var headers = new[] { "ID", "Name", "Amount", "Active" };
    var rows = new List<string[]>
    {
        new[] { "1", "Alice", "100.50", "true" },
        new[] { "2", "Bob", "200.75", "false" },
        new[] { "3", "Charlie", "300.00", "yes" }
    };
    
    var results = TypeInferenceService.InferColumnTypes(headers, rows);
    
    Assert.Equal(4, results.Count);
    Assert.Equal(ColumnDataType.Integer, results[0].DataType);
    Assert.Equal(ColumnDataType.String, results[1].DataType);
    Assert.Equal(ColumnDataType.Number, results[2].DataType);
    Assert.Equal(ColumnDataType.Boolean, results[3].DataType);
}
```

### RecentFilesService Tests

```csharp
// tests/FlatFile.Services.Tests/RecentFilesServiceTests.cs

[Fact]
public void AddRecentFile_NewFile_AddsToList()
{
    var service = new RecentFilesService(_tempPath);
    
    service.AddRecentFile("/path/to/file.csv");
    
    Assert.Single(service.RecentFiles);
    Assert.Equal("/path/to/file.csv", service.RecentFiles[0]);
}

[Fact]
public void AddRecentFile_DuplicateFile_MovesToTop()
{
    var service = new RecentFilesService(_tempPath);
    service.AddRecentFile("/path/file1.csv");
    service.AddRecentFile("/path/file2.csv");
    
    service.AddRecentFile("/path/file1.csv");
    
    Assert.Equal(2, service.RecentFiles.Count);
    Assert.Equal("/path/file1.csv", service.RecentFiles[0]);
}

[Fact]
public void AddRecentFile_ExceedsMax_RemovesOldest()
{
    var service = new RecentFilesService(_tempPath);
    for (int i = 0; i < 15; i++)
        service.AddRecentFile($"/path/file{i}.csv");
    
    Assert.Equal(10, service.RecentFiles.Count); // MaxRecentFiles = 10
    Assert.Equal("/path/file14.csv", service.RecentFiles[0]);
}

[Fact]
public void Save_And_Load_PersistsFiles()
{
    var service1 = new RecentFilesService(_tempPath);
    service1.AddRecentFile("/path/test.csv");
    service1.Save();
    
    var service2 = new RecentFilesService(_tempPath);
    
    Assert.Single(service2.RecentFiles);
    Assert.Equal("/path/test.csv", service2.RecentFiles[0]);
}
```

---

## ViewModel Tests

### DocumentViewModel Tests

```csharp
// tests/FlatFile.UI.Tests/ViewModels/DocumentViewModelTests.cs

[AvaloniaFact]
public async Task SaveFileAsync_DirtyDocument_SavesAndClearsDirty()
{
    var path = CreateTempCsv("a,b\n1,2");
    using var vm = new DocumentViewModel();
    await vm.LoadFileAsync(path);
    
    vm.UpdateCell(vm.Rows[0].RowId, 0, "1", "X");
    Assert.True(vm.IsDirty);
    
    var result = await vm.SaveFileAsync();
    
    Assert.True(result);
    Assert.False(vm.IsDirty);
}

[AvaloniaFact]
public void AddRow_BlankDocument_AddsRowToGrid()
{
    using var vm = new DocumentViewModel();
    var initialCount = vm.Rows.Count;
    
    vm.AddRow();
    
    Assert.Equal(initialCount + 1, vm.Rows.Count);
    Assert.True(vm.IsDirty);
}

[AvaloniaFact]
public void DeleteRow_ExistingRow_RemovesFromGrid()
{
    using var vm = new DocumentViewModel();
    vm.AddRow();
    var row = vm.Rows[0];
    var initialCount = vm.Rows.Count;
    
    vm.DeleteRow(row.RowId);
    
    Assert.Equal(initialCount - 1, vm.Rows.Count);
}

[AvaloniaFact]
public async Task Undo_AfterEdit_RevertsChange()
{
    var path = CreateTempCsv("Name\nAlice");
    using var vm = new DocumentViewModel();
    await vm.LoadFileAsync(path);
    
    var originalValue = vm.Rows[0].Values[0];
    vm.TrackCellEdit(vm.Rows[0].RowId, 0, originalValue, "Bob");
    vm.CommitAllPendingEdits();
    
    Assert.Equal("Bob", vm.Rows[0].Values[0]);
    
    vm.Undo();
    
    Assert.Equal("Alice", vm.Rows[0].Values[0]);
}

[AvaloniaFact]
public async Task ExecuteQuery_ValidSql_ReturnsResults()
{
    var path = CreateTempCsv("Name,Age\nAlice,30\nBob,25");
    using var vm = new DocumentViewModel();
    await vm.LoadFileAsync(path);
    
    vm.SqlQuery = "SELECT * FROM data WHERE Age > 26";
    
    await vm.ExecuteQuery();
    
    Assert.True(vm.IsShowingQueryResults);
    Assert.Equal(1, vm.QueryResultCount);
}
```

### PreferencesService Tests

```csharp
// tests/FlatFile.UI.Tests/Preferences/PreferencesServiceTests.cs

[Fact]
public void ToToml_DefaultPreferences_GeneratesValidToml()
{
    var prefs = AppPreferences.CreateDefault();
    var service = PreferencesService.Instance;
    
    var toml = service.ToToml(prefs);
    
    Assert.Contains("[grid]", toml);
    Assert.Contains("page_size", toml);
    Assert.Contains("initial_rows", toml);
}

[Fact]
public void TryParseToml_ValidToml_ParsesCorrectly()
{
    var toml = @"
[grid]
page_size = 5000
initial_rows = 10000
show_row_numbers = true
";
    var service = PreferencesService.Instance;
    
    var result = service.TryParseToml(toml, out var prefs, out var error);
    
    Assert.True(result);
    Assert.Null(error);
    Assert.Equal(5000, prefs!.Grid.PageSize);
    Assert.Equal(10000, prefs.Grid.InitialRows);
}

[Theory]
[InlineData("Ctrl+S", "Ctrl+S")]
[InlineData("ctrl+s", "Ctrl+S")]
[InlineData("CTRL+S", "Ctrl+S")]
[InlineData("Ctrl+Shift+S", "Ctrl+Shift+S")]
[InlineData("Alt+F4", "Alt+F4")]
public void NormalizeGesture_VariousInputs_NormalizesCorrectly(string input, string expected)
{
    var service = PreferencesService.Instance;
    
    var result = service.NormalizeGesture(input);
    
    Assert.Equal(expected, result);
}

[Theory]
[InlineData("page_size = 1000 # comment", "page_size = 1000")]
[InlineData("# full line comment", "")]
[InlineData("value = \"string with # inside\"", "value = \"string with # inside\"")]
public void StripComment_VariousInputs_StripsCorrectly(string input, string expected)
{
    var service = PreferencesService.Instance;
    
    var result = service.StripComment(input);
    
    Assert.Equal(expected, result.Trim());
}
```

---

## Common Patterns

### Temp File Helper

**CRITICAL**: Tests run in parallel. Each test MUST use unique file paths (GUIDs) and properly close database connections before cleanup.

```csharp
private readonly List<string> _tempFiles = [];
private SqliteDataService? _service;

private string CreateTempCsv(string content, Encoding? encoding = null)
{
    var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    File.WriteAllText(path, content, encoding ?? Encoding.UTF8);
    _tempFiles.Add(path);
    return path;
}

private string CreateTempCsvPath()
{
    var path = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.csv");
    _tempFiles.Add(path);
    return path;
}

public void Dispose()
{
    // IMPORTANT: Close database connections FIRST
    try
    {
        _service?.Close();
        _service?.Dispose();
    }
    catch { /* Ignore */ }

    // Then clean up files (wrapped in try-catch for parallel test safety)
    foreach (var file in _tempFiles)
    {
        try
        {
            if (File.Exists(file)) File.Delete(file);
            var cacheFile = file + ".ff";
            if (File.Exists(cacheFile)) File.Delete(cacheFile);
        }
        catch { /* Ignore - file may be locked by parallel test */ }
    }
}
```

### File Locking Prevention

Tests fail with "file is being used by another process" when SQLite connections aren't properly closed. Follow these rules:

1. **Always call `Close()` before `Dispose()`** on SqliteDataService
2. **Use GUIDs in file paths** - never use predictable/shared paths
3. **Wrap cleanup in try-catch** - don't fail tests on cleanup errors
4. **For DocumentViewModel**, use `using var vm = ...` pattern
5. **Don't share state between tests** - each test creates its own files

### Theory Tests for Multiple Scenarios

```csharp
[Theory]
[InlineData(",", "a,b,c")]
[InlineData("\t", "a\tb\tc")]
[InlineData(";", "a;b;c")]
[InlineData("|", "a|b|c")]
public async Task DetectDelimiter_VariousDelimiters_DetectsCorrectly(
    string delimiter, string headerRow)
{
    var content = $"{headerRow}\n1{delimiter}2{delimiter}3";
    var path = CreateTempCsv(content);
    
    var detected = await CsvParser.DetectDelimiterAsync(
        path, Encoding.UTF8, CancellationToken.None);
    
    Assert.Equal(delimiter[0], detected);
}
```

### Testing Exceptions

```csharp
[Fact]
public async Task LoadFileAsync_NullPath_ThrowsArgumentNullException()
{
    using var vm = new DocumentViewModel();
    
    await Assert.ThrowsAsync<ArgumentNullException>(
        () => vm.LoadFileAsync(null!));
}

[Fact]
public void UpdateCell_ReadOnlyMode_RaisesEvent()
{
    using var vm = new DocumentViewModel();
    vm.ToggleReadOnly();
    var eventRaised = false;
    vm.ReadOnlyEditAttempted += (_, _) => eventRaised = true;
    
    vm.UpdateCell(1, 0, "old", "new");
    
    Assert.True(eventRaised);
}
```

### Testing Event Handlers

```csharp
[Fact]
public void PropertyChanged_WhenModified_RaisesEvent()
{
    var vm = new PreferencesViewModel(PreferencesService.Instance);
    var propertyNames = new List<string>();
    vm.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);
    
    vm.PageSize = 999;
    
    Assert.Contains("PageSize", propertyNames);
    Assert.Contains("IsDirty", propertyNames);
}
```

---

## Troubleshooting

### Common Issues

**1. Avalonia not initialized**
```
Error: Avalonia application has not been initialized
```
Solution: Use `[AvaloniaFact]` instead of `[Fact]` for tests that need UI thread.

**2. File locked / Used by another process**
```
Error: The process cannot access the file because it is being used by another process
```
Solution: This is the most common issue. Fix by:
- Call `_service.Close()` BEFORE `_service.Dispose()`
- Use unique GUIDs in all file paths: `$"test_{Guid.NewGuid()}.csv"`
- Wrap file cleanup in try-catch blocks
- Use `using var vm = new DocumentViewModel()` for auto-disposal

**3. SQLite database locked**
```
Error: Database is locked
```
Solution: 
- Ensure `SqliteDataService.Close()` is called before Dispose
- Don't share database paths between tests
- Each test must create its own unique .ff file path

**4. Test isolation**
```
Error: Test depends on state from another test
```
Solution: Each test should create its own temp files and service instances. Never use shared paths like `test.csv` - always use `$"test_{Guid.NewGuid()}.csv"`.

**5. Cleanup failures causing test failures**
```
Error: Cannot delete file during Dispose
```
Solution: Wrap ALL cleanup code in try-catch. Tests should pass even if cleanup fails - the next test uses different files anyway.

### Running Specific Tests

```bash
# Run single test
dotnet test --filter "FullyQualifiedName=FlatFile.Services.Tests.CsvParserTests.DetectDelimiterAsync_CommaDelimited_ReturnsComma"

# Run test class
dotnet test --filter "ClassName=CsvParserTests"

# Run tests matching pattern
dotnet test --filter "Name~Delimiter"
```

---

## Checklist for New Tests

Before submitting tests, verify:

- [ ] Test follows AAA pattern (Arrange, Act, Assert)
- [ ] Test has descriptive name: `Method_Scenario_ExpectedResult`
- [ ] Test uses `[AvaloniaFact]` if it needs UI thread
- [ ] Test doesn't depend on other tests
- [ ] Test handles async properly with `await`
- [ ] No obvious comments (structure is clear from whitespace)
- [ ] Follows same style as existing tests in the file
- [ ] **File paths use GUIDs** - `$"test_{Guid.NewGuid()}.csv"`
- [ ] **Close() called before Dispose()** for SqliteDataService
- [ ] **Cleanup wrapped in try-catch** - don't fail on cleanup errors

---

## Handling Failures and Edge Cases

### When Existing Tests Fail

**DO NOT modify or delete existing tests.** Instead:

1. Document the failure:
   ```
   Test: SqliteDataServiceTests.UpdateCell_ModifiesValue
   Error: Assert.Equal() Failure - Expected: "Bob", Actual: "Alice"
   ```

2. Analyze the cause:
   - Is the test wrong? (unlikely if it was passing before)
   - Is the code broken? (more likely)
   - Did a recent change break it?

3. Report to user and wait for permission:
   ```
   "Test X is failing. Analysis: [your analysis]. 
   May I fix [the test / the code]?"
   ```

### When Your New Tests Fail

1. First, check your test logic
2. If your test is correct, you may have found a bug
3. Document the bug and ask before fixing production code:
   ```
   "My test for LoadPage() found that it returns null when offset > rowcount.
   This appears to be a bug. May I fix SqliteDataService.cs?"
   ```

### When Adding Tests to Existing Files

```bash
# First, check if file exists
ls tests/FlatFile.Services.Tests/CsvWriterTests.cs

# If it exists, READ it first
cat tests/FlatFile.Services.Tests/CsvWriterTests.cs

# Then ADD your tests to the existing class, don't overwrite
```

### Never Do These Things

- Delete a failing test to make the suite pass
- Change assertions to match broken behavior
- Overwrite an existing test file with a new one
- Create duplicate tests with slightly different names
- Modify production code without documenting why

---

## Next Steps

1. Review [coverage-checklist.md](./coverage-checklist.md) for uncovered methods
2. Pick items marked (uncovered)
3. **Read existing test files** to see what's already covered
4. Write tests following patterns in this guide
5. Run tests: `dotnet test`
6. Update checklist to when complete
7. Verify with `git diff` that you only added, not deleted
8. Commit with message: `test: add tests for [ClassName]`
