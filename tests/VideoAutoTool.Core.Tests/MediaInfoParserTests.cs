using VideoAutoTool.Core.Ffmpeg;

namespace VideoAutoTool.Core.Tests;

public class MediaInfoParserTests
{
    [Fact]
    public void Parse_AudioVideo_ReturnsExpectedFields()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "h264", "width": 1920, "height": 1080, "pix_fmt": "yuv420p", "avg_frame_rate": "30/1" },
                { "codec_type": "audio", "codec_name": "aac", "duration": "14.000000" }
              ],
              "format": { "duration": "14.000000" }
            }
            """;

        var info = MediaInfoParser.Parse("test.mp4", json);
        Assert.True(info.HasAudio);
        Assert.True(info.HasVideo);
        Assert.Equal(14.0, info.AudioDurationSeconds);
        Assert.False(info.HasAlpha);
    }

    [Fact]
    public void Parse_AlphaPixelFormat_SetsHasAlpha()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "codec_name": "qtrle", "width": 640, "height": 160, "pix_fmt": "argb", "avg_frame_rate": "60/1" }
              ],
              "format": { "duration": "2.500000" }
            }
            """;

        var info = MediaInfoParser.Parse("wave.mov", json);
        Assert.True(info.HasAlpha);
    }
}
