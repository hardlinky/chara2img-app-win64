using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using System.Windows;
using chara2img.Models;

namespace chara2img.Converters
{
    public class Base64ToBitmapImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var s = value as string;
            if (string.IsNullOrEmpty(s)) return null;

            try
            {
                var base64 = s.Contains(",") ? s.Substring(s.IndexOf(',') + 1) : s;
                var bytes = System.Convert.FromBase64String(base64);
                using var ms = new MemoryStream(bytes);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                // Try to set width/height on ImageInput if possible
                try
                {
                    // DataContext isn't available here; rely on Application.Current?.MainWindow to find matching inputs is problematic.
                    // We leave setting width/height to view code when loading files instead.
                }
                catch
                {
                }

                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
