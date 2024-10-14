using System;
using System.IO;
using System.Windows;

namespace VideoEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            string exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string ffmpegPath = Path.Combine(exeDirectory, "ffmpeg.exe");
            string ffprobePath = Path.Combine(exeDirectory, "ffprobe.exe");

            if (!File.Exists(ffmpegPath) || !File.Exists(ffprobePath))
            {
                MessageBox.Show("Please install FFmpeg and place the executable files ffmpeg.exe and ffprobe.exe in the same folder where this program is located.", "FFmpeg not found", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Shutdown();
            }
        }
    }
}
