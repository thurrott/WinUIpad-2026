using System;
using System.IO;
using Microsoft.UI.Xaml.Data;

namespace WinUIpad
{
    public class FileNameConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, string language)
        {
            var s = value as string;
            if (string.IsNullOrWhiteSpace(s))
                return string.Empty;

            try
            {
                return Path.GetFileNameWithoutExtension(s);
            }
            catch
            {
                // Fallback to original if Path fails for any reason
                return s;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, string language)
        {
            // TitleTextBlock is OneWay; ConvertBack won't be used.
            // Return the incoming value to be safe if ever invoked.
            return value;
        }
    }
}