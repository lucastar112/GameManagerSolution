using gameManager.src.Arguments;
using Xunit;

namespace GameManager.Tests;

public sealed class GameManagerArgumentParserTests
{
    [Fact]
    public void Parse_RequiresExactlyThreeArguments()
    {
        Assert.Throws<ArgumentException>(() => GameManagerArgumentParser.Parse([]));
        Assert.Throws<ArgumentException>(() => GameManagerArgumentParser.Parse(["MTWP01", "dolphin.exe"]));
        Assert.Throws<ArgumentException>(() => GameManagerArgumentParser.Parse(["MTWP01", "dolphin.exe", "game.iso", "extra"]));
    }

    [Fact]
    public void Parse_NormalizesGameIdBeforeCheckingFiles()
    {
        var exception = Assert.Throws<FileNotFoundException>(() =>
            GameManagerArgumentParser.Parse(["mtwp01", "missing-dolphin.exe", "missing-game.iso"]));

        Assert.Equal(Path.GetFullPath("missing-dolphin.exe"), exception.FileName);
    }
}
