using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace VideoScreensaver {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private bool preview;

        private ArrayList videoList;

        private Point? lastMousePosition = null;  // Workaround for "MouseMove always fires when maximized" bug.

        private double volume {
            get { return FullScreenMedia.Volume; }
            set {
                FullScreenMedia.Volume = Math.Max(Math.Min(value, 1), 0);
                PreferenceManager.WriteVolumeSetting(FullScreenMedia.Volume);
            }
        }

        public MainWindow(bool preview) {
            videoList = new ArrayList();
            InitializeComponent();
            this.preview = preview;
            FullScreenMedia.Volume = PreferenceManager.ReadVolumeSetting();
            if (preview) {
                ShowError("When fullscreen, control volume with up/down arrows or mouse wheel.");
            }
        }

        private void ScrKeyDown(object sender, KeyEventArgs e) {
            switch (e.Key) {
                case Key.Right:
                    SetNewMedia((String)videoList[new Random().Next(videoList.Count)]);
                    break;
                case Key.Up:
                case Key.VolumeUp:
                    volume += 0.1;
                    break;
                case Key.Down:
                case Key.VolumeDown:
                    volume -= 0.1;
                    break;
                case Key.VolumeMute:
                case Key.D0:
                    volume = 0;
                    break;
                default:
                    EndFullScreensaver();
                    break;
            }
        }

        private void ScrMouseWheel(object sender, MouseWheelEventArgs e) {
            volume += e.Delta / 1000.0;
        }

        private void ScrMouseMove(object sender, MouseEventArgs e) {
            // Workaround for bug in WPF.
            Point mousePosition = e.GetPosition(this);
            if (lastMousePosition != null && mousePosition != lastMousePosition) {
                EndFullScreensaver();
            }
            lastMousePosition = mousePosition;
        }

        private void ScrMouseDown(object sender, MouseButtonEventArgs e) {
            EndFullScreensaver();
        }

        private void ScrSizeChange(object sender, SizeChangedEventArgs e) {
            FullScreenMedia.Width = e.NewSize.Width;
            FullScreenMedia.Height = e.NewSize.Height;
        }

        // End the screensaver only if running in full screen. No-op in preview mode.
        private void EndFullScreensaver() {
            if (!preview) {
                Close();
            }
        }

        private void GetAllVideos(String videoPath)
        {
            try
            {
                IEnumerable<String> files = Directory.EnumerateFiles(videoPath, "*", SearchOption.AllDirectories);
                            
                foreach (var f in files)
                {
                    if (f.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".mov", StringComparison.OrdinalIgnoreCase))
                        videoList.Add(f);
                }

                Console.WriteLine("{0} files found.", videoList.Count);
            }
            catch (UnauthorizedAccessException UAEx)
            {
                Console.WriteLine(UAEx.Message);
            }
            catch (PathTooLongException PathEx)
            {
                Console.WriteLine(PathEx.Message);
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            String videoPath = PreferenceManager.ReadVideoSettings();
            GetAllVideos(videoPath);
            if (videoPath.Length == 0) {
                ShowError("This screensaver needs to be configured before any video is displayed.");
            } else {
                FullScreenMedia.Source = new System.Uri((String)videoList[new Random().Next(videoList.Count)]);
            }
        }

        private void ShowError(string errorMessage) {
            ErrorText.Text = errorMessage;
            ErrorText.Visibility = System.Windows.Visibility.Visible;
            if (preview) {
                ErrorText.FontSize = 12;
            }
        }

        private void MediaEnded(object sender, RoutedEventArgs e) {
            SetNewMedia((String)videoList[new Random().Next(videoList.Count)]);
            //FullScreenMedia.Position = new TimeSpan(0);
        }

        private void SetNewMedia(String fileName)
        {
            FullScreenMedia.Source = new System.Uri(fileName);
            MediaFileName.Text = fileName;
        }
    }
}
