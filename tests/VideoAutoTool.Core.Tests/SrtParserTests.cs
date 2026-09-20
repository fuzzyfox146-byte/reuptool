using VideoAutoTool.Core.Subtitles;

namespace VideoAutoTool.Core.Tests;

public class SrtParserTests
{
    [Fact]
    public void Parse_KeepsBracketTags()
    {
        const string srt = """
            1
            00:00:00,320 --> 00:00:04,230
            word, you must grasp [music] that heaven

            """;

        var cues = SrtParser.Parse(srt);
        Assert.Single(cues);
        Assert.Contains("[music]", cues[0].Text);
    }

    [Fact]
    public void Parse_StripsItalicAndBraceTags()
    {
        const string srt = """
            1
            00:00:00,000 --> 00:00:02,000
            <i>Hello</i> {\\an8} world

            """;

        var cues = SrtParser.Parse(srt);
        Assert.Equal("Hello world", cues[0].Text);
    }
}
