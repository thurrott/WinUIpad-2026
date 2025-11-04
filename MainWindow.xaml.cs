using Microsoft.Graphics.Canvas.Text;
using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Collections.Generic;
using Windows.UI.Text;

namespace WinUIpad
{
    public sealed partial class MainWindow : Window
    {
        public App app = (App)Application.Current;
        public Document doc = new();

        // Find and replace
        private string lastSearchText = string.Empty;
        private int lastSearchPosition = -1;

        // Font
        private string fontName = "";
        private double fontSize = 0;
        private bool fontItalic = false, fontBold = false;

        // State
        AppWindow appWindow;
        public AppSettings appSettings { get; set; }

        public MainWindow()
        {
            appSettings = new();

            InitializeComponent();

            // For window metrics
            IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

            // Customize the title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitlebarGrid);
            TitlebarGrid.MinWidth = 188;

            // Load app settings
            LoadAppSettings();

            // Set up document
            doc.ResetDocument();

            // Focus the text box when the app appears
            this.TextBox1.Loaded += (s, e) =>
            {
                this.TextBox1.Focus(FocusState.Programmatic);
            };
        }

        public void LoadAppSettings()
        {
            var app = (App)Application.Current;

            // Window metrics
            appSettings.LoadWindowSettings(appWindow);

            // Theme
            ((FrameworkElement)this.Content).RequestedTheme = appSettings.LoadThemeSettings(ThemeRadioButtons) switch
            {
                0 => ElementTheme.Light,
                1 => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };

            // Font
            Font_Configuration();

            // Word wrap
            appSettings.LoadWordWrapSettings(TextBox1, WordWrapMenu, WordWrapToggle);

            // Status bar
            appSettings.LoadStatusBarSettings(StatusbarGrid, StatusBarMenu, StatusBarToggle);
        }

        private async void Font_Configuration()
        {
            appSettings.LoadFontSettings(out fontName, out fontSize, out fontItalic, out fontBold);

            // Use Microsoft.Graphics.Win2D to get system font families
            string[] fontFamilies = CanvasTextFormat.GetSystemFontFamilies();
            Array.Sort(fontFamilies);

            // Font family combo box
            FontFamilyComboBox.ItemsSource = fontFamilies;
            for (int x = 0; x <= fontFamilies.Length - 1; x++)
            {
                if ((string)FontFamilyComboBox.Items[x] == Settings.Default.FontName)
                {
                    FontFamilyComboBox.SelectedItem = FontFamilyComboBox.Items[x];
                    break;
                }
            }
            FontFamilyComboBox.SelectedItem = Settings.Default.FontName;
            FontExampleTextBlock.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily(fontName);

            // Font style combo box
            List<string> fontStyle =
            [
                "Normal", "Italic", "Bold", "Bold Italic"
            ];
            FontStyleComboBox.DataContext = fontStyle;

            if (fontBold && fontItalic)
            {
                FontStyleComboBox.SelectedIndex = 3;
                FontExampleTextBlock.FontStyle = FontStyle.Italic;
                FontExampleTextBlock.FontWeight = FontWeights.Bold;
            }
            else if (fontBold == true && fontItalic == false)
            {
                FontStyleComboBox.SelectedIndex = 2;
                FontExampleTextBlock.FontStyle = FontStyle.Normal;
                FontExampleTextBlock.FontWeight = FontWeights.Bold;
            }
            else if (fontItalic == true && fontBold == false)
            {
                FontStyleComboBox.SelectedIndex = 1;
                FontExampleTextBlock.FontStyle = FontStyle.Italic;
                FontExampleTextBlock.FontWeight = FontWeights.Normal;
            }
            else
            {
                FontStyleComboBox.SelectedIndex = 0;
                FontExampleTextBlock.FontStyle = FontStyle.Normal;
                FontExampleTextBlock.FontWeight = FontWeights.Normal;
            }

            // Font size combo box
            List<double> myFontSize =
            [
                8,9,10,11,12,14,16,18,20,22,24,26,28,36,48,72
            ];
            FontSizeComboBox.DataContext = myFontSize;

            for (int x = 0; x < myFontSize.Count; x++)
                if (myFontSize[x] == fontSize)
                {
                    FontSizeComboBox.SelectedIndex = x;
                    break;
                }
            FontExampleTextBlock.FontSize = (double)FontSizeComboBox.SelectedItem;
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            appSettings.SaveWindowSettings(appWindow);
            appSettings.SaveThemeSettings(ThemeRadioButtons);
            appSettings.SaveFontSettings(TextBox1);
            appSettings.SaveWordWrapSettings(TextBox1.TextWrapping);
            appSettings.SaveStatusBarSettings(StatusbarGrid.Visibility);
        }

