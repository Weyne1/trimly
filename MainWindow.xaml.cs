using FFMpegCore;
using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Path = System.IO.Path;

namespace Trimly
{
    public partial class MainWindow : Window
    {
        private ProgressWindow progressWindow;

        private bool isRendering = false;
        private bool isSnapshoting = false;
        private bool isSkipping = false;
        private bool renderShutdown = false;
        private bool isMediaPlaying = false;
        private bool isDragging = false;
        private bool isFullScreen = false;
        private bool playAfterDragging = false;
        private bool mergeAudioTracks = true;
        private bool isAudioOnly = false;
        private bool isReverseCropping = false;
        private bool isMediaElementLoading = false;
        private bool firstVideoLaunch = true;
        
        private byte audioStreamsCount = 0;
        private short bufferFPS = 0;
        private short bufferWidth = 0;
        private short bufferHeight = 0;
        private short bufferSampleRates = 0;
        private short bufferChannels = 0;
        private int bufferVideoBitrate = 0;
        private int bufferAudioBitrate = 0;
        private long globalViideoBitrate = 0;
        private long globalAudioBitrate = 0;

        private float audioVolume = 1.0f;
        private float videoSpeed = 1.0f;
        private float aspectRatio = 1.777f;
        private double frameDuration = 0;
        private double globalFPS = 0;
        private double videoDuration = 0;
        private double zoomInPoint = 0;
        private double zoomOutPoint = 0;

        private string outputType = ".mp4";
        private readonly string windowTitle = "Trimly 1.0.0";

        private WindowState previousWindowState;
        private WindowStyle previousWindowStyle;
        private ResizeMode previousResizeMode;

        #region --WINDOW--

        public MainWindow()
        {
            InitializeComponent();
            ConfigureFFMpeg();
            LoadFromSettings();

            CompositionTarget.Rendering += OnWindowRendering;
            mediaElement.MediaEnded += MediaElement_MediaEnded;

            Title = windowTitle;
            LastRenderedVideoText.Opacity = 0;
        }

        private void OnWindowRendering(object sender, EventArgs e)
        {
            if (!isDragging && mediaElement.Source != null && isMediaPlaying)
            {
                TrimSlider.Value = mediaElement.Position.TotalMilliseconds;
            }
        }
        private async void MainWindow_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.I && mediaElement.HasVideo)
            {
                // Сдвиг второго маркера, если он стоит перед первым или на его месте
                if (TrimSlider.Value >= SecondMarker.Value)
                {
                    SecondMarker.Value = TrimSlider.Maximum;
                }

                FirstMarker.Value = TrimSlider.Value;
            }

            else if (e.Key == Key.O && mediaElement.HasVideo)
            {
                // Сдвиг первого маркера, если он стоит после второго или на его месте
                if (TrimSlider.Value <= FirstMarker.Value)
                {
                    FirstMarker.Value = FirstMarker.Minimum;
                }

                SecondMarker.Value = TrimSlider.Value;
            }

            else if (e.Key == Key.Z && ZoomButton.IsEnabled) SetZoom();

            else if (e.Key == Key.X && ResetZoomButton.IsEnabled) ResetZoom();

            else if ((e.Key == Key.Space || e.Key == Key.K) && mediaElement.HasVideo)
            {
                TrimSlider.Focus();
                ChangeMediaPlayerStatus();
                e.Handled = true;
            }
            
            else if (e.Key == Key.M)
            {
                if (VolumeSlider.Value > 0)
                {
                    AnimateSliderBound(VolumeSlider, Slider.ValueProperty, VolumeSlider.Value, 0);
                }
                else
                {
                    AnimateSliderBound(VolumeSlider, Slider.ValueProperty, VolumeSlider.Value, 100);
                }
            }

            else if (e.Key == Key.R)
            {
                if (ReverseCropCheck.IsEnabled)
                {
                    ReverseCropCheck.IsChecked = !ReverseCropCheck.IsChecked;
                }
            }

            else if ((e.Key == Key.Enter) && TrimSliderText.IsFocused) await EditTrimSliderText();

