using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Graphics;

namespace WinUIpad
{
    public partial class AppSettings : INotifyPropertyChanged
    {
        public AppSettings()
        {

        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadWindowSettings(Microsoft.UI.Windowing.AppWindow appWindow)
        {
            // Window location
            appWindow.Move(new PointInt32(Settings.Default.LocationX, Settings.Default.LocationY));

            // Windows size
            appWindow.Resize(new SizeInt32(Settings.Default.Width, Settings.Default.Height));

            // Is Windows maximized?
            if (Settings.Default.Maximized)
            {
                var presenter = (Microsoft.UI.Windowing.OverlappedPresenter)appWindow.Presenter;
                presenter.Maximize();
            }
        }

        public void SaveWindowSettings(Microsoft.UI.Windowing.AppWindow appWindow)
        {
            // Window maximized?
            var presenter = (Microsoft.UI.Windowing.OverlappedPresenter)appWindow.Presenter;
            if (presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Maximized)
            {
                Settings.Default.Maximized = true;
            }
            else
            {
                Settings.Default.Maximized = false;

                // Window size
                Settings.Default.Width = appWindow.Size.Width;
                Settings.Default.Height = appWindow.Size.Height;

                // Window location
                Settings.Default.LocationX = appWindow.Position.X;
                Settings.Default.LocationY = appWindow.Position.Y;
            }
            Settings.Default.Save();
        }

        public int LoadThemeSettings(RadioButtons trb)
        {
            trb.SelectedIndex = Settings.Default.Theme;
            return trb.SelectedIndex;
        }

        public void SaveThemeSettings(RadioButtons trb)
        {
            Settings.Default.Theme = trb.SelectedIndex;
            Settings.Default.Save();
        }

        public void LoadFontSettings(out string fontName, out double fontSize, out bool fontItalic, out bool  fontBold)
        {
            fontName = string.IsNullOrEmpty(Settings.Default.FontName) ? "Consolas" : Settings.Default.FontName;
            fontSize = Settings.Default.FontSize;
            fontItalic = Settings.Default.FontItalic;
            fontBold = Settings.Default.FontBold;
        }

        public void SaveFontSettings(TextBox tb)
        {
            Settings.Default.FontName = tb.FontFamily.Source.ToString();
            Settings.Default.FontSize = tb.FontSize;
            if (tb.FontStyle == Windows.UI.Text.FontStyle.Italic)
                Settings.Default.FontItalic = true;
            else Settings.Default.FontItalic = false;
            if (tb.FontWeight == FontWeights.Bold)
                Settings.Default.FontBold = true;
            else Settings.Default.FontBold = false;
            
            Settings.Default.Save();
        }

        public void LoadWordWrapSettings(TextBox tb, ToggleMenuFlyoutItem mnfo, ToggleSwitch ts)
        {
            if (Settings.Default.WordWrap == true)
            {
                tb.TextWrapping = TextWrapping.Wrap;
                mnfo.IsChecked = true;
                ts.IsOn = true;
            }
            else
            {
                tb.TextWrapping = TextWrapping.NoWrap;
                mnfo.IsChecked = false;
                ts.IsOn = false;
            }
        }

        public void SaveWordWrapSettings(TextWrapping tw)
        {
            Settings.Default.WordWrap = (tw == TextWrapping.Wrap) ? true : false;
            Settings.Default.Save();
        }

        public void LoadStatusBarSettings(Grid sbg, ToggleMenuFlyoutItem sbm, ToggleSwitch ts)
        {
            if (Settings.Default.StatusBar == true)
            {
                sbg.Visibility = Visibility.Visible;
                sbm.IsChecked = true;
                ts.IsOn = true;
            }
            else
            {
                sbg.Visibility = Visibility.Collapsed;
                sbm.IsChecked = false;
                ts.IsOn = false;
            }
        }

        public void SaveStatusBarSettings(Visibility v)
        {
            Settings.Default.StatusBar = (v == Visibility.Visible) ? true : false;
            Settings.Default.Save();
        }
    }
}
