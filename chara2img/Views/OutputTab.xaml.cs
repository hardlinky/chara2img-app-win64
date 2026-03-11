using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;

namespace chara2img.Views
{
    /// <summary>
    /// Interaction logic for OutputTab.xaml
    /// </summary>
    public partial class OutputTab
    {
        private Point _scrollMousePoint;
        private double _hOffset;
        private double _vOffset;
        private bool _isDragging;

        private Point _fullscreenScrollMousePoint;
        private double _fullscreenHOffset;
        private double _fullscreenVOffset;
        private bool _isFullscreenDragging;

        public OutputTab()
        {
            InitializeComponent();
            this.Loaded += OutputTab_Loaded;
            this.DataContextChanged += OutputTab_DataContextChanged;
        }

        private void OutputTab_Loaded(object sender, RoutedEventArgs e)
        {
            // Set focus to enable keyboard input
            this.Focusable = true;
            this.Focus();
            
            SubscribeToViewModelEvents();
        }

        private void OutputTab_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old view model
            if (e.OldValue is INotifyPropertyChanged oldViewModel)
            {
                oldViewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            
            // Subscribe to new view model
            SubscribeToViewModelEvents();
        }

        private void SubscribeToViewModelEvents()
        {
            if (DataContext is INotifyPropertyChanged viewModel)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged; // Prevent double subscription
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModels.MainViewModel.ImageZoom))
            {
                if (DataContext is ViewModels.MainViewModel viewModel && viewModel.ImageZoom == -1)
                {
                    // ImageZoom was set to -1, which means we should fit to screen
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FitImageToScreen();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
            else if (e.PropertyName == nameof(ViewModels.MainViewModel.IsFullscreen))
            {
                if (DataContext is ViewModels.MainViewModel viewModel && viewModel.IsFullscreen)
                {
                    // Fit image when entering fullscreen
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FitFullscreenImageToScreen();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }
        }

        private void OutputTab_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not ViewModels.MainViewModel viewModel) return;

            // Handle F11 for fullscreen toggle
            if (e.Key == Key.F11)
            {
                viewModel.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Handle Escape - exit fullscreen first, then close image
            if (e.Key == Key.Escape)
            {
                if (viewModel.IsFullscreen)
                {
                    viewModel.ToggleFullscreenCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
                else if (!viewModel.IsGalleryView)
                {
                    viewModel.CloseImageCommand.Execute(null);
                    e.Handled = true;
                    return;
                }
            }

            if (viewModel.IsGalleryView || viewModel.CurrentImage == null) return;

            var currentImages = viewModel.CurrentImages;
            if (currentImages.Count == 0) return;

            var currentIndex = currentImages.IndexOf(viewModel.CurrentImage);
            if (currentIndex == -1) return;

            switch (e.Key)
            {
                case Key.Left:
                    // Go to previous image (loop to last if at first)
                    var prevIndex = currentIndex == 0 ? currentImages.Count - 1 : currentIndex - 1;
                    viewModel.ShowImageCommand.Execute(currentImages[prevIndex]);
                    e.Handled = true;
                    break;

                case Key.Right:
                    // Go to next image (loop to first if at last)
                    var nextIndex = currentIndex == currentImages.Count - 1 ? 0 : currentIndex + 1;
                    viewModel.ShowImageCommand.Execute(currentImages[nextIndex]);
                    e.Handled = true;
                    break;

                case Key.Down:
                    // Go to next job
                    viewModel.NavigateToNextJobCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Go to previous job
                    viewModel.NavigateToPreviousJobCommand.Execute(null);
                    e.Handled = true;
                    break;
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

        private void FitFullscreenImageToScreen()
        {
            if (FullscreenImage.Source == null) return;

            var imageWidth = FullscreenImage.Source.Width;
            var imageHeight = FullscreenImage.Source.Height;
            
            // Use the actual fullscreen scroll viewer dimensions
            var scrollViewerWidth = FullscreenScrollViewer.ActualWidth;
            var scrollViewerHeight = FullscreenScrollViewer.ActualHeight;

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
            FullscreenScaleTransform.ScaleX = scale;
            FullscreenScaleTransform.ScaleY = scale;

            // Reset scroll position to center
            FullscreenScrollViewer.ScrollToHorizontalOffset(0);
            FullscreenScrollViewer.ScrollToVerticalOffset(0);
        }

        private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control || OutputImage.Source != null)
            {
                // Get mouse position relative to the ScrollViewer
                var mousePos = e.GetPosition(ImageScrollViewer);
                
                // Calculate the point in the image that's under the cursor (before zoom)
                var pointInImage = new Point(
                    (mousePos.X + ImageScrollViewer.HorizontalOffset) / ImageScaleTransform.ScaleX,
                    (mousePos.Y + ImageScrollViewer.VerticalOffset) / ImageScaleTransform.ScaleY
                );

                // Calculate new scale
                double zoom = e.Delta > 0 ? 0.1 : -0.1;
                double oldScale = ImageScaleTransform.ScaleX;
                double newScale = oldScale + zoom;

                // Limit zoom between 0.1x and 10x
                newScale = Math.Max(0.1, Math.Min(10, newScale));

                // Apply the new scale
                ImageScaleTransform.ScaleX = newScale;
                ImageScaleTransform.ScaleY = newScale;

                // Calculate new scroll offsets to keep the same point under the cursor
                var newOffsetX = pointInImage.X * newScale - mousePos.X;
                var newOffsetY = pointInImage.Y * newScale - mousePos.Y;

                // Apply new scroll offsets
                ImageScrollViewer.ScrollToHorizontalOffset(newOffsetX);
                ImageScrollViewer.ScrollToVerticalOffset(newOffsetY);

                e.Handled = true;
            }
        }

        private void FullscreenScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (FullscreenImage.Source != null)
            {
                // Get mouse position relative to the ScrollViewer
                var mousePos = e.GetPosition(FullscreenScrollViewer);
                
                // Calculate the point in the image that's under the cursor (before zoom)
                var pointInImage = new Point(
                    (mousePos.X + FullscreenScrollViewer.HorizontalOffset) / FullscreenScaleTransform.ScaleX,
                    (mousePos.Y + FullscreenScrollViewer.VerticalOffset) / FullscreenScaleTransform.ScaleY
                );

                // Calculate new scale
                double zoom = e.Delta > 0 ? 0.1 : -0.1;
                double oldScale = FullscreenScaleTransform.ScaleX;
                double newScale = oldScale + zoom;

                // Limit zoom between 0.1x and 10x
                newScale = Math.Max(0.1, Math.Min(10, newScale));

                // Apply the new scale
                FullscreenScaleTransform.ScaleX = newScale;
                FullscreenScaleTransform.ScaleY = newScale;

                // Calculate new scroll offsets to keep the same point under the cursor
                var newOffsetX = pointInImage.X * newScale - mousePos.X;
                var newOffsetY = pointInImage.Y * newScale - mousePos.Y;

                // Apply new scroll offsets
                FullscreenScrollViewer.ScrollToHorizontalOffset(newOffsetX);
                FullscreenScrollViewer.ScrollToVerticalOffset(newOffsetY);

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

        private void FullscreenImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FullscreenImage.Source != null)
            {
                _fullscreenScrollMousePoint = e.GetPosition(FullscreenScrollViewer);
                _fullscreenHOffset = FullscreenScrollViewer.HorizontalOffset;
                _fullscreenVOffset = FullscreenScrollViewer.VerticalOffset;
                _isFullscreenDragging = true;
                FullscreenImage.Tag = Cursors.Hand;
                FullscreenImage.CaptureMouse();
            }
        }

        private void FullscreenImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isFullscreenDragging = false;
            FullscreenImage.Tag = null;
            FullscreenImage.ReleaseMouseCapture();
        }

        private void FullscreenImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isFullscreenDragging && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPoint = e.GetPosition(FullscreenScrollViewer);
                double deltaX = currentPoint.X - _fullscreenScrollMousePoint.X;
                double deltaY = currentPoint.Y - _fullscreenScrollMousePoint.Y;

                FullscreenScrollViewer.ScrollToHorizontalOffset(_fullscreenHOffset - deltaX);
                FullscreenScrollViewer.ScrollToVerticalOffset(_fullscreenVOffset - deltaY);
            }
        }

        private void OutputImage_TargetUpdated(object sender, System.Windows.Data.DataTransferEventArgs e)
        {
            // When a new image is loaded, fit it to screen
            Dispatcher.BeginInvoke(new Action(() =>
            {
                FitImageToScreen();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void ImageScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-fit image when scroll viewer size changes (e.g., window resize)
            if (OutputImage.Source != null && ImageScaleTransform.ScaleX == 1.0)
            {
                FitImageToScreen();
            }
        }

        private void FullscreenScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Re-fit image when fullscreen scroll viewer size changes
            if (FullscreenImage.Source != null)
            {
                FitFullscreenImageToScreen();
            }
        }
    }
}