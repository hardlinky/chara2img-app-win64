using System;
using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

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
            
            this.Loaded += MainWindow_Loaded;
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
            // Any global property change handling can go here
        }
    }

    #region Value Converters

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

    // Add this to your existing Converters file or MainWindow.xaml.cs

    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var count = 0;
            if (value is int intValue)
                count = intValue;
            else if (value is ICollection collection)
                count = collection.Count;

            var isInverse = parameter?.ToString()?.ToLower() == "inverse";
            var isZero = count == 0;

            if (isInverse)
                return isZero ? Visibility.Collapsed : Visibility.Visible;
            else
                return isZero ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InputTypeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string inputType && parameter is string expectedType)
            {
                return inputType == expectedType ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}