        //
        // File menu event handlers
        //
        private async void FileOpMenu_Click(object sender, RoutedEventArgs e)
        {
            // This is for File > New and File > Open

            FileOperations fo = new FileOperations();
            if (await fo.NeedsToBeSavedAsync(this, doc))
            {
                if (doc != null)
                {
                    string menuText = ((MenuFlyoutItem)sender).Text;
                    switch (menuText)
                    {
                        case "New":
                            doc.ResetDocument();
                            break;
                        case "Open":
                            fo.OpenFile(this, doc);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        private void NewWindowMenu_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Activate();
        }

        private void SaveMenu_Click(object sender, RoutedEventArgs e)
        {
            if (doc != null)
            {
                if (doc.DocumentIsSaved)
                {
                    // Save existing document
                    FileOperations fo = new FileOperations();
                    fo.SaveDocument(this, doc);
                }
                else
                {
                    // Save as a new document
                    SaveAsMenu_Click(sender, e);
                }
            }
        }

        private async void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (doc != null)
            {
                FileOperations fo = new FileOperations();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                await fo.SaveAsDocument(hwnd, doc);
            }
        }

        private void PageSetupMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void PrintMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            // State check before close
        }

        //
        // Edit menu event handlers
        //

        private void UndoMenu_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox1.CanUndo) TextBox1.Undo();
        }

        private void CutMenu_Click(object sender, RoutedEventArgs e)
        {
            TextBox1.CutSelectionToClipboard();
        }

        private void CopyMenu_Click(object sender, RoutedEventArgs e)
        {
            TextBox1.CopySelectionToClipboard();
        }

