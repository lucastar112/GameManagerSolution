using System.Diagnostics;
using GameManager.Dolphin;
using Xunit;

namespace GameManager.Dolphin.Tests;

public sealed class PlatformContractTests
{
    [Fact]
    public void ProcessMemoryAccess_ExplicitlyRejectsNonWindowsPlatforms()
    {
        if (OperatingSystem.IsWindows())
            return;

        var process = Process.GetCurrentProcess();
        try
        {
            var exception = Assert.Throws<PlatformNotSupportedException>(() => new DolphinConnection(process));
            Assert.Contains("Windows", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            process.Dispose();
        }
    }
}
