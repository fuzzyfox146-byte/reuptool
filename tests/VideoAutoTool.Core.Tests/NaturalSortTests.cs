using VideoAutoTool.Core.Scanning;

namespace VideoAutoTool.Core.Tests;

public class NaturalSortTests
{
    [Fact]
    public void Compare_OrdersNumbersNaturally()
    {
        Assert.True(NaturalSort.Compare("2", "10") < 0);
        Assert.True(NaturalSort.Compare("001", "002") < 0);
    }

    [Fact]
    public void NumberExtractor_ParsesLeadingNumber()
    {
        Assert.Equal(1, NumberExtractor.Extract("001 🔴 Title.mp4", @"^\s*(\d+)"));
        Assert.Null(NumberExtractor.Extract("no-number.mp4", @"^\s*(\d+)"));
    }
}
