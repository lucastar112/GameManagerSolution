namespace gameManager.src.Ipc 
{
    public sealed class IpcRequest 
    {
        public long RequestId { get; init; }
        public string Operation { get; init; } = string.Empty;
        public string Resource { get; init; } = string.Empty;
    }
}
