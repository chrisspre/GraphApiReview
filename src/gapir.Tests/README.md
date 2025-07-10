# gapir.Tests

This test project contains comprehensive integration tests for the `gapir` (Graph API Review) command-line tool.

## Test Structure

The tests are organized using data-driven testing patterns with xUnit's `[Theory]` and `[InlineData]` attributes to minimize repetition and maximize coverage.

### Test Categories

#### 1. Help Command Tests (`Help_Flags_ShowUsageInformation`)
- **Data-driven test** using `[Theory]` with 2 test cases
- Tests both `--help` and `-h` flags
- Validates appropriate level of detail for each flag
- Ensures help output includes proper usage information

#### 2. Flag Combination Tests (`Flag_Combinations_ProduceExpectedBehavior`)
- **Data-driven test** using `[Theory]` with 14 test cases
- Tests all possible combinations of flags:
  - `--verbose` / `-v` (diagnostic output control)
  - `--show-approved` / `-a` (show already approved PRs)
  - `--full-urls` / `-f` (URL format control)
- Validates:
  - Correct verbose output behavior
  - Proper approved PR section display
  - URL format (short vs. full)
  - Integration between multiple flags

#### 3. Edge Case Tests (`Invalid_Or_Edge_Case_Arguments_HandleGracefully`)
- **Data-driven test** using `[Theory]` with 5 test cases
- Tests graceful handling of:
  - Unknown flags (`--unknown-flag`, `-x`)
  - Invalid argument formats (`--show-approved=true`)
  - Extra arguments (`--verbose extra-arg`)
  - Help flag precedence (`--help --verbose`)

#### 4. Default Behavior Tests (`Default_Behavior_Works_With_No_Arguments`)
- **Data-driven test** using `[Theory]` with 2 test cases
- Tests default behavior with:
  - No arguments
  - Whitespace-only arguments
- Validates default flag values and output format

## Test Data Coverage

| Test Case | Verbose | Show Approved | Full URLs | Notes |
|-----------|---------|---------------|-----------|-------|
| Default | ❌ | ❌ | ❌ | Baseline behavior |
| `--verbose` | ✅ | ❌ | ❌ | Diagnostic output |
| `-v` | ✅ | ❌ | ❌ | Short flag variant |
| `--show-approved` | ❌ | ✅ | ❌ | Include approved PRs |
| `-a` | ❌ | ✅ | ❌ | Short flag variant |
| `--show-approved --full-urls` | ❌ | ✅ | ✅ | Approved + full URLs |
| `-a -f` | ❌ | ✅ | ✅ | Short flags combined |
| `--show-approved --verbose` | ✅ | ✅ | ❌ | Approved + verbose |
| `-a -v` | ✅ | ✅ | ❌ | Short flags combined |
| `--verbose --full-urls` | ✅ | ❌ | ✅ | Verbose + full URLs |
| `-v -f` | ✅ | ❌ | ✅ | Short flags combined |
| `--show-approved --verbose --full-urls` | ✅ | ✅ | ✅ | All flags enabled |
| `-a -v -f` | ✅ | ✅ | ✅ | All short flags |
| `--invalid-flag` | ❌ | ❌ | ❌ | Graceful degradation |

## Key Features Tested

### Verbose Output Control
- **When enabled**: Shows "Authenticating", "Successfully authenticated!", "Checking pull requests"
- **When disabled**: No diagnostic messages in output

### Approved PRs Display
- **When enabled**: Shows "PR(s) you have already approved:" section
- **When disabled**: Section is omitted from output

### URL Format Control
- **Short URLs (default)**: Uses `http://g.io/pr/` format
- **Full URLs**: Uses `https://msazure.visualstudio.com` format

### Help Output
- **`--help`**: Full help with detailed flag documentation
- **`-h`**: Basic usage information

### Error Handling
- **Invalid flags**: Ignored gracefully, tool continues normally
- **Malformed arguments**: Processed with best effort
- **Extra arguments**: Ignored, core functionality preserved

## Running Tests

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity normal

# Run specific test method
dotnet test --filter "MethodName~Flag_Combinations"
```

## Test Implementation Notes

- Tests use **process integration testing** - they run the actual compiled `gapir.exe`
- Each test has a **30-second timeout** to prevent hanging
- All output (stdout/stderr) is captured and analyzed
- Tests use relative paths to locate the `gapir` project directory
- **Data-driven approach** reduces code duplication and improves maintainability
- Tests validate both positive and negative cases for robust coverage

## Total Test Count: 23

- 2 help flag variations
- 14 flag combination scenarios  
- 5 edge case scenarios
- 2 default behavior scenarios

This comprehensive test suite ensures that all command-line flag combinations work correctly and the tool behaves predictably across different usage scenarios.
