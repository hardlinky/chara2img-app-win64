using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using chara2img.Models;

namespace chara2img
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _scrollMousePoint;
        private double _hOffset;
        private double _vOffset;
        private bool _isDragging;
        private BitmapImage? _lastImageSource;

        public MainWindow()
        {
            InitializeComponent();
            
            this.Loaded += MainWindow_Loaded;
            
            // Check for image changes periodically
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel viewModel &&
                !viewModel.IsGalleryView &&
                viewModel.CurrentImage != null &&
                viewModel.CurrentImage != _lastImageSource)
            {
                _lastImageSource = viewModel.CurrentImage;
                Dispatcher.BeginInvoke(new Action(() => FitImageToScreen()), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is ViewModels.MainViewModel viewModel)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.IsGalleryView) && 
                sender is ViewModels.MainViewModel viewModel &&
                !viewModel.IsGalleryView &&
                viewModel.CurrentImage != null)
            {
                // When switching from gallery to image view
                _lastImageSource = viewModel.CurrentImage;
                Dispatcher.BeginInvoke(new Action(() => FitImageToScreen()), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void FitImageToScreen()
        {
            if (OutputImage.Source == null) return;

            var imageWidth = OutputImage.Source.Width;
            var imageHeight = OutputImage.Source.Height;
            
            var scrollViewerWidth = ImageScrollViewer.ActualWidth;
            var scrollViewerHeight = ImageScrollViewer.ActualHeight;

            if (imageWidth <= 0 || imageHeight <= 0 || scrollViewerWidth <= 0 || scrollViewerHeight <= 0)
            {
                return;
            }

            // Calculate scale to fit while maintaining aspect ratio
            var scaleX = scrollViewerWidth / imageWidth;
            var scaleY = scrollViewerHeight / imageHeight;
            var scale = Math.Min(scaleX, scaleY);

            // Don't zoom in beyond 100%
            scale = Math.Min(scale, 1.0);

            // Apply the calculated scale
            ImageScaleTransform.ScaleX = scale;
            ImageScaleTransform.ScaleY = scale;

            // Reset scroll position to top-left
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
        }

        private void JobsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (JobsListView.SelectedItem is RunpodJob job && !string.IsNullOrEmpty(job.Id))
            {
                try
                {
                    Clipboard.SetText(job.Id);
                    
                    // Optional: Show a brief confirmation
                    if (DataContext is ViewModels.MainViewModel viewModel)
                    {
                        var previousStatus = viewModel.StatusMessage;
                        viewModel.StatusMessage = $"Job ID copied to clipboard: {job.Id}";
                        
                        // Reset status after 2 seconds
                        Task.Delay(2000).ContinueWith(_ =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (viewModel.StatusMessage.StartsWith("Job ID copied"))
                                {
                                    viewModel.StatusMessage = previousStatus;
                                }
                            });
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to copy to clipboard: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control || OutputImage.Source != null)
            {
                double zoom = e.Delta > 0 ? 0.1 : -0.1;
                double newScale = ImageScaleTransform.ScaleX + zoom;

                // Limit zoom between 0.1x and 10x
                newScale = Math.Max(0.1, Math.Min(10, newScale));

                ImageScaleTransform.ScaleX = newScale;
                ImageScaleTransform.ScaleY = newScale;

                e.Handled = true;
            }
        }

        private void OutputImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (OutputImage.Source != null)
            {
                _scrollMousePoint = e.GetPosition(ImageScrollViewer);
                _hOffset = ImageScrollViewer.HorizontalOffset;
                _vOffset = ImageScrollViewer.VerticalOffset;
                _isDragging = true;
                OutputImage.Tag = Cursors.Hand;
                OutputImage.CaptureMouse();
            }
        }

        private void OutputImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            OutputImage.Tag = null;
            OutputImage.ReleaseMouseCapture();
        }

        private void OutputImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(ImageScrollViewer);
                double deltaX = currentPoint.X - _scrollMousePoint.X;
                double deltaY = currentPoint.Y - _scrollMousePoint.Y;

                ImageScrollViewer.ScrollToHorizontalOffset(_hOffset - deltaX);
                ImageScrollViewer.ScrollToVerticalOffset(_vOffset - deltaY);
            }
        }

        private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-fit when the window is resized
            if (DataContext is ViewModels.MainViewModel viewModel && 
                !viewModel.IsGalleryView && 
                viewModel.CurrentImage != null)
            {
                FitImageToScreen();
            }
        }

        private void OutputImage_TargetUpdated(object sender, DataTransferEventArgs e)
        {
            // This is triggered when the Image Source binding is updated
            if (DataContext is ViewModels.MainViewModel viewModel &&
                !viewModel.IsGalleryView &&
                viewModel.CurrentImage != null)
            {
                Dispatcher.BeginInvoke(new Action(() => FitImageToScreen()), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : false;
        }
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ExpandCollapseTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool isExpanded && isExpanded ? "▲ Collapse" : "▼ Show Full Response";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}