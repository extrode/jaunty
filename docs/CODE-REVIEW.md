# Code Review Checklist

Use this checklist when reviewing pull requests for Jaunty.

## Quick Links

- [Contributing Guide](../CONTRIBUTING.md)
- [API Design Guidelines](API-DESIGN.md)
- [Architecture Decisions](ARCHITECTURE-DECISIONS.md)

---

## Pre-Review Checklist

Before starting a review, verify:

- [ ] PR has clear description of changes
- [ ] Related issues are linked
- [ ] Tests are included (if applicable)
- [ ] Documentation is updated (if applicable)

---

## Code Quality

### Correctness

- [ ] Logic is correct and complete
- [ ] Edge cases are handled
- [ ] Error handling is appropriate
- [ ] No obvious bugs or typos

### Performance

- [ ] No unnecessary allocations in hot paths
- [ ] Async code uses `ConfigureAwait(false)`
- [ ] Caching is used appropriately
- [ ] No obvious performance issues

### Security

- [ ] No SQL injection vulnerabilities (using parameterized queries)
- [ ] No sensitive data in error messages
- [ ] Input validation is present
- [ ] No hardcoded credentials or secrets

---

## Code Style

### Naming

- [ ] Classes use PascalCase
- [ ] Methods use PascalCase
- [ ] Parameters use camelCase
- [ ] Private fields use `_camelCase`
- [ ] Names are descriptive and clear

### Organization

- [ ] File organization is logical
- [ ] Methods are focused and concise
- [ ] Classes have single responsibility
- [ ] No god classes or methods

### Comments

- [ ] XML documentation on public APIs
- [ ] Inline comments explain "why" not "what"
- [ ] No commented-out code
- [ ] TODO comments have issue references

---

## API Design

### Consistency

- [ ] Follows existing API patterns
- [ ] Parameter order is consistent
- [ ] Naming matches existing conventions
- [ ] Return types are appropriate

### Usability

- [ ] API is intuitive to use
- [ ] Error messages are helpful
- [ ] Examples are provided (in documentation)
- [ ] Breaking changes are avoided or justified

### Async Support

- [ ] Async version available for I/O operations
- [ ] CancellationToken parameter included
- [ ] Async method names end with `Async`
- [ ] No sync-over-async patterns

---

## Testing

### Coverage

- [ ] New code has test coverage
- [ ] Edge cases are tested
- [ ] Error scenarios are tested
- [ ] Integration tests included (if applicable)

### Test Quality

- [ ] Test names describe the scenario
- [ ] Tests are independent and isolated
- [ ] No flaky tests
- [ ] Tests follow Arrange-Act-Assert pattern

### Test Execution

- [ ] All tests pass locally
- [ ] No test failures in CI
- [ ] Performance tests meet thresholds (if applicable)

---

## Documentation

### Code Documentation

- [ ] Public APIs have XML documentation
- [ ] Documentation includes examples
- [ ] Exceptions are documented
- [ ] SeeAlso references related APIs

### User Documentation

- [ ] README updated (if applicable)
- [ ] API documentation generated successfully
- [ ] Breaking changes documented
- [ ] Migration guide provided (if applicable)

---

## Git Hygiene

### Commits

- [ ] Commit messages are clear and descriptive
- [ ] Commits are logically grouped
- [ ] No merge commits in feature branch
- [ ] Commit history tells a coherent story

### Branch

- [ ] Branch name follows convention
- [ ] Branch is up to date with main
- [ ] No unrelated changes in PR

---

## Specific Areas

### Database Operations

- [ ] SQL is parameterized (no string concatenation)
- [ ] Connection handling is correct (open/close)
- [ ] Transaction handling is correct
- [ ] Dialect-specific code is isolated

### Caching

- [ ] Cache keys are unique and stable
- [ ] Cache invalidation is handled
- [ ] Thread safety is considered
- [ ] Memory usage is reasonable

### Error Handling

- [ ] Specific exception types used
- [ ] Error messages are informative
- [ ] Inner exceptions preserved
- [ ] No swallowing exceptions

---

## Review Recommendations

### Must Fix (Blocking)

List issues that must be fixed before merging:

1. 
2. 
3. 

### Should Fix (Recommended)

List issues that should be fixed but don't block merging:

1. 
2. 

### Nice to Have (Optional)

List suggestions for future improvement:

1. 
2. 

---

## Final Checklist

Before approving:

- [ ] All blocking issues resolved
- [ ] Code is production-ready
- [ ] Tests pass
- [ ] Documentation complete
- [ ] No concerns unaddressed

---

## Approval

- [ ] **Approved** - Ready to merge
- [ ] **Approved with minor fixes** - Can merge after addressing non-blocking feedback
- [ ] **Changes requested** - Requires re-review after fixes
- [ ] **Not approved** - Significant issues, needs substantial rework

---

## Reviewer Notes

Add any additional context, concerns, or observations:

