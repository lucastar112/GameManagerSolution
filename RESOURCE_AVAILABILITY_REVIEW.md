# Resource readiness review

## Changes
- Renamed the `GameState` values to match the documented lifecycle at `0x80000CF6`:
  - `0`: `RaceNotLoaded`
  - `1`: `CacheBuildRequested`
  - `2`: `CacheReady`
- Replaced the boolean-only availability check with `ResourceAvailabilityManager.EnsureAvailable`.
- Race-scoped reads and writes now return the IPC error code `RESOURCE_NOT_READY` when state is `0` or `1`.
- If GameManager cannot read the state byte, it reports `READ_FAILED` instead of incorrectly claiming the resource is simply not ready.
- Both `GetResourceAsync` and `SetAsync` use the same availability validation.

## IPC behavior
The server already serializes failed requests as `IpcResponse` with `Success = false` and an `Error` object. The shared client turns that into `GameManagerIpcException`, preserving the error code in `exception.Code`.

Example:
```csharp
try
{
    uint? teamId = await client.GetAsync<uint>("TeamId", playerId: 5);
}
catch (GameManagerIpcException ex)
    when (ex.Code == IpcErrorCodes.ResourceNotReady)
{
    // The race cache is not ready yet; retry later or keep showing a loading state.
}
```

`RESOURCE_UNAVAILABLE` remains available for other cases, such as a dynamic cache entry/player pointer that cannot be resolved. This patch does not silently return zero or null for an unavailable resource.

## Verification limitation
The current environment does not have the `dotnet` CLI installed, so I could inspect the project and patch the source but could not run a .NET build here.
