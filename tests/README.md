# GameManager test strategy

Automated tests live in this directory. Reusable production libraries remain under `libraries/`.

## Current suites

- `GameManager.Shared.Tests`: IPC message contract defaults and invariants.
- `GameManager.Dolphin.Tests`: platform contract for process-memory access.
- `GameManager.Tests`: command-line argument parsing and validation.
- `MTWManager.Tests`: pointer-chain layout constants and slot arithmetic.

## Running tests

From the repository root:

```sh
dotnet test tests/GameManager.Shared.Tests/GameManager.Shared.Tests.csproj
dotnet test tests/GameManager.Dolphin.Tests/GameManager.Dolphin.Tests.csproj
dotnet test tests/GameManager.Tests/GameManager.Tests.csproj
dotnet test tests/MTWManager.Tests/MTWManager.Tests.csproj
```

Or run the solution-wide test discovery/build workflow in CI. The GitHub Actions workflow runs cross-platform suites on Windows and macOS and builds the WPF DebugManager on Windows.

## Growth rules

1. Add a regression test when fixing a reproducible bug.
2. Keep unit tests close to the owning project in this directory; use `Integration.Tests` for cross-project workflows once fixtures are established.
3. Keep tests deterministic: no live Dolphin process, network, user-specific paths, or real game installation required for unit tests.
4. Binary format tests should use small synthetic fixtures for edge cases and separately maintained real-track fixtures for integration tests.
5. DebugManager UI automation is Windows-only. Parsing and track-format logic should be moved into platform-independent libraries before it receives cross-platform tests.
