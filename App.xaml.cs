using FFMpegCore;
using System;
using System.IO;
using System.Windows;

namespace Trimly
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
                MessageBoxResult result = MessageBox.Show(
                    "Please install ffmpeg.exe and ffprobe.exe and place them in the same folder where this program is located. \n\nPress \"OK\" to open link.",
                    "FFmpeg not found",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Exclamation,
                    MessageBoxResult.OK,
                    MessageBoxOptions.DefaultDesktopOnly
                );

                if (result == MessageBoxResult.OK)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "https://www.gyan.dev/ffmpeg/builds/",
                        UseShellExecute = true
                    });
                }

                Shutdown();
            }
        }
    }
}
