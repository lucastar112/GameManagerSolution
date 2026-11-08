using GameManager.Shared.Ipc;
using Xunit;

namespace GameManager.Shared.Tests;

public sealed class IpcContractTests
{
    [Fact]
    public void Request_DefaultsToRequestMessageType()
    {
        var request = new IpcRequest { Operation = IpcOperation.Ping };

        Assert.Equal(IpcMessageType.Request, request.MessageType);
        Assert.Equal(IpcOperation.Ping, request.Operation);
    }

    [Fact]
    public void Response_DefaultsToResponseMessageType()
    {
        var response = new IpcResponse { RequestId = 42, Success = true };

        Assert.Equal(IpcMessageType.Response, response.MessageType);
        Assert.Equal(42, response.RequestId);
        Assert.True(response.Success);
    }
}