            else if (e.Key == Key.F11)
            {
                if (!isFullScreen)
                {
                    // Save current window state
                    previousWindowState = WindowState;
                    previousWindowStyle = WindowStyle;
                    previousResizeMode = ResizeMode;

                    // Switch to full screen
                    WindowState = WindowState.Maximized;
                    WindowStyle = WindowStyle.None;
                    ResizeMode = ResizeMode.NoResize;

                    isFullScreen = true;
                }
                else
                {
                    // Restore previous window state
                    WindowState = previousWindowState;
                    WindowStyle = previousWindowStyle;
                    ResizeMode = previousResizeMode;

                    isFullScreen = false;
                }
            }
        }
        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.J && mediaElement.HasVideo)
            {
                if (isSkipping) return;

                isSkipping = true;
                if (TrimSlider.Value <= 5000)
                    TrimSlider.Value = 0;
                else TrimSlider.Value -= 5000;

                mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
                if (!isMediaPlaying)
                {
                    await SetPreviewFrame(80);
                }
                await Task.Delay(100);
                isSkipping = false;
            }

            else if (e.Key == Key.L && mediaElement.HasVideo)
            {
                if (isSkipping) return;

                isSkipping = true;
                if (TrimSlider.Value >= TrimSlider.Maximum - 5000)
                    TrimSlider.Value = TrimSlider.Maximum;
                else TrimSlider.Value += 5000;

                mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
                if (!isMediaPlaying)
                {
                    await SetPreviewFrame(80);
                }
                await Task.Delay(100);
                isSkipping = false;
            }

            if ((e.Key == Key.Left || e.Key == Key.Right) && mediaElement.HasVideo && (TrimSlider.IsFocused || CursorRect.IsFocused))
            {
                if (isSkipping) return;

                isSkipping = true;

                double newValue = TrimSlider.Value;

                if (e.Key == Key.Left)
                    newValue = Math.Max(0, TrimSlider.Value - frameDuration);

                else if (e.Key == Key.Right)
                    newValue = Math.Min(TrimSlider.Maximum, TrimSlider.Value + frameDuration);

                mediaElement.Position = TimeSpan.FromMilliseconds(newValue);
                TrimSlider.Value = newValue;

                if (!isMediaPlaying)
                {
                    await SetPreviewFrame(20);
                }
                else if (isMediaPlaying)
                {
                    ChangeMediaPlayerStatus();
                }

                await Task.Delay(50);
                isSkipping = false;
            }
        }
        private void MediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            mediaElement.Stop();
            TrimSlider.Value = TrimSlider.Minimum;
        }
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Получаем новую ширину videoWidthRow
            double newWidth = videoWidthRow.ActualWidth;
            double newHeight = newWidth / 1.777f;
            videoHeightRow.Height = new GridLength(newHeight);

            // Рассчитываем общую высоту окна
            double totalOtherRowsHeight = 0;
            foreach (var row in MainGrid.RowDefinitions)
            {
                if (row != videoHeightRow)
                {
                    totalOtherRowsHeight += row.ActualHeight;
                }
            }

            MinHeight = totalOtherRowsHeight + 33 + newHeight;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            var settings = AppSettings.Load();

            // Общее
            settings.Path = OutputFilePath.Text;
            settings.Codec = (byte)CodecComboBox.SelectedIndex;
            settings.Type = (byte)TypeComboBox.SelectedIndex;

            // Видео
            settings.Width = short.Parse(WidthInput.Text);
            settings.Height = short.Parse(HeightInput.Text);
            settings.FPS = short.Parse(FPSInput.Text);
            settings.VideoBitrate = int.Parse(VideoBitrateInput.Text);
            settings.AutoMetaSet = (bool)MatchSourceCheck.IsChecked;

            // Аудио
            settings.AudioBitrate = int.Parse(AudioBitrateInput.Text);
            settings.Channels = (byte)ChannelsComboBox.SelectedIndex;
            settings.SampleRates = (byte)CodecComboBox.SelectedIndex;
            settings.MergeAudioTracks = (bool)MergeAudioTracksCheck.IsChecked;

            // Пресеты
            settings.BufferFPS = bufferFPS;
            settings.BufferWidth = bufferWidth;
            settings.BufferHeight = bufferHeight;
            settings.BufferVideoBitrate = bufferVideoBitrate;
            settings.BufferAudioBitrate = bufferAudioBitrate;
            settings.BufferSampleRates = bufferSampleRates;
            settings.BufferChannels = bufferChannels;

            // Окно
            if (!isFullScreen)
            {
                settings.WindowWidth = (short)Width;
                settings.WindowHeight = (short)Height;
            }

            settings.Save();
        }

        #endregion


        #region --MEDIA PLAYER--

        private void VideoDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    string fileExtension = Path.GetExtension(filePath).ToLower();
                    string[] allowedExtensions = { ".mp4", ".mkv", ".avi", ".mov" };

                    if (allowedExtensions.Contains(fileExtension))
                    {
                        InputFilePath.Text = filePath;
                        LoadVideo(filePath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text) && !string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }
        private async void LoadVideo(string videoPath)
        {
            isMediaElementLoading = true;

            if (mediaElement.Source != null)
            {
                mediaElement.Stop();
                if (isMediaPlaying) isMediaPlaying = false;
                mediaElement.Close();
            }

            mediaElement.Source = new Uri($"{videoPath}", UriKind.RelativeOrAbsolute);

            if (firstVideoLaunch)
            {
                StrokeRectangle.Opacity = 0;
                DragAndDropImage.Opacity = 0;
                CursorRect.Cursor = Cursors.Hand;
                PlayImage.Opacity = 1;
                ReverseCropCheck.IsEnabled = true;
                firstVideoLaunch = false;
            }
            else
            {
                ClearSliderAnimations();
            }

            SetVideoDimensions(videoPath);
            ResetZoom(false);

            isMediaElementLoading = false;

            await Task.Delay(30);
            ChangeMediaPlayerStatus();

            await CalculateEstimatedSize();
            await CalculateDuration();
        }

        private async Task SetPreviewFrame(short sleepTime)
        {
            isSnapshoting = true;

            if (isMediaPlaying && !playAfterDragging)
            {
                ChangeMediaPlayerStatus();
                playAfterDragging = true;
            }

            await Task.Run(async () =>
            {
                await Dispatcher.InvokeAsync(() => {
                    mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
                    mediaElement.Play();
                });
                await Task.Delay(sleepTime);
                await Dispatcher.InvokeAsync(() => mediaElement.Pause());
            });

            isSnapshoting = false;
        }

        private void MediaElement_MouseDown(object sender, MouseButtonEventArgs e) => ChangeMediaPlayerStatus();
        private void ChangeMediaPlayerStatus(bool showAnimation = true)
        {
            if (isMediaElementLoading)
            {
                return;
            }
            else if (isMediaPlaying)
            {
                isMediaPlaying = false;
                mediaElement.Pause();
                if (showAnimation) ApplyStopAnimation();
            }
            else if (mediaElement.Source != null && !isRendering)
            {
                isMediaPlaying = true;
                mediaElement.Play();
                if (showAnimation) ApplyPlayAnimation();
            }
        }
        private void ApplyPlayAnimation()
        {
            ScaleTransform scaleTransform = PlayImage.RenderTransform as ScaleTransform;

            DoubleAnimation bounce = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuinticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
        }
        private void ApplyStopAnimation()
        {
            ScaleTransform scaleTransform = PlayImage.RenderTransform as ScaleTransform;

            DoubleAnimation bounce = new DoubleAnimation
            {
                From = 0.5,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new BackEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, bounce);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, bounce);
        }
        private async Task CalculateDuration()
        {
            try
            {
                await Task.Run(() =>
                {
                    // Расчет продолжительности с учетом указанной скорости и метода обрезки видео
                    TimeSpan originalDuration = TimeSpan.FromMilliseconds(Dispatcher.Invoke(() => SecondMarker.Value - FirstMarker.Value));

                    if (isReverseCropping)
                        originalDuration = TimeSpan.FromMilliseconds(videoDuration - (Dispatcher.Invoke(() => SecondMarker.Value - FirstMarker.Value)));

                    TimeSpan adjustedDuration = TimeSpan.FromTicks((long)(originalDuration.Ticks / videoSpeed));
                    string formattedDuration = adjustedDuration.ToString(@"h\:mm\:ss");

                    Dispatcher.Invoke(() => DurationText.Text = $"Total: {formattedDuration}");
                });
            }
            catch
            {
                Dispatcher.Invoke(() => DurationText.Text = $"Total: error");
            }
        }
        private async Task CalculateEstimatedSize()
        {
            try
            {
                if (!string.IsNullOrEmpty(InputFilePath.Text) &&
                    !string.IsNullOrEmpty(VideoBitrateInput.Text) &&
                    !string.IsNullOrEmpty(AudioBitrateInput.Text))
                {
                    int audioBitrate = ParseBitrate(AudioBitrateInput.Text, globalAudioBitrate);

                    TimeSpan duration = TimeSpan.FromMilliseconds(SecondMarker.Value - FirstMarker.Value);

                    if (isReverseCropping)
                        duration = TimeSpan.FromMilliseconds(videoDuration - (SecondMarker.Value - FirstMarker.Value));

                    TimeSpan adjustedDuration = TimeSpan.FromTicks((long)(duration.Ticks / videoSpeed));
                    int newDuration = (int)adjustedDuration.TotalSeconds;

                    long sizeInBytes = 0;

                    if (isAudioOnly)
                    {
                        sizeInBytes = audioBitrate * newDuration;
                    }
                    else
                    {
                        int bitrate = ParseBitrate(VideoBitrateInput.Text, globalViideoBitrate);
                        sizeInBytes = (bitrate + audioBitrate) * newDuration;
                    }

                    double sizeInMegabytes = (double)sizeInBytes / (8 * 1024 * 1024) * 1000;

                    await Dispatcher.InvokeAsync(() =>
                    {
                        EstimatedSizeText.Text = $" · {sizeInMegabytes:F2} Mb";
                    });
                }
            }
            catch (Exception ex)
            {
                EstimatedSizeText.Text = " · ? Mb";
                MessageBox.Show($"Could not calculate approximate video size: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private int ParseBitrate(string bitrateInput, long globalBitrate)
        {
            return int.TryParse(bitrateInput, out int bitrate) && bitrate > 0 ? bitrate : (int)globalBitrate / 1000;
        }
        private async void TrimSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!(sender is Slider slider)) return;

            const short tolerance = 30;
            var sliderValue = slider.Value;
            bool isWithinTolerance(double val1, double val2) => Math.Abs(val1 - val2) <= tolerance;

            if (isMediaPlaying && !isDragging)
            {
                bool shouldPause = false;
                double newPosition = 0;

                switch (isReverseCropping)
                {
                    case false when isWithinTolerance(sliderValue, SecondMarker.Value) || isWithinTolerance(sliderValue, zoomOutPoint):
                        newPosition = FirstMarker.Value;
                        shouldPause = true;
                        break;

                    case true when isWithinTolerance(sliderValue, FirstMarker.Value):
                        newPosition = SecondMarker.Value;
                        shouldPause = true;
                        break;

                    case true when isWithinTolerance(sliderValue, zoomOutPoint):
                        newPosition = TrimSlider.Minimum;
                        shouldPause = true;
                        break;
                }

                if (shouldPause)
                {
                    ChangeMediaPlayerStatus(false);
                    mediaElement.Position = TimeSpan.FromMilliseconds(newPosition);
                    slider.Value = newPosition;
                    ChangeMediaPlayerStatus(false);
                }
            }

            // Отображение времени рядом со слайдером
            TimeSpan time = TimeSpan.FromMilliseconds(slider.Value);
            short frames = (short)Math.Round((slider.Value % 1000) / frameDuration);
            TrimSliderText.Text = $"{time:h\\:mm\\:ss\\:}{frames:D2}";

            if (!isSnapshoting && isDragging && PreviewsOnDraggingCheck.IsChecked == true)
            {
                await SetPreviewFrame(20);
            }
        }
        private void TrimSlider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;

            if (sender is Slider slider)
            {
                Point position = e.GetPosition(slider);
                double relativePosition = position.X / slider.ActualWidth;
                double newValue = relativePosition * (slider.Maximum - slider.Minimum) + slider.Minimum;
                slider.Value = newValue;
            }
        }
        private async void TrimSlider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;

            mediaElement.Position = TimeSpan.FromMilliseconds(TrimSlider.Value);
            mediaElement.IsMuted = false;

            if (playAfterDragging)
            {
                await Task.Delay(150);
                ChangeMediaPlayerStatus();
                playAfterDragging = false;
            }

            if (!(bool)PreviewsOnDraggingCheck.IsChecked && !isMediaPlaying)
            {
                await SetPreviewFrame(20);
            }
        }
        private void FirstMarker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                if (slider.Template.FindName("PART_Track", slider) is Track track &&
                    track.Thumb is Thumb thumb)
                {
                    thumb.PreviewMouseRightButtonDown += FirstMarker_OnMouseRightButtonDown;
                }
            }
        }
        private async void FirstMarker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SecondMarker.Value - FirstMarker.Value <= 100)
            {
                FirstMarker.Value = SecondMarker.Value - 100;
            }

            if (isReverseCropping)
            {
                if (FirstMarker.Value - TrimSlider.Minimum < 300)
                {
                    FirstMarker.Value = FirstMarker.Minimum + 300;
                }
            }

            ZoomButtonCheck();
            await CalculateEstimatedSize();
            await CalculateDuration();
        }
        private async void FirstMarker_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMediaPlaying)
            {
                await SetPreviewFrame(40);
            }
            TrimSlider.Value = FirstMarker.Value;
            mediaElement.Position = TimeSpan.FromMilliseconds(FirstMarker.Value);
        }
        private void SecondMarker_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Slider slider)
            {
                if (slider.Template.FindName("PART_Track", slider) is Track track &&
                    track.Thumb is Thumb thumb)
                {
                    thumb.PreviewMouseRightButtonDown += SecondMarker_OnMouseRightButtonDown;
                }
            }
        }
        private async void SecondMarker_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SecondMarker.Value - FirstMarker.Value <= 100)
            {
                SecondMarker.Value = FirstMarker.Value + 100;
            }

            if (isReverseCropping)
            {
                if (TrimSlider.Maximum - SecondMarker.Value < 300)
                {
                    SecondMarker.Value = SecondMarker.Maximum - 300;
                }
            }

            ZoomButtonCheck();
            await CalculateEstimatedSize();
            await CalculateDuration();
        }
        private async void SecondMarker_OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!isMediaPlaying)
            {
                await SetPreviewFrame(40);
            }
            TrimSlider.Value = SecondMarker.Value;
            mediaElement.Position = TimeSpan.FromMilliseconds(SecondMarker.Value);
        }
        private void ZoomButton_Click(object sender, RoutedEventArgs e) => SetZoom();
        private void ResetZoomButton_Click(object sender, RoutedEventArgs e) => ResetZoom();
        private void SetZoom()
        {
            zoomInPoint = FirstMarker.Value;
            zoomOutPoint = SecondMarker.Value;

            if (isReverseCropping)
            {
                zoomInPoint -= 300;
                zoomOutPoint += 300;
            }

            AnimateSliderBound(TrimSlider, Slider.MinimumProperty, TrimSlider.Minimum, zoomInPoint);
            AnimateSliderBound(TrimSlider, Slider.MaximumProperty, TrimSlider.Maximum, zoomOutPoint);

            AnimateSliderBound(FirstMarker, Slider.MinimumProperty, FirstMarker.Minimum, zoomInPoint);
            AnimateSliderBound(FirstMarker, Slider.MaximumProperty, FirstMarker.Maximum, zoomOutPoint);

            AnimateSliderBound(SecondMarker, Slider.MinimumProperty, SecondMarker.Minimum, zoomInPoint);
            AnimateSliderBound(SecondMarker, Slider.MaximumProperty, SecondMarker.Maximum, zoomOutPoint);
            

            if (mediaElement.Position < TimeSpan.FromMilliseconds(zoomInPoint) ||
                mediaElement.Position >= TimeSpan.FromMilliseconds(zoomOutPoint))
            {
                mediaElement.Position = TimeSpan.FromMilliseconds(zoomInPoint);
                TrimSlider.Value = zoomInPoint;
            }

            string startPoint = TimeSpan.FromMilliseconds(zoomInPoint).ToString(@"h\:mm\:ss");
            string endPoint = TimeSpan.FromMilliseconds(zoomOutPoint).ToString(@"h\:mm\:ss");

            ZoomStatusText.Text = $"Zoom from {startPoint} to {endPoint}.";
            ZoomButton.IsEnabled = false;
            ResetZoomButton.IsEnabled = true;
        }
        private void ResetZoom(bool showAnimation = true)
        {
            zoomInPoint = 0;
            zoomOutPoint = videoDuration;

            if (showAnimation)
            {
                AnimateSliderBound(TrimSlider, Slider.MinimumProperty, TrimSlider.Minimum, 0);
                AnimateSliderBound(TrimSlider, Slider.MaximumProperty, TrimSlider.Maximum, videoDuration);

                AnimateSliderBound(FirstMarker, Slider.MinimumProperty, FirstMarker.Minimum, 0);
                AnimateSliderBound(FirstMarker, Slider.MaximumProperty, FirstMarker.Maximum, videoDuration);

                AnimateSliderBound(SecondMarker, Slider.MinimumProperty, SecondMarker.Minimum, 0);
                AnimateSliderBound(SecondMarker, Slider.MaximumProperty, SecondMarker.Maximum, videoDuration);
            }
            else
            {
                TrimSlider.Minimum = 0;
                TrimSlider.Maximum = videoDuration;
                TrimSlider.Value = 0;

                FirstMarker.Minimum = 0;
                FirstMarker.Maximum = videoDuration;
                FirstMarker.Value = 0;

                SecondMarker.Maximum = videoDuration;
                SecondMarker.Minimum = 0;
                SecondMarker.Value = videoDuration;

                ZoomButtonCheck();
            }
            
            ResetZoomButton.IsEnabled = false;
            ZoomStatusText.Text = "Timeline without zoom.";
        }
        private void AnimateSliderBound(Slider slider, DependencyProperty property, double fromValue, double toValue)
        {
            var animation = new DoubleAnimation
            {
                From = fromValue,
                To = toValue,
                Duration = TimeSpan.FromSeconds(0.5),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            animation.Completed += (s, e) => ZoomButtonCheck();

            slider.BeginAnimation(property, animation);
        }
        private void ClearSliderAnimations()
        {
            TrimSlider.BeginAnimation(Slider.MinimumProperty, null);
            TrimSlider.BeginAnimation(Slider.MaximumProperty, null);
            FirstMarker.BeginAnimation(Slider.MinimumProperty, null);
            FirstMarker.BeginAnimation(Slider.MaximumProperty, null);
            SecondMarker.BeginAnimation(Slider.MinimumProperty, null);
            SecondMarker.BeginAnimation(Slider.MaximumProperty, null);
        }
        private void ZoomButtonCheck()
        {
            if (!IsInitialized) return;

            short delta = 0;

            if (isReverseCropping)
            {
                delta = 300;
            }

            if ((FirstMarker.Value == FirstMarker.Minimum + delta && 
                SecondMarker.Value == SecondMarker.Maximum - delta) ||
                SecondMarker.Value - FirstMarker.Value < 3000)
                ZoomButton.IsEnabled = false;
            else
                ZoomButton.IsEnabled = true;
        }

        #endregion


        #region --PRESETS CONTROL--

        private void LoadFromSettings()
        {
            var settings = AppSettings.Load();

            // Комбо боксы
            CodecComboBox.SelectedIndex = settings.Codec;
            TypeComboBox.SelectedIndex = settings.Type;

            // Выходной путь
            if (!string.IsNullOrEmpty(settings.Path))
            {
                OutputFilePath.Text = settings.Path;
                OutputFilePathText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C8C8"));
            }

            // Параметры видео
            if (settings.Width > 0)
                WidthInput.Text = settings.Width.ToString();
            if (settings.Height > 0)
                HeightInput.Text = settings.Height.ToString();
            if (settings.FPS > 0 && settings.FPS.ToString().Length < 4)
                FPSInput.Text = settings.FPS.ToString();
            if (settings.VideoBitrate > 0 && settings.VideoBitrate.ToString().Length < 7)
                VideoBitrateInput.Text = settings.VideoBitrate.ToString();

            MatchSourceCheck.IsChecked = settings.AutoMetaSet;

            // Параметры аудио
            if (settings.AudioBitrate > 0 && settings.AudioBitrate.ToString().Length < 4)
                AudioBitrateInput.Text = settings.AudioBitrate.ToString();

            SampleRatesComboBox.SelectedIndex = settings.SampleRates;
            ChannelsComboBox.SelectedIndex = settings.Channels;
            MergeAudioTracksCheck.IsChecked = settings.MergeAudioTracks;

            // Пресеты
            if (settings.BufferFPS > 0 && settings.BufferFPS.ToString().Length < 4)
                bufferFPS = settings.BufferFPS;
            if (settings.BufferWidth > 0 && settings.BufferWidth.ToString().Length < 4)
                bufferWidth = settings.BufferWidth;
            if (settings.BufferHeight > 0 && settings.BufferHeight.ToString().Length < 4)
                bufferHeight = settings.BufferHeight;
            if (settings.BufferVideoBitrate > 0 && settings.BufferVideoBitrate.ToString().Length < 7)
                bufferVideoBitrate = settings.BufferVideoBitrate;
            if (settings.BufferAudioBitrate > 0 && settings.BufferAudioBitrate.ToString().Length < 4)
                bufferAudioBitrate = settings.BufferAudioBitrate;

            bufferSampleRates = settings.BufferSampleRates;
            bufferChannels = settings.BufferChannels;

            // Окно
            if (settings.WindowWidth != 0 && settings.WindowHeight != 0)
            {
                Width = settings.WindowWidth;
                Height = settings.WindowHeight;
            }

            settings.Save();
        }
        private void SaveVideoParamsButton_Click(object sender, RoutedEventArgs e)
        {
            bufferFPS = short.Parse(FPSInput.Text.ToString());
            bufferVideoBitrate = int.Parse(VideoBitrateInput.Text.ToString());
            bufferWidth = short.Parse(WidthInput.Text.ToString());
            bufferHeight = short.Parse(HeightInput.Text.ToString());
        }
        private void PasteVideoParamsButton_Click(object sender, RoutedEventArgs e)
        {
            if (bufferFPS > 0)
                FPSInput.Text = bufferFPS.ToString();
            if (bufferVideoBitrate > 0)
                VideoBitrateInput.Text = bufferVideoBitrate.ToString();
            if (bufferWidth > 0)
                WidthInput.Text = bufferWidth.ToString();
            if (bufferHeight > 0)
                HeightInput.Text = bufferHeight.ToString();
        }
        private void SaveAudioParamsButton_Click(object sender, RoutedEventArgs e)
        {
            bufferAudioBitrate = int.Parse(AudioBitrateInput.Text.ToString());
            bufferSampleRates = (short)SampleRatesComboBox.SelectedIndex;
            bufferChannels = (short)ChannelsComboBox.SelectedIndex;
        }
        private void PasteAudioParamsButton_Click(object sender, RoutedEventArgs e)
        {
            if (bufferAudioBitrate > 0)
                AudioBitrateInput.Text = bufferAudioBitrate.ToString();
            SampleRatesComboBox.SelectedIndex = bufferSampleRates;
            ChannelsComboBox.SelectedIndex = bufferChannels;
        }

        #endregion


        #region --OTHER WINDOW ELEMENTS--

        private void SelectInputFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov"
            };
            if (openFileDialog.ShowDialog() == true)
            {
                InputFilePath.Text = openFileDialog.FileName;
                LoadVideo(openFileDialog.FileName);
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text) && !string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }
        private void SelectOutputFile_Click(object sender, RoutedEventArgs e)
        {
            string selectedOutputType = ((ComboBoxItem)TypeComboBox.SelectedItem).Content.ToString();
            string filename;
            string filter;

            switch (selectedOutputType)
            {
                case "MOV": filter = "MOV Files|*.mov"; break;
                case "AVI": filter = "AVI Files|*.avi"; break;
                case "MKV": filter = "MKV Files|*.mkv"; break;
                case "MP3 - Audio": filter = "MP3 Files|*.mp3"; break;
                case "AAC - Audio": filter = "AAC Files|*.aac"; break;
                default: filter = "MP4 Files|*.mp4"; break;
            }

            if (!string.IsNullOrEmpty(OutputFilePath.Text))
                filename = Path.GetFileNameWithoutExtension(OutputFilePath.Text);
            else
                filename = Path.GetFileNameWithoutExtension(InputFilePath.Text);

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = filter,
                FileName = filename
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                OutputFilePath.Text = saveFileDialog.FileName;
            }

            if (string.IsNullOrEmpty(InputFilePath.Text))
            {
                OutputFilePathText.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC8C8C8"));
            }
            else if (!string.IsNullOrEmpty(OutputFilePath.Text) && !isRendering)
            {
                RenderButton.IsEnabled = true;
            }
        }
        private void WidthTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox) textBox.CaretIndex = textBox.Text.Length;

            if (int.TryParse(WidthInput.Text, out int wigth))
            {
                if (wigth == 0)
                    WidthInput.Text = "1";
                if (wigth > 7680) WidthInput.Text = "7680";
                if (LinkCheck != null && LinkCheck.IsChecked == true && wigth > 1)
                {
                    HeightInput.Text = ((int)(wigth / aspectRatio)).ToString();
                }
            }
            else WidthInput.Text = Regex.Replace(WidthInput.Text, "[^0-9]", "");
        }
        private void WidthTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(WidthInput.Text, out int wigth))
            {
                if (wigth < 150) WidthInput.Text = "150";
                else if (wigth % 2 != 0) WidthInput.Text = $"{wigth + 1}";

                if (LinkCheck.IsChecked != null && LinkCheck.IsChecked == true
                    && int.TryParse(HeightInput.Text, out int heigth))
                {
                    if (heigth % 2 != 0) HeightInput.Text = $"{heigth + 1}";
                }
            }
        }
        private void HeightTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox) textBox.CaretIndex = textBox.Text.Length;

            if (int.TryParse(HeightInput.Text, out int heigth))
            {
                if (HeightInput.Text.StartsWith("0"))
                    HeightInput.Text = HeightInput.Text.Substring(1);
                if (heigth > 4320) HeightInput.Text = "4320";
            }
            else HeightInput.Text = Regex.Replace(HeightInput.Text, "[^0-9]", "");
        }
        private void HeightTextLostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(HeightInput.Text, out int heigth))
            {
                if (heigth % 2 != 0) HeightInput.Text = $"{heigth + 1}";
            }
        }
        private void LinkCheck_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.IsChecked == true)
                HeightInput.IsEnabled = false;
            else
                HeightInput.IsEnabled = true;
        }
        private void FPSInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            FPSInput.Text = Regex.Replace(FPSInput.Text, "[^0-9]", "");
        }
        private void FPSInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(FPSInput.Text) || FPSInput.Text == "0") FPSInput.Text = "1";
        }
        private async void VideoBitrateInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            VideoBitrateInput.Text = Regex.Replace(VideoBitrateInput.Text, "[^0-9]", "");
            await CalculateEstimatedSize();
        }
        private void VideoBitrateInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (VideoBitrateInput.Text == string.Empty) VideoBitrateInput.Text = "0";
        }
        private async void AudioBitrateInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            AudioBitrateInput.Text = Regex.Replace(AudioBitrateInput.Text, "[^0-9]", "");
            await CalculateEstimatedSize();
        }
        private void AudioBitrateInput_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AudioBitrateInput.Text == string.Empty) AudioBitrateInput.Text = "1";
        }
        private async void TypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            string selectedOutputType = ((ComboBoxItem)TypeComboBox.SelectedItem).Content.ToString();

            string fileType;
            switch (selectedOutputType)
            {
                case "MOV": fileType = ".mov"; break;
                case "AVI": fileType = ".avi"; break;
                case "MKV": fileType = ".mkv"; break;
                case "MP3 - Audio": fileType = ".mp3"; break;
                case "AAC - Audio": fileType = ".aac"; break;
                default: fileType = ".mp4"; break;
            }

            if ((fileType == ".mp3" || fileType == ".aac") && IsInitialized)
            {
                isAudioOnly = true;
                MergeAudioTracksCheck.IsChecked = true;
                MergeAudioTracksCheck.IsEnabled = false;
                WidthInput.IsEnabled = false;
                HeightInput.IsEnabled = false;
                LinkCheck.IsEnabled = false;
                FPSInput.IsEnabled = false;
                VideoBitrateInput.IsEnabled = false;
                CodecComboBox.IsEnabled = false;
            }
            else if (IsInitialized)
            {
                isAudioOnly = false;
                MergeAudioTracksCheck.IsEnabled = true;
                WidthInput.IsEnabled = true;
                HeightInput.IsEnabled = !(bool)LinkCheck.IsChecked;
                LinkCheck.IsEnabled = true;
                FPSInput.IsEnabled = true;
                VideoBitrateInput.IsEnabled = true;
                CodecComboBox.IsEnabled = true;
            }

            outputType = fileType;

            if (!string.IsNullOrEmpty(OutputFilePath.Text))
            {
                string fileName = Path.GetFileNameWithoutExtension(OutputFilePath.Text) + outputType;
                OutputFilePath.Text = Path.GetDirectoryName(OutputFilePath.Text) + "\\" + fileName;
            }

            await CalculateEstimatedSize();
        }
        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsInitialized)
            {
                VolumeText.Text = ((ushort)VolumeSlider.Value).ToString() + "%";
                audioVolume = (float)VolumeSlider.Value / 100;
                mediaElement.Volume = audioVolume / 2;
            }
        }
        private async void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsInitialized)
            {
                SpeedText.Text = Math.Round(SpeedSlider.Value, 1).ToString().Replace(',', '.') + "x";
                videoSpeed = (float)Math.Round(SpeedSlider.Value, 1);
                mediaElement.SpeedRatio = videoSpeed;

                await CalculateDuration();
                await CalculateEstimatedSize();
            }
        }
        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Trim video: [I], [O]\n" +
                "Playback control: [SPACE], [K]\n" +
                "Zoom/Reset zoom: [Z], [X]\n" +
                "5 sec. rewind: [J], [L]\n" +
                "Frame by frame rewind: [←], [→]\n" +
                "Mute audio: [M]\n" + 
                "Change cut mode: [R]", "Hotkeys", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void MergeAudioTracksCheck_Checked(object sender, RoutedEventArgs e)
        {
            mergeAudioTracks = (bool)MergeAudioTracksCheck.IsChecked;
        }
        private void MatchSourceCheck_Checked(object sender, RoutedEventArgs e)
        {
            if (IsInitialized && mediaElement.HasVideo)
            {
                MatchSource(InputFilePath.Text);
            }
        }
        private async void ReverseCropCheck_Click(object sender, RoutedEventArgs e)
        {
            isReverseCropping = (bool)ReverseCropCheck.IsChecked;

            if (FirstMarker.Template.FindName("PART_Track", FirstMarker) is Track track &&
                track.Thumb is Thumb thumb)
            {
                if (thumb.Template.FindName("Image", thumb) is Image image)
                {
                    image.Source = new BitmapImage(new Uri(isReverseCropping ? "pack://application:,,,/res/images/first-marker-red.png" : "pack://application:,,,/res/images/first-marker.png"));
                }
            }

            if (SecondMarker.Template.FindName("PART_Track", SecondMarker) is Track track2 &&
                track2.Thumb is Thumb thumb2)
            {
                if (thumb2.Template.FindName("Image", thumb2) is Image image2)
                {
                    image2.Source = new BitmapImage(new Uri(isReverseCropping ? "pack://application:,,,/res/images/second-marker-red.png" : "pack://application:,,,/res/images/second-marker.png"));
                }
            }

            // Отступы маркеров от краёв при включении реверсивной обрезки
            if (isReverseCropping)
            {
                double indent = (FirstMarker.Maximum - FirstMarker.Minimum) / 5;

                RenderButton.Content = "Render outside selection";

                if (FirstMarker.Value - TrimSlider.Minimum < 300)
                {
                    FirstMarker.Value = FirstMarker.Minimum + indent;
                }
                if (TrimSlider.Maximum - SecondMarker.Value < 300)
                {
                    SecondMarker.Value = SecondMarker.Maximum - indent;
                }
            }
            else
            {
                RenderButton.Content = "Render selection";
            }

            await CalculateEstimatedSize();
            await CalculateDuration();
        }

        private void TrimSliderText_TextChanged(object sender, TextChangedEventArgs e)
        {
            TrimSliderText.Text = Regex.Replace(TrimSliderText.Text, "[^0-9:]", "");
        }
        private async void TrimSliderText_LostFocus(object sender, RoutedEventArgs e) => await EditTrimSliderText();
        private async Task EditTrimSliderText()
        {
            await Task.Run(() => Dispatcher.Invoke(async () =>
            {
                string[] parts = TrimSliderText.Text.Split(':');

                try
                {
                    ushort hours = ushort.Parse(parts[0]);
                    ushort minutes = ushort.Parse(parts[1]);
                    ushort seconds = ushort.Parse(parts[2]);
                    ushort frames = ushort.Parse(parts[3]);

                    // Преобразование времени и кадров в миллисекунды
                    double millisecondsFromFrames = frames * frameDuration;
                    TrimSlider.Value = hours * 3600000 + minutes * 60000 + seconds * 1000 + millisecondsFromFrames;


                    TimeSpan value = TimeSpan.FromMilliseconds(TrimSlider.Value);
                    mediaElement.Position = value;
                    await SetPreviewFrame(50);
                    mediaElement.Position = value;
                }
                catch { }
            }));
        }

        #endregion


        #region --RENDER & FFMPEG--

        private void ConfigureFFMpeg()
        {
            var binaryFolder = AppDomain.CurrentDomain.BaseDirectory;

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = binaryFolder,
            });
        }
        private void SetVideoDimensions(string inputPath)
        {
            try
            {
                var videoInfo = FFProbe.Analyse(inputPath);

                videoDuration = videoInfo.Duration.TotalMilliseconds;
                globalViideoBitrate = videoInfo.PrimaryVideoStream.BitRate;
                globalAudioBitrate = videoInfo.PrimaryAudioStream?.BitRate ?? 0;
                globalFPS = (short)Math.Round(videoInfo.PrimaryVideoStream.FrameRate);
                frameDuration = TimeSpan.FromSeconds(1.0 / videoInfo.PrimaryVideoStream.FrameRate).TotalMilliseconds;
                aspectRatio = Math.Abs(videoInfo.PrimaryVideoStream.Width / (float)videoInfo.PrimaryVideoStream.Height);
                audioStreamsCount = (byte)videoInfo.AudioStreams.Count;

                if (MatchSourceCheck.IsChecked == true)
                {
                    MatchSource(inputPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting video parameters: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            if (!string.IsNullOrEmpty(InputFilePath.Text))
            {
                InputFilePathText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFC8C8C8"));
            }
        }

        private void MatchSource(string inputPath)
        {
            var videoInfo = FFProbe.Analyse(inputPath);

            WidthInput.Text = videoInfo.PrimaryVideoStream.Width.ToString();
            HeightInput.Text = videoInfo.PrimaryVideoStream.Height.ToString();
            FPSInput.Text = globalFPS.ToString();
            VideoBitrateInput.Text = ((int)globalViideoBitrate / 1000).ToString();
            AudioBitrateInput.Text = ((int)globalAudioBitrate / 1000).ToString();

            if (videoInfo.PrimaryAudioStream != null)
            {
                int sampleRates = videoInfo.PrimaryAudioStream.SampleRateHz;

                if (sampleRates > 44100)
                    SampleRatesComboBox.SelectedIndex = 0;
                else if (sampleRates <= 44100)
                    SampleRatesComboBox.SelectedIndex = 1;
                else if (sampleRates <= 32000)
                    SampleRatesComboBox.SelectedIndex = 2;
                else if (sampleRates <= 22050)
                    SampleRatesComboBox.SelectedIndex = 3;
                else if (sampleRates <= 16000)
                    SampleRatesComboBox.SelectedIndex = 4;
                else if (sampleRates <= 11025)
                    SampleRatesComboBox.SelectedIndex = 5;
                else if (sampleRates <= 8000)
                    SampleRatesComboBox.SelectedIndex = 6;

                ChannelsComboBox.SelectedIndex = videoInfo.PrimaryAudioStream.Channels == 2 ? 0 : 1;
            }
        }

        private async void RenderButton_Click(object sender, RoutedEventArgs e) => await LoadToStartRender();
        private async Task LoadToStartRender()
        {
            
            if (!int.TryParse(FPSInput.Text, out int fps) || !int.TryParse(VideoBitrateInput.Text, out int videoBitrate) || !int.TryParse(AudioBitrateInput.Text, out int audioBitrate))
            {
                MessageBox.Show("Please enter correct values ​​for FPS and bitrate.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (globalAudioBitrate == 0 && (outputType == ".mp3" || outputType == ".aac"))
            {
                MessageBox.Show("This video does not contain audio, so it cannot be extracted", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (isAudioOnly && (audioVolume == 0 || audioBitrate == 0))
            {
                MessageBox.Show("Audio bitrate or volume is set to 0.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (InputFilePath.Text == OutputFilePath.Text)
            {
                string path = Path.GetDirectoryName(InputFilePath.Text);
                string newFilename = Path.GetFileNameWithoutExtension(InputFilePath.Text) + "_rendered" + outputType;
                OutputFilePath.Text = Path.Combine(path, newFilename);
            }

            progressWindow = new ProgressWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            progressWindow.Show();

            try
            {
                int width = int.Parse(WidthInput.Text);
                int height = int.Parse(HeightInput.Text);

                byte selectedChannels = (byte)ChannelsComboBox.SelectedIndex;
                byte selectedCodec = (byte)CodecComboBox.SelectedIndex;
                byte selectedSampleRates = (byte)SampleRatesComboBox.SelectedIndex;

                string channels = selectedChannels == 0 ? "stereo" : "mono";
                
                string videoCodec;
                switch (selectedCodec)
                {
                    case 0: videoCodec = "libx264"; break;
                    case 1: videoCodec = "h264_nvenc"; break;
                    case 2: videoCodec = "h264_qsv"; break;
                    case 3: videoCodec = "h264_amf"; break;
                    default: videoCodec = "libx264"; break;
                }

                int sampleRates;
                switch (selectedSampleRates)
                {
                    case 0: sampleRates = 48000; break;
                    case 1: sampleRates = 41000; break;
                    case 2: sampleRates = 32000; break;
                    case 3: sampleRates = 22050; break;
                    case 4: sampleRates = 16000; break;
                    case 5: sampleRates = 11025; break;
                    case 6: sampleRates = 8000; break;
                    default: sampleRates = 41000; break;
                }

                TimeSpan totalTime = TimeSpan.FromMilliseconds(videoDuration);
                TimeSpan trimStart = TimeSpan.FromMilliseconds(FirstMarker.Value);
                TimeSpan trimEnd = TimeSpan.FromMilliseconds(SecondMarker.Value);

                if (isMediaPlaying) ChangeMediaPlayerStatus();

                isRendering = true;
                RenderButton.IsEnabled = false;

                progressWindow.SetWindowParent(this);
                progressWindow.ChangeTitle("Rendering...");
                progressWindow.ChangeVideoTitle(Path.GetFileName(OutputFilePath.Text));

                await StartRender(InputFilePath.Text, OutputFilePath.Text, width, height,
                    fps, videoBitrate, audioBitrate, sampleRates, audioStreamsCount, channels, 
                    totalTime, trimStart, trimEnd, videoCodec);

                await Task.Delay(500);
                isRendering = false;
                progressWindow.ChangeTitle("Rendering [Done]");
                progressWindow.Close();

                PlaySound("Trimly.res.sounds.render_finished.wav");

                // Проверяем, существует ли уже файл с таким именем
                if (File.Exists(OutputFilePath.Text))
                {
                    string path = Path.GetDirectoryName(OutputFilePath.Text);
                    string fileName = Path.GetFileNameWithoutExtension(OutputFilePath.Text);
                    string extension = Path.GetExtension(OutputFilePath.Text);

                    int openBracketIndex = fileName.LastIndexOf(" (");
                    int closeBracketIndex = fileName.LastIndexOf(")");

                    if (openBracketIndex != -1 && closeBracketIndex != -1 && closeBracketIndex > openBracketIndex)
                    {
                        string numberString = fileName.Substring(openBracketIndex + 2, closeBracketIndex - openBracketIndex - 2);
                        if (int.TryParse(numberString, out int number))
                        {
                            fileName = fileName.Substring(0, openBracketIndex) + $" ({number + 1})";
                        }
                    }
                    else fileName += " (1)";
                    OutputFilePath.Text = Path.Combine(path, fileName + extension);
                }
            }
            catch (Exception ex)
            {
                if (!renderShutdown)
                {
                    MessageBox.Show($"Rendering error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    progressWindow.Close();
                    ChangeRenderText("[Error] " + Path.GetFileName(InputFilePath.Text));
                }
                isRendering = false;
                RenderButton.IsEnabled = true;
            }
        }
        private async Task StartRender(string inputPath, string outputPath, int width, int height,
            int fps, int videoBitrate, int audioBitrate, int sampleRates, byte audioStreamCount, string channels,
            TimeSpan totalTime, TimeSpan trimStart, TimeSpan trimEnd, string videoCodec)
        {
            // Рассчитываем продолжительность с учетом скорости
            TimeSpan outputDuration = trimEnd - trimStart;
            TimeSpan adjustedDuration = TimeSpan.FromTicks((long)(outputDuration.Ticks / videoSpeed));

            if (!isReverseCropping)
            {
                await Task.Run(() =>
                {
                    var arguments = FFMpegArguments
                        .FromFileInput(inputPath, true, options => options.Seek(trimStart))
                        .OutputToFile(outputPath, true, options =>
                        {
                            options.UsingMultithreading(true).WithDuration(adjustedDuration);

                            // Видеонастройки
                            if (!isAudioOnly)
                            {
                                ConfigureVideoOptions(options, videoCodec, videoSpeed, fps, width, height, videoBitrate);
                            }

                            // Аудионастройки
                            ConfigureAudioOptions(options, audioStreamCount, audioVolume, audioBitrate, videoSpeed, sampleRates, channels);
                        })
                        .NotifyOnProgress(progress =>
                        {
                            progressWindow.UpdateProgress(progress);
                        }, totalTime)
                        .ProcessSynchronously();
                });
            }
            else
            {
                // Reverse cropping: Рендерим два участка и объединяем их
                string part1Output = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "_temp1" + outputType);
                string part2Output = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + "_temp2" + outputType);
                string fileName = Path.GetFileName(OutputFilePath.Text);

                await Task.Run(() =>
                {
                    // Рендерим первый участок от 0 до trimStart
                    Dispatcher.Invoke(() => progressWindow.ChangeVideoTitle($"{fileName} [step 1/3]"));
                    FFMpegArguments
                        .FromFileInput(inputPath, true)
                        .OutputToFile(part1Output, true, options =>
                        {
                            options.UsingMultithreading(true).WithDuration(trimStart);

                            // Видеонастройки
                            if (!isAudioOnly)
                            {
                                ConfigureVideoOptions(options, videoCodec, videoSpeed, fps, width, height, videoBitrate);
                            }

                            // Аудионастройки
                            ConfigureAudioOptions(options, audioStreamCount, audioVolume, audioBitrate, videoSpeed, sampleRates, channels);
                        })
                        .NotifyOnProgress(progress =>
                        {
                            progressWindow.UpdateProgress(progress);
                        }, totalTime)
                        .ProcessSynchronously();

                    // Рендерим второй участок от trimEnd до конца
                    Dispatcher.Invoke(() => progressWindow.ChangeVideoTitle($"{fileName} [step 2/3]"));
                    FFMpegArguments
                        .FromFileInput(inputPath, true, options => options.Seek(trimEnd))
                        .OutputToFile(part2Output, true, options =>
                        {
                            options.UsingMultithreading(true).WithDuration(totalTime - trimEnd);

                            // Видеонастройки
                            if (!isAudioOnly)
                            {
                                ConfigureVideoOptions(options, videoCodec, videoSpeed, fps, width, height, videoBitrate);
                            }

                            // Аудионастройки
                            ConfigureAudioOptions(options, audioStreamCount, audioVolume, audioBitrate, videoSpeed, sampleRates, channels);
                        })
                        .NotifyOnProgress(progress =>
                        {
                            progressWindow.UpdateProgress(progress);
                        }, totalTime)
                        .ProcessSynchronously();

                    Dispatcher.Invoke(() => progressWindow.ChangeVideoTitle($"{fileName} [step 3/3]"));
                    if (outputType == ".mp3")
                    {
                        FFMpegArguments
                            .FromFileInput(part1Output)
                            .AddFileInput(part2Output)
                            .OutputToFile(outputPath, true, options =>
                            {
                                options.UsingMultithreading(true);
                                options.WithCustomArgument("-filter_complex \"concat=n=2:v=0:a=1\" -c:a libmp3lame");
                            })
                            .NotifyOnProgress(progress =>
                            {
                                progressWindow.UpdateProgress(progress);
                            }, totalTime)
                            .ProcessSynchronously();
                    }
                    else if (outputType == ".avi")
                    {
                        FFMpegArguments
                            .FromFileInput(part1Output)
                            .AddFileInput(part2Output)
                            .OutputToFile(outputPath, true, options =>
                            {
                                options.UsingMultithreading(true);
                                options.WithCustomArgument("-filter_complex \"concat=n=2:v=1:a=1\" -c:v mpeg4 -c:a mp3");
                            })
                            .NotifyOnProgress(progress =>
                            {
                                progressWindow.UpdateProgress(progress);
                            }, totalTime)
                            .ProcessSynchronously();
                    }
                    else
                    {
                        if (!mergeAudioTracks && audioStreamCount > 1)
                        {
                            FFMpegArguments
                                .FromFileInput(part1Output)
                                .AddFileInput(part2Output)
                                .OutputToFile(outputPath, true, options =>
                                {
                                    options.UsingMultithreading(true);
                                    ConfigureAudioOptions(options, audioStreamCount, audioVolume, audioBitrate, videoSpeed, sampleRates, channels);
                                })
                                .NotifyOnProgress(progress =>
                                {
                                    progressWindow.UpdateProgress(progress);
                                }, totalTime)
                                .ProcessSynchronously();
                        }
                        else
                        {
                            // Обычное объединение с concat демуксером
                            string concatFile = Path.Combine(Path.GetDirectoryName(outputPath), "concat_list.txt");
                            File.WriteAllText(concatFile, $"file '{part1Output}'\nfile '{part2Output}'");

                            FFMpegArguments
                                .FromFileInput(concatFile, true, options => options.WithCustomArgument("-f concat -safe 0"))
                                .OutputToFile(outputPath, true, options =>
                                {
                                    options.UsingMultithreading(true);
                                    options.WithCustomArgument("-c copy");
                                })
                                .NotifyOnProgress(progress =>
                                {
                                    progressWindow.UpdateProgress(progress);
                                }, totalTime)
                                .ProcessSynchronously();

                            File.Delete(concatFile);
                        }
                    }

                    File.Delete(part1Output);
                    File.Delete(part2Output);
                });
            }
        }

        private void ConfigureVideoOptions(FFMpegArgumentOptions options, string videoCodec, float videoSpeed,
            float fps, int width, int height, int videoBitrate)
        {
            if (videoBitrate == 0)
            {
                options.WithCustomArgument("-an"); // Без видео
            }
            else
            {
                string videoFilter = $"setpts={(1 / videoSpeed).ToString().Replace(',', '.')}*PTS";

                // Если задаются видеонастройки
                options
                    .WithVideoCodec(videoCodec)
                    .WithFramerate(fps)
                    .Resize(width, height)
                    .WithVideoBitrate(videoBitrate);

                if (videoSpeed != 1.0f)
                {
                    // Применение фильтра для изменения скорости видео
                    options.WithCustomArgument($"-filter:v \"{videoFilter}\"");
                }
            }
        }

        private void ConfigureAudioOptions(FFMpegArgumentOptions options, byte audioStreamCount, float audioVolume,
            int audioBitrate, float videoSpeed, int sampleRates, string channels)
        {
            if (audioVolume == 0 || audioBitrate == 0)
            {
                options.WithCustomArgument(isAudioOnly ? "-filter:a \"volume=0\"" : "-an");
            }
            else
            {
                string audioFilter = $"volume={audioVolume.ToString().Replace(',', '.')}," +
                                     $"atempo={videoSpeed.ToString().Replace(',', '.')}," +
                                     $"aformat=sample_fmts=s16:sample_rates={sampleRates}:channel_layouts={channels}";

                if (audioStreamCount > 1)
                {
                    string filterComplex = BuildAudioFilterComplex(audioStreamCount, audioFilter, channels);

                    if (mergeAudioTracks)
                    {
                        options.WithCustomArgument($"-filter_complex \"{filterComplex}\" -map 0:v -map [a]");
                    }
                    else
                    {
                        options.WithCustomArgument($"-filter_complex \"{filterComplex}\"");
                        for (int i = 0; i < audioStreamCount; i++) 
                            options.WithCustomArgument($"-map [a{i}]");
                        options.WithCustomArgument("-map 0:v");
                    }
                }
                else
                {
                    options.WithCustomArgument($"-filter:a \"{audioFilter}\"");
                }
            }
        }

        // Метод для создания filter_complex для нескольких аудиотреков
        private string BuildAudioFilterComplex(byte audioStreamCount, string audioFilter, string channels)
        {
            string filterComplex = string.Empty;
            for (int i = 0; i < audioStreamCount; i++)
            {
                filterComplex += $"[0:a:{i}]{audioFilter}[a{i}];";
            }

            if (mergeAudioTracks)
            {
                filterComplex += string.Join("", Enumerable.Range(0, audioStreamCount).Select(i => $"[a{i}]"));
                filterComplex += $"amerge=inputs={audioStreamCount},pan={channels}|c0<c0+c2|c1<c1+c3[a]";
            }
            else
            {
                filterComplex = filterComplex.TrimEnd(';');
            }
            return filterComplex;
        }


        public void EnableRenderButton()
        {
            RenderButton.IsEnabled = true;
        }
        public void ChangeRenderText(string text)
        {
            LastRenderedVideoText.IsEnabled = true;
            LastRenderedVideoText.Opacity = 1;
            LastRenderedVideoText.Text = text;
        }
        public void StopRender() => renderShutdown = true;
        private void ViewRenderedVideo(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (LastRenderedVideoText.Opacity != 0)
                {
                    string directoryPath = Path.GetDirectoryName(OutputFilePath.Text);

                    if (!string.IsNullOrEmpty(directoryPath))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", directoryPath);
                    }
                    else
                    {
                        MessageBox.Show("The specified folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to open path: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PlaySound(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (SoundPlayer player = new SoundPlayer(stream))
                    {
                        player.Play();
                    }
                }
            }
        }

        #endregion
    }
}