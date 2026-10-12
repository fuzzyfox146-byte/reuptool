using VideoAutoTool.Core.Ffmpeg;
using VideoAutoTool.Core.FrameBatch;

namespace FrameRender;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            FrameStaging.AppFolder = FrameEdition.DataFolder;
            var paths = FfmpegLocator.Locate(null);
            var engine = new FrameBatchEngine(new FfmpegCommandRunner(new FfmpegRunner(paths)), paths.FfprobePath);
            Application.Run(new MainForm(engine, paths.FfmpegPath));
        }
        catch (FfmpegNotFoundException)
        {
            MessageBox.Show(
                "Không thấy ffmpeg.exe và ffprobe.exe trong PATH.",
                FrameEdition.Title,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
