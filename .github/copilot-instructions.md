# General Guidelines for Creating Test Classes

## Pre-test 

- Ensure you have a clear understanding of the system under test and its dependencies.
- Identify the testing type required (unit tests, integration tests, etc.) and act accordingly based on the guidelines defined below.
- Add the test class to the appropriate namespace, following the existing structure of the project.

<hr/>

## Unit Testing Guidelines

- Follow industry best practices for unit testing, focusing on isolation, readability, and maintainability.
- Work with mocks and stubs to isolate the unit of work. The library `Moq` is used for mocking dependencies.
- Use AutoFixture for generating test data to reduce boilerplate code.
- Ensure that tests are deterministic and do not rely on external systems or state.
- Use the Arrange-Act-Assert (AAA) pattern for structuring tests.
- Follow the naming conventions and structure outlined below to ensure consistency and clarity across all test classes.
- Reduce code duplication by using shared dependencies and helper methods.

### Class Structure & Initialization
- Name test classes with the `Tests` suffix.
- Declare all shared dependencies as `private readonly` fields at the class level to reduce code duplication.
- Initialize all mocks, fixtures, and the system under test in the constructor.
- Use AutoFixture for test data generation and configure it in the constructor (disable throwing recursion behavior if needed).

### Dependency Management
- Use `Mock<T>` for all external dependencies and declare them as `private readonly` fields.
- Initialize the `Fixture` as a `private readonly` field for consistent test data generation.
- Create the system under test (e.g., handler, validator) as a `private readonly` field, injecting mocked dependencies.
- Pre-create common test objects (like request DTOs) as `private readonly` fields when used across multiple tests.

### Naming Conventions
- Use descriptive test method names following the pattern: `When{Condition}_{Method}_{ExpectedOutcome}`.
- Use meaningful variable names that clearly indicate their purpose.

### Test Structure & Patterns
- Use `[Fact]` for single-scenario tests and `[Theory]` with `[InlineData]` for parameterized tests.
- Prefer async test methods for testing async code.
- Extract common setup logic and duplicated sections into private helper methods (e.g., `SetupContextAccessor()`, `SetupTranslateWithTemplate()`).

### Code Comments Rules
- **Required**: `// Arrange`, `// Act`, and `// Assert` comments in all test methods.
- **Forbidden**: All other explanatory comments within test methods.
- Code should be self-documenting through clear variable names and method names.
- Complex business logic explanations belong in the production code, not in tests.

### Assertions & Verification
- Use FluentValidation's `TestValidate()` for validator testing with `ShouldHaveValidationErrorFor()` and `ShouldNotHaveAnyValidationErrors()`.
- Use `Verify()` on mocks to ensure correct interactions with dependencies.
- Specify exact call counts with `Times.Once`, `Times.Never`, etc.
- Use `.Only()` when expecting a single validation error.
- Ensure the tests are valid and pass.

### Configuration & Setup
- Configure AutoFixture to handle recursion issues using `OmitOnRecursionBehavior`.
- Setup common dependencies (like HttpContext, translation services) in helper methods.
- Use `It.IsAny<T>()` for mock setups when the exact parameter value doesn't matter.
- Pre-configure common return values in the constructor for better test performance.
- Ensure that mock setups, use `It.IsAny<T>()` for optional parameters when the exact value is not critical to the test.
- Do not install new packages or change the project structure without prior approval.
- If you have to create a new type for mocking, ask for approval first.

### Example Unit Tests Template
```
public class MyServiceTests 
{ 
    private readonly Fixture fixture;
    private readonly Mock<IDependency1> dependency1Mock;
    private readonly Mock<IDependency2> dependency2Mock;
    private readonly MyService service;
    private readonly MyRequestDto request;

    public MyServiceTests()
    {
        fixture = new Fixture();
        dependency1Mock = new Mock<IDependency1>();
        dependency2Mock = new Mock<IDependency2>();
    
        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        service = new MyService(dependency1Mock.Object, dependency2Mock.Object);
        request = fixture.Create<MyRequestDto>();
    
        SetupCommonMocks();
    }

    [Fact]
    public async Task WhenValidRequest_Handle_ReturnsExpectedResult()
    {
        // Arrange
        var expectedResult = fixture.Create<MyResult>();
        dependency1Mock.Setup(x => x.DoSomethingAsync(It.IsAny<string>()))
                      .ReturnsAsync(expectedResult);

        // Act
        var result = await service.HandleAsync(request);

        // Assert
        Assert.NotNull(result);
        dependency1Mock.Verify(x => x.DoSomethingAsync(It.IsAny<string>()), Times.Once);
        dependency2Mock.Verify(x => x.LogActivity(It.IsAny<string>()), Times.Once);
    }

    private void SetupCommonMocks()
    {
        dependency2Mock.Setup(x => x.LogActivity(It.IsAny<string>()));
    }
}
```

<hr/>

## Integration Testing Guidelines

- Currently, integration tests are not required. You should ask for approval and clarification before creating any integration tests.