# Contributing to Jaunty

Thank you for your interest in contributing to Jaunty! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Code Review Guidelines](#code-review-guidelines)

---

## Code of Conduct

- Be respectful and inclusive in all interactions
- Focus on constructive feedback
- Welcome contributors of all skill levels

---

## Getting Started

### Prerequisites

- .NET 8.0 SDK or later
- Git for version control
- IDE of choice (Visual Studio, VS Code, Rider)

### Setup

1. Fork the repository
2. Clone your fork: `git clone https://github.com/YOUR_USERNAME/jaunty.git`
3. Create a branch: `git checkout -b feature/your-feature-name`

---

## Development Workflow

### Branch Naming

- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation updates
- `refactor/` - Code refactoring
- `test/` - Test additions or modifications

### Commit Messages

Follow conventional commit format:

```
<type>(<scope>): <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting, missing semicolons, etc.
- `refactor`: Code refactoring
- `test`: Adding tests
- `chore`: Maintenance tasks

**Example:**
```
feat(read): Add support for custom mapping mode

Added new MappingMode.Custom enum value and support for 
custom mapper functions in Query methods.

Closes #123
```

---

## Coding Standards

### C# Conventions

- Use `var` when the type is obvious
- Use explicit types when clarity is improved
- Prefer expression-bodied members for simple methods
- Use XML documentation comments for all public APIs

### Naming Conventions

- **Classes**: PascalCase (`QueryCore`, `ParameterBinder`)
- **Methods**: PascalCase (`ExecuteQuery`, `BindParameters`)
- **Properties**: PascalCase (`CommandText`, `Parameters`)
- **Parameters**: camelCase (`connection`, `sql`, `cancellationToken`)
- **Private fields**: camelCase with underscore prefix (`_cache`, `_mapper`)
- **Interfaces**: PascalCase with "I" prefix (`IMapped`, `IEntity`)

### File Organization

- One class per file (with exceptions for small related types)
- File name matches class name
- Organize files by namespace and functionality

### Code Style Examples

```csharp
// Good: Clear, concise, well-documented
/// <summary>
/// Executes a SQL query and returns all results mapped to entities.
/// </summary>
public static List<T> Query<T>(this IDbConnection connection, string sql) where T : new()
{
    return QueryCore<T>(connection, sql, null, default, MappingMode.Strict);
}

// Avoid: Unclear naming, missing documentation
public static List<T> DoQuery<T>(IDbConnection conn, string s) where T : new()
{
    return QueryCore<T>(conn, s, null, default, MappingMode.Strict);
}
```

---

## Testing Guidelines

### Test Organization

- Place tests in `tests/Jaunty.Tests/`
- Mirror source directory structure
- Use descriptive test method names: `MethodName_Scenario_ExpectedResult`

### Assertion Library

**Jaunty uses xUnit's built-in Assert class** for all assertions.

> **Note (2026-02-25)**: FluentAssertions was removed due to licensing restrictions.
> All tests now use standard xUnit Assert methods.

**Examples:**

```csharp
// Equality checks
Assert.Equal(expected, actual);
Assert.NotEqual(unexpected, actual);

// Null checks
Assert.Null(value);
Assert.NotNull(value);

// Collection checks
Assert.Empty(collection);
Assert.NotEmpty(collection);
Assert.Single(collection);
Assert.Contains(item, collection);
Assert.DoesNotContain(item, collection);
Assert.All(collection, item => Assert.True(condition));

// Boolean checks
Assert.True(condition);
Assert.False(condition);

// Exception checks
var ex = Assert.Throws<ExceptionType>(() => action);
Assert.Contains("expected message", ex.Message);

// String checks
Assert.StartsWith(prefix, value);
Assert.EndsWith(suffix, value);
Assert.Contains(substring, value);
```

### Test Types

1. **Unit Tests**: Test individual methods/classes in isolation
2. **Integration Tests**: Test database operations with SQLite
3. **Edge Case Tests**: Test boundary conditions and error scenarios

### Test Example

```csharp
[Fact]
public void Query_NullConnection_ThrowsArgumentNullException()
{
    IDbConnection? connection = null;

    var ex = Assert.Throws<ArgumentNullException>(() =>
        connection!.Query<Category>("SELECT * FROM categories"));

    Assert.Equal("connection", ex.ParamName);
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~QueryTests"

# Run tests with coverage
dotnet test /p:CollectCoverage=true
```

---

## Pull Request Process

### Before Submitting

1. Ensure all tests pass: `dotnet test`
2. Build succeeds without warnings: `dotnet build`
3. Code follows project conventions
4. XML documentation added for public APIs
5. Update documentation if behavior changes

### PR Description Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to change)
- [ ] Documentation update

## Testing
- [ ] Tests added/updated
- [ ] All tests pass locally
- [ ] Integration tests pass

## Checklist
- [ ] Code follows project conventions
- [ ] Code is documented
- [ ] No new warnings introduced
```

### Review Process

1. Submit PR with clear description
2. Wait for automated checks to pass
3. Address reviewer feedback
4. Squash commits if requested
5. PR will be merged by maintainer

---

## Code Review Guidelines

### For Authors

- Respond to feedback promptly
- Explain reasoning for complex changes
- Be open to suggestions

### For Reviewers

- Focus on logic, architecture, and correctness
- Be constructive and respectful
- Suggest improvements, not just criticisms
- Acknowledge good solutions

### Review Checklist

- [ ] Code compiles without errors
- [ ] Tests pass
- [ ] Logic is correct
- [ ] Edge cases handled
- [ ] Error handling appropriate
- [ ] Performance considered
- [ ] Security considered
- [ ] Documentation complete
- [ ] Follows coding standards

---

## Questions?

- Open an issue for questions or discussions
- Check existing issues before creating new ones
- Tag issues appropriately (bug, enhancement, question)

---

Thank you for contributing to Jaunty!
