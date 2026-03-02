using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using chara2img.Models;

namespace chara2img
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
}