        private void PasteMenu_Click(object sender, RoutedEventArgs e)
        {
            TextBox1.PasteFromClipboard();
        }

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox1.SelectedText))
            {
                TextBox1.SelectedText = string.Empty;
            }
        }

        private async void SearchMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TextBox1.SelectedText))
            {
                var uri = new Uri($"https://www.bing.com/search?q={Uri.EscapeDataString(TextBox1.SelectedText)}");
                await Windows.System.Launcher.LaunchUriAsync(uri);
            }
        }

        private async void FindMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Find",
                PrimaryButtonText = "Find Next",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var panel = new StackPanel { Spacing = 10 };
            var textBox = new TextBox
            {
                PlaceholderText = "Find what",
                Text = lastSearchText
            };
            panel.Children.Add(textBox);

            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(textBox.Text))
            {
                lastSearchText = textBox.Text;
                FindNext();
            }
        }

        private void FindNextMenu_Click(object sender, RoutedEventArgs e)
        {
            FindNext();
        }

        private void FindNext()
        {
            if (string.IsNullOrEmpty(lastSearchText)) return;

            var text = TextBox1.Text;
            var startIndex = lastSearchPosition + 1;
            if (startIndex >= text.Length) startIndex = 0;

            var index = text.IndexOf(lastSearchText, startIndex, StringComparison.CurrentCultureIgnoreCase);
            if (index == -1 && startIndex > 0)
            {
                // Wrap around to the beginning
                index = text.IndexOf(lastSearchText, 0, StringComparison.CurrentCultureIgnoreCase);
            }

            if (index != -1)
            {
                TextBox1.SelectionStart = index;
                TextBox1.SelectionLength = lastSearchText.Length;
                TextBox1.Focus(FocusState.Programmatic);
                lastSearchPosition = index;
            }
        }

        private void FindPreviousMenu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(lastSearchText)) return;

            var text = TextBox1.Text;
            var startIndex = lastSearchPosition - 1;
            if (startIndex < 0) startIndex = text.Length - 1;

            var index = text.LastIndexOf(lastSearchText, startIndex, StringComparison.CurrentCultureIgnoreCase);
            if (index == -1 && startIndex < text.Length - 1)
            {
                // Wrap around to the end
                index = text.LastIndexOf(lastSearchText, text.Length - 1, StringComparison.CurrentCultureIgnoreCase);
            }

            if (index != -1)
            {
                TextBox1.SelectionStart = index;
                TextBox1.SelectionLength = lastSearchText.Length;
                TextBox1.Focus(FocusState.Programmatic);
                lastSearchPosition = index;
            }
        }

        private async void ReplaceMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Replace",
                PrimaryButtonText = "Replace",
                SecondaryButtonText = "Replace All",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var panel = new StackPanel { Spacing = 10 };
            var findBox = new TextBox
            {
                PlaceholderText = "Find what",
                Text = lastSearchText
            };
            var replaceBox = new TextBox { PlaceholderText = "Replace with" };
            panel.Children.Add(findBox);
            panel.Children.Add(replaceBox);

            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                if (!string.IsNullOrEmpty(findBox.Text))
                {
                    lastSearchText = findBox.Text;
                    var selStart = TextBox1.SelectionStart;
                    FindNext();
                    if (TextBox1.SelectionLength > 0)
                    {
                        TextBox1.SelectedText = replaceBox.Text;
                    }
                }
            }
            else if (result == ContentDialogResult.Secondary)
            {
                if (!string.IsNullOrEmpty(findBox.Text))
                {
                    TextBox1.Text = TextBox1.Text.Replace(findBox.Text, replaceBox.Text);
                }
            }
        }

        private async void GoToMenu_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Go To Line",
                PrimaryButtonText = "Go To",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };

            var panel = new StackPanel { Spacing = 10 };
            var lineBox = new TextBox
            {
                PlaceholderText = "Line number",
                InputScope = new InputScope { Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } } }
            };
            panel.Children.Add(lineBox);

            dialog.Content = panel;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && int.TryParse(lineBox.Text, out int lineNumber))
            {
                var lines = TextBox1.Text.Split('\n');
                if (lineNumber > 0 && lineNumber <= lines.Length)
                {
                    var position = 0;
                    for (int i = 0; i < lineNumber - 1; i++)
                    {
                        position += lines[i].Length + 1; // +1 for the newline character
                    }
                    TextBox1.SelectionStart = position;
                    TextBox1.SelectionLength = 0;
                    TextBox1.Focus(FocusState.Programmatic);
                }
            }
        }

        private void TimeDateMenu_Click(object sender, RoutedEventArgs e)
        {
            int selectionStart = TextBox1.SelectionStart;
            string dateTime = DateTime.Now.ToString("h:mm tt M/d/yyyy");
            TextBox1.Text = TextBox1.Text.Insert(selectionStart, dateTime);
            TextBox1.SelectionStart = selectionStart + dateTime.Length;
        }

        private void FontMenu_Click(object sender, RoutedEventArgs e)
        {
            MainSettingsGrid.Visibility = Visibility.Visible;
            MainAppGrid.Visibility = Visibility.Collapsed;
            FontExpander.IsExpanded = true;
        }

        //
        // View menu event handlers
        //

        private void ZoomInMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ZoomOutMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void RestoreDefaultZoomMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StatusBarMenu_Click(object sender, RoutedEventArgs e)
        {
            if (StatusbarGrid.Visibility == Visibility.Visible)
            {
                StatusbarGrid.Visibility = Visibility.Collapsed;
                StatusBarToggle.IsOn = false;
                Settings.Default.StatusBar = false;
            }
            else
            {
                StatusbarGrid.Visibility = Visibility.Visible;
                StatusBarToggle.IsOn = true;
                Settings.Default.StatusBar = true;
            }
            Settings.Default.Save();
        }

        private void WordWrapMenu_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox1.TextWrapping == TextWrapping.Wrap)
            {
                TextBox1.TextWrapping = TextWrapping.NoWrap;
                WordWrapToggle.IsOn = false;
                Settings.Default.WordWrap = false;
            }
            else
            {
                TextBox1.TextWrapping = TextWrapping.Wrap;
                WordWrapToggle.IsOn = true;
                Settings.Default.WordWrap = true;
            }
            Settings.Default.Save();
        }


        //
        // Text box event handlers
        //

        private void TextBox1_TextChanging(Microsoft.UI.Xaml.Controls.TextBox sender, Microsoft.UI.Xaml.Controls.TextBoxTextChangingEventArgs args)
        {
                
        }

        //
        // Settings event handlers
        //

        private async void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            FontExpander.IsExpanded = false;
            MainAppGrid.Visibility = Visibility.Collapsed;
            MainSettingsGrid.Visibility = Visibility.Visible;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            FontExpander.IsExpanded = false;
            MainSettingsGrid.Visibility = Visibility.Collapsed;
            MainAppGrid.Visibility = Visibility.Visible;

            // Focus the text box
            this.TextBox1.Loaded += (s, e) =>
            {
                this.TextBox1.Focus(FocusState.Programmatic);
            };
        }

        private void LightRadioButton_Click(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)this.Content).RequestedTheme = ElementTheme.Light;
        }

        private void DarkRadioButton_Click(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)this.Content).RequestedTheme = ElementTheme.Dark;
        }

        private void SystemRadioButton_Click(object sender, RoutedEventArgs e)
        {
            ((FrameworkElement)this.Content).RequestedTheme = ElementTheme.Default;
        }

        private void StatusBarToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var isVisible = StatusBarToggle.IsOn;
            StatusBarMenu.IsChecked = isVisible;
            StatusbarGrid.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            Settings.Default.StatusBar = isVisible;
            Settings.Default.Save();
        }

        private void WordWrapToggle_Toggled(object sender, RoutedEventArgs e)
        {
            var isEnabled = WordWrapToggle.IsOn;
            WordWrapMenu.IsChecked = isEnabled;
            TextBox1.TextWrapping = isEnabled ? TextWrapping.Wrap : TextWrapping.NoWrap;
            Settings.Default.WordWrap = isEnabled;
            Settings.Default.Save();
        }

        private void FontFamilyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FontExampleTextBlock.FontFamily = new Microsoft.UI.Xaml.Media.FontFamily((string)FontFamilyComboBox.SelectedItem);
            TextBox1.FontFamily = FontExampleTextBlock.FontFamily;
            Settings.Default.FontName = TextBox1.FontFamily.Source.ToString();
            Settings.Default.Save();
        }

        private void FontStyleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // fontBold && fontItalic
            if (FontStyleComboBox.SelectedIndex == 3)
            {
                FontExampleTextBlock.FontStyle = FontStyle.Italic;
                FontExampleTextBlock.FontWeight = FontWeights.Bold;
            }
            // fontBold == true && fontItalic == false
            else if (FontStyleComboBox.SelectedIndex == 2)
            {
                FontExampleTextBlock.FontStyle = FontStyle.Normal;
                FontExampleTextBlock.FontWeight = FontWeights.Bold;
            }
            // fontItalic == true && fontBold == false
            else if (FontStyleComboBox.SelectedIndex == 1)
            {
                FontExampleTextBlock.FontStyle = FontStyle.Italic;
                FontExampleTextBlock.FontWeight = FontWeights.Normal;
            }
            // normal / 0
            else
            {
                FontExampleTextBlock.FontStyle = FontStyle.Normal;
                FontExampleTextBlock.FontWeight = FontWeights.Normal;
            }

            TextBox1.FontStyle = FontExampleTextBlock.FontStyle;
            TextBox1.FontWeight = FontExampleTextBlock.FontWeight;

            if(TextBox1.FontStyle == FontStyle.Italic)
                Settings.Default.FontItalic = true;
            else Settings.Default.FontItalic = false;

            if(TextBox1.FontWeight == FontWeights.Bold)
                Settings.Default.FontBold = true;
            else Settings.Default.FontBold = false;

            Settings.Default.Save();
        }

        private void FontSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FontExampleTextBlock.FontSize = (double)FontSizeComboBox.SelectedItem;
            TextBox1.FontSize = FontExampleTextBlock.FontSize;
        }
    }
}
