namespace gameManager.src.Ipc
{
    public sealed class IpcResponse
    { 
        public long RequestId { get; init; }
        public bool Success { get; init; }
        public object? Value { get; init; }
        public string? Error { get; init; }
    }
}
