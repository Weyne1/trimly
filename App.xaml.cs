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
                MessageBox.Show("Пожалуйста, установите FFmpeg и поместите исполняемые файлы ffmpeg.exe и ffprobe.exe в ту же папку, где находится эта программа.", "FFmpeg не найден", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                Shutdown();
            }
        }
    }
}
