using MTW.Memory;
using Xunit;

namespace MTWManager.Tests;

public sealed class PointerChainContractTests
{
    [Fact]
    public void PlayerSlots_MatchDocumentedTwelveSlotLayout()
    {
        Assert.Equal(12, ConfirmedPointerChains.PlayerSlotCount);
        Assert.Equal(4, ConfirmedPointerChains.PlayerPointerStride);
    }

    [Fact]
    public void PlayerObjectChain_UsesPlayerSpecificSlotOffset()
    {
        var first = ConfirmedPointerChains.PlayerObjectChain(0);
        var last = ConfirmedPointerChains.PlayerObjectChain(11);

        Assert.Equal(ConfirmedPointerChains.PlayerHolderInstancePointerAddress, first.RootPointerAddress);
        Assert.Equal(0, first.Offsets[1]);
        Assert.Equal(44, last.Offsets[1]);
        Assert.Equal(first.Offsets[0], last.Offsets[0]);
    }
}
