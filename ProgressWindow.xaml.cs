using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Trimly
{
    public partial class ProgressWindow : Window
    {
        private MainWindow mainWindow;
        public ProgressWindow()
        {
            InitializeComponent();
        }

        public void ChangeTitle(string title) => Title = title;
        public void ChangeVideoTitle(string videoTitle)
        {
            VideoTitle.Text = videoTitle;
            mainWindow.ChangeRenderText("[...%] " + videoTitle);
        }

        public async void UpdateProgress(double progress)
        {
            await Task.Run(() => Dispatcher.Invoke(() =>
            {
                double currentProgress = ProgressBar.Value;

                if (progress > currentProgress)
                {
                    AnimateProgressBar(currentProgress, progress, TimeSpan.FromSeconds(0.5));
                }
                else
                {
                    AnimateProgressBar(currentProgress, progress, TimeSpan.FromSeconds(0));
                }
                
                AnimateText(currentProgress, progress, TimeSpan.FromSeconds(0.5));
            }));
        }

        private void AnimateProgressBar(double fromValue, double toValue, TimeSpan duration)
        {
            DoubleAnimation progressBarAnimation = new DoubleAnimation
            {
                From = fromValue,
                To = toValue,
                Duration = duration,
                EasingFunction = new QuadraticEase()
            };

            ProgressBar.BeginAnimation(ProgressBar.ValueProperty, progressBarAnimation);
        }
        private void AnimateText(double fromValue, double toValue, TimeSpan duration)
        {
            DoubleAnimation textAnimation = new DoubleAnimation
            {
                From = fromValue,
                To = toValue,
                Duration = duration,
                EasingFunction = new QuadraticEase()
            };

            textAnimation.CurrentTimeInvalidated += (s, e) =>
            {
                if (s is Clock clock)
                {
                    double currentProgress = ProgressBar.Value;
                    Percent.Text = $"{currentProgress:0.00}%";
                }
            };
            Percent.BeginAnimation(OpacityProperty, textAnimation);
        }

        public void SetWindowParent(MainWindow window) => mainWindow = window;
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            mainWindow.EnableRenderButton();
            mainWindow.ChangeRenderText("[100%] " + VideoTitle.Text);

            Process[] ffmpegProcesses = Process.GetProcessesByName("ffmpeg");

            foreach (Process process in ffmpegProcesses)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();

                    ChangeTitle("Rendering [Cancel]");
                    mainWindow.StopRender();
                    mainWindow.ChangeRenderText("[Canceled] " + VideoTitle.Text);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error when terminating ffmpeg process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
