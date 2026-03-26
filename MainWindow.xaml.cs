using Microsoft.UI.Text;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Text;
using WinUIpad.Services;
using WinUIpad.Helpers;

namespace WinUIpad
{
    public sealed partial class MainWindow : Window
    {
        //
        // Per-tab state
        //
        private sealed class TabData
        {
            public DocumentTab Document { get; } = new();    // Old
            // public DocumentTab documentTab { get; } = new();   // New
            public TextBox Editor { get; set; } = null!;
        }

        private readonly Dictionary<TabViewItem, TabData> _tabs = [];
        private TabViewItem? _activeItem;
        private TabData? Active => _activeItem is not null && _tabs.TryGetValue(_activeItem, out var d) ? d : null;
        private TextBox? ActiveEditor => Active?.Editor;

        //
        // App-level state
        //

        private readonly SettingsService _settingsService = new();
        private AppSettings Settings => _settingsService.Settings;

        //
        // Find/Replace state
        //
        private string _lastSearch = string.Empty;
        private int _lastSearchPos = -1;
        private bool _replaceMode = false;

        //
        // Other window-related items
        //
        private readonly AppWindow? _appWindow;
        private bool _showWordCount = true;        // for the status bar; doesn't affect the actual editor word count behavior

        public MainWindow()
        {
            _settingsService.Load();

            _appWindow = this.AppWindow;
            if (_appWindow is not null)
                _appWindow.Closing += OnAppWindowClosing;

            InitializeComponent();

            // Customize the title bar
            ExtendsContentIntoTitleBar = true;
            SetTitleBar(TitlebarGrid);

            // Set minimum window sizes
            if (_appWindow?.Presenter is OverlappedPresenter p)
            {
                p.PreferredMinimumWidth = 1600;
                p.PreferredMinimumHeight =1000;
            }

            ApplyWindowSettings();
            ApplyTheme(Settings.Theme);
            ApplySettingsUI();

            // Try to restore previous session; otherwise open one blank tab
            if (!RestoreSession())
                CreateTab();

            UpdateRecentFilesMenu();
        }

        //
        // File menu event handlers
        //
        private void NewTabMenu_Click(object sender, RoutedEventArgs e) => CreateTab();

        private void NewWindowMenu_Click(object sender, RoutedEventArgs e) =>
        new MainWindow().Activate();

        private async void OpenMenu_Click(object sender, RoutedEventArgs e)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var newDoc = new DocumentTab();
            if (await FileService.OpenPickerAsync(hwnd, newDoc))
            {
                // Open in a new tab (or reuse current blank unsaved tab)
                var useExisting = Active?.Document is { IsSaved: false, IsModified: false };
                if (useExisting == true && _activeItem is not null)
                {
                    var data = Active!;
                    data.Document.FileName = newDoc.FileName;
                    data.Document.Contents = newDoc.Contents;
                    data.Document.Encoding = newDoc.Encoding;
                    data.Document.LineEnding = newDoc.LineEnding;
                    data.Document.IsModified = false;
                    data.Document.IsSaved = true;
                    data.Editor.Text = newDoc.Contents;
                    UpdateStatusBar();
                }
                else
                {
                    var item = CreateTab(filePath: newDoc.FileName);
                    if (_tabs.TryGetValue(item, out var data))
                    {
                        data.Document.Contents = newDoc.Contents;
                        data.Document.Encoding = newDoc.Encoding;
                        data.Document.LineEnding = newDoc.LineEnding;
                        data.Document.IsModified = false;
                        data.Editor.Text = newDoc.Contents;
                        UpdateStatusBar();
                    }
                }
                UpdateRecentFilesMenu();
            }
        }

        private async void SaveMenu_Click(object sender, RoutedEventArgs e)
        {
            if (Active is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (await FileService.SaveAsync(hwnd, Active.Document))
                UpdateRecentFilesMenu();
        }

        private async void SaveAsMenu_Click(object sender, RoutedEventArgs e)
        {
            if (Active is null) return;
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            if (await FileService.SaveAsAsync(hwnd, Active.Document))
                UpdateRecentFilesMenu();
        }

        private void PageSetupMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void PrintMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void CloseTabMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_activeItem is not null)
                await CloseTabAsync(_activeItem);
        }

        private async void ExitMenu_Click(object sender, RoutedEventArgs e)
        {
            await TryExitAsync();
        }

        //
        // Edit menu event handlers
        //

        private void UndoMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor?.CanUndo == true) ActiveEditor.Undo();
        }

        private void CutMenu_Click(object sender, RoutedEventArgs e) => ActiveEditor?.CutSelectionToClipboard();

        private void CopyMenu_Click(object sender, RoutedEventArgs e) => ActiveEditor?.CopySelectionToClipboard();

        private void PasteMenu_Click(object sender, RoutedEventArgs e) => ActiveEditor?.PasteFromClipboard();

        private void DeleteMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor is { } ed && !string.IsNullOrEmpty(ed.SelectedText))
                ed.SelectedText = string.Empty;
        }

        private void FindMenu_Click(object sender, RoutedEventArgs e) => ShowFindBar(replaceMode: false);

        private void FindNextMenu_Click(object sender, RoutedEventArgs e)
        {
            if (FindReplaceBar.Visibility == Visibility.Collapsed && !string.IsNullOrEmpty(_lastSearch))
                FindNext();
            else
                ShowFindBar(replaceMode: false);
        }

        private void FindPrevMenu_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastSearch)) FindPrev();
        }

        private void ReplaceMenu_Click(object sender, RoutedEventArgs e) => ShowFindBar(replaceMode: true);

        private async void GoToMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor is not { } ed) return;

            var dlg = new ContentDialog
            {
                Title = "Go to line",
                PrimaryButtonText = "Go to",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.Content.XamlRoot
            };
            var lineBox = new TextBox
            {
                PlaceholderText = "Line number",
                InputScope = new InputScope { Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } } }
            };
            dlg.Content = lineBox;
            lineBox.Loaded += (_, _) => lineBox.Focus(FocusState.Programmatic);

            if (await dlg.ShowAsync() == ContentDialogResult.Primary
                && int.TryParse(lineBox.Text, out int lineNum) && lineNum > 0)
            {
                string text = ed.Text;
                int pos = 0, line = 1;
                while (pos < text.Length && line < lineNum)
                {
                    if (text[pos] == '\r')
                    {
                        line++;
                        if (pos + 1 < text.Length && text[pos + 1] == '\n') pos++;
                    }
                    else if (text[pos] == '\n') line++;
                    pos++;
                }
                if (line == lineNum)
                {
                    ed.SelectionStart = pos;
                    ed.SelectionLength = 0;
                    ed.Focus(FocusState.Programmatic);
                }
            }
        }

        private void SelectAllMenu_Click(object sender, RoutedEventArgs e)
        {
            ActiveEditor?.DispatcherQueue.TryEnqueue(() => ActiveEditor.SelectAll());
            ActiveEditor?.Focus(FocusState.Programmatic);
        }

        private void TimeDateMenu_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor is not { } ed) return;
            int pos = ed.SelectionStart;
            string stamp = DateTime.Now.ToString("h:mm tt M/d/yyyy");
            ed.Text = ed.Text.Insert(pos, stamp);
            ed.SelectionStart = pos + stamp.Length;
        }

        private void FontMenu_Click(object sender, RoutedEventArgs e)
        {
            FontExpander.IsExpanded = true;
            ShowSettings();
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

        private void ZoomResetMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void EncodingMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LineEndingMenu_Click(object sender, RoutedEventArgs e)
        {

        }

        private void StatusBarMenu_Click(object sender, RoutedEventArgs e)
        {
            bool show = StatusBarMenu.IsChecked;
            Settings.ShowStatusBar = show;
            StatusBarToggle.IsOn = show;
            StatusBarGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _settingsService.Save();
        }

        private void WordWrapMenu_Click(object sender, RoutedEventArgs e)
        {
            bool wrap = WordWrapMenu.IsChecked;
            Settings.WordWrap = wrap;
            WordWrapMenu.IsChecked = wrap;
            ApplyWordWrapToAllTabs(wrap);
            _settingsService.Save();
        }

        private void ApplyWordWrapToAllTabs(bool wrap)
        {
            foreach (var (_, data) in _tabs)
                data.Editor.TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        }

        // 
        // Status bar
        // 

        private void UpdateStatusBar()
        {
            UpdatePosition();
            UpdateCount();
            if (Active is { } a)
            {
                LineEndingText.Text = a.Document.LineEndingName;
                EncodingText.Text = a.Document.EncodingName;
            }
            ZoomText.Text = $"{Settings.ZoomLevel:0}%";
        }

        public void UpdatePosition(int caretIndex = -1)
        {
            if (ActiveEditor is not { } ed) return;
            string text = ed.Text ?? string.Empty;
            if (caretIndex < 0) caretIndex = ed.SelectionStart;
            caretIndex = Math.Clamp(caretIndex, 0, text.Length);

            int lineNum = 0, lineStart = 0;
            for (int i = 0; i < caretIndex; i++)
            {
                if (text[i] == '\r')
                {
                    lineNum++;
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    lineStart = i + 1;
                }
                else if (text[i] == '\n')
                {
                    lineNum++;
                    lineStart = i + 1;
                }
            }
            PositionText.Text = $"Ln {lineNum + 1}, Col {caretIndex - lineStart + 1}";
        }

        public void UpdateCount()
        {
            if (ActiveEditor is not { } ed) return;
            if (_showWordCount)
            {
                int n = Regex.Matches(ed.Text, @"\S+").Count;
                CountText.Text = n == 1 ? "1 word" : $"{n} words";
            }
            else
            {
                int n = ed.Text.Length;
                CountText.Text = n == 1 ? "1 character" : $"{n} characters";
            }
        }
        private void CountText_Tapped(object sender, TappedRoutedEventArgs e)
        {
            _showWordCount = !_showWordCount;
            UpdateCount();
        }

        //
        // Find and replace
        //
        private void ShowFindBar(bool replaceMode)
        {
            _replaceMode = replaceMode;
            FindReplaceBar.Visibility = Visibility.Visible;
            ReplaceLabel.Visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;
            ReplaceBox.Visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;
            ReplaceBtn.Visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;
            ReplaceAllBtn.Visibility = replaceMode ? Visibility.Visible : Visibility.Collapsed;

            if (!string.IsNullOrEmpty(_lastSearch))
                FindBox.Text = _lastSearch;

            FindBox.Focus(FocusState.Programmatic);
            FindBox.SelectAll();
        }

        private void CloseFindBar_Click(object sender, RoutedEventArgs e)
        {
            FindReplaceBar.Visibility = Visibility.Collapsed;
            ActiveEditor?.Focus(FocusState.Programmatic);
        }

        private void FindBox_TextChanged(object sender, TextChangedEventArgs e) =>
        FindResultText.Text = string.Empty;

        private void FindBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                _lastSearch = FindBox.Text;
                FindNext();
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                CloseFindBar_Click(sender, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void FindNextBtn_Click(object sender, RoutedEventArgs e)
        {
            _lastSearch = FindBox.Text;
            FindNext();
        }

        private void FindPrevBtn_Click(object sender, RoutedEventArgs e)
        {
            _lastSearch = FindBox.Text;
            FindPrev();
        }

        private void FindNext()
        {
            if (string.IsNullOrEmpty(_lastSearch) || ActiveEditor is not { } ed) return;

            string text = ed.Text;
            var comparison = MatchCaseBtn.IsChecked == true
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;

            int start = _lastSearchPos + 1;
            if (start >= text.Length) start = 0;

            int idx = FindOccurrence(text, _lastSearch, start, forward: true, comparison);
            if (idx == -1 && start > 0)
                idx = FindOccurrence(text, _lastSearch, 0, forward: true, comparison);

            if (idx != -1)
            {
                ed.SelectionStart = idx;
                ed.SelectionLength = _lastSearch.Length;
                ed.Focus(FocusState.Programmatic);
                _lastSearchPos = idx;
                FindResultText.Text = string.Empty;
            }
            else
            {
                FindResultText.Text = "No results";
            }
        }

        private void FindPrev()
        {
            if (string.IsNullOrEmpty(_lastSearch) || ActiveEditor is not { } ed) return;

            string text = ed.Text;
            var comparison = MatchCaseBtn.IsChecked == true
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;

            int start = _lastSearchPos - 1;
            if (start < 0) start = text.Length - 1;

            int idx = FindOccurrence(text, _lastSearch, start, forward: false, comparison);
            if (idx == -1 && start < text.Length - 1)
                idx = FindOccurrence(text, _lastSearch, text.Length - 1, forward: false, comparison);

            if (idx != -1)
            {
                ed.SelectionStart = idx;
                ed.SelectionLength = _lastSearch.Length;
                ed.Focus(FocusState.Programmatic);
                _lastSearchPos = idx;
                FindResultText.Text = string.Empty;
            }
            else
            {
                FindResultText.Text = "No results";
            }
        }

        private static int FindOccurrence(string text, string search, int from, bool forward, StringComparison comparison)
        {
            if (RegexOn())
            {
                // Regex mode is handled separately; fall through to literal
            }
            return forward
                ? (from < text.Length ? text.IndexOf(search, from, comparison) : -1)
                : (from >= 0 ? text.LastIndexOf(search, from, comparison) : -1);
        }

        private static bool RegexOn() => false; // placeholder — wire up to RegexBtn.IsChecked

        private void ReplaceBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor is not { } ed) return;
            _lastSearch = FindBox.Text;
            if (ed.SelectedText.Equals(_lastSearch, StringComparison.CurrentCultureIgnoreCase))
                ed.SelectedText = ReplaceBox.Text;
            FindNext();
        }

        private void ReplaceAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ActiveEditor is not { } ed || string.IsNullOrEmpty(FindBox.Text)) return;
            var comparison = MatchCaseBtn.IsChecked == true
                ? StringComparison.CurrentCulture
                : StringComparison.CurrentCultureIgnoreCase;
            ed.Text = ReplaceAll(ed.Text, FindBox.Text, ReplaceBox.Text, comparison);
        }

        private static string ReplaceAll(string text, string find, string replace, StringComparison comp)
        {
            int idx;
            while ((idx = text.IndexOf(find, 0, comp)) >= 0)
                text = text.Remove(idx, find.Length).Insert(idx, replace);
            return text;
        }

        //
        // Recent files
        //
        private void UpdateRecentFilesMenu()
        {
            RecentFilesMenu.Items.Clear();
            var files = RecentFilesManager.GetRecentFiles();
            if (files.Count == 0)
            {
                var empty = new MenuFlyoutItem { Text = "(No recent files)", IsEnabled = false };
                RecentFilesMenu.Items.Add(empty);
                return;
            }

            for (int i = 0; i < files.Count; i++)
            {
                string path = files[i];
                var mi = new MenuFlyoutItem
                {
                    Text = $"{i + 1}. {System.IO.Path.GetFileName(path)}",
                    Tag = path
                };
                ToolTipService.SetToolTip(mi, path);
                mi.Click += RecentFileItem_Click;
                RecentFilesMenu.Items.Add(mi);
            }
        }

        private async void RecentFileItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as MenuFlyoutItem)?.Tag is not string path) return;
            if (!System.IO.File.Exists(path))
            {
                await ShowInfoAsync("File not found", $"'{path}' could not be found. It may have been moved or deleted.");
                UpdateRecentFilesMenu();
                return;
            }

            var item = CreateTab(filePath: path);
            await LoadIntoTabAsync(item, path);
            UpdateRecentFilesMenu();
        }

        private async Task ShowInfoAsync(string title, string message)
        {
            var dlg = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dlg.ShowAsync();
        }

        private async void ClearRecentMenu_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new ContentDialog
            {
                Title = "Clear recent files",
                Content = "Are you sure you want to clear the recent files list?",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.Content.XamlRoot
            };
            if (await dlg.ShowAsync() == ContentDialogResult.Primary)
            {
                RecentFilesManager.ClearRecentFiles();
                UpdateRecentFilesMenu();
            }
        }

        //
        // Session save/restore
        //
        private void SaveSession()
        {
            var session = new SessionData
            {
                ActiveTabIndex = TabControl.SelectedIndex
            };

            foreach (var (_, data) in _tabs)
            {
                session.Tabs.Add(new SessionEntry
                {
                    IsNew = !data.Document.IsSaved,
                    FilePath = data.Document.FileName,
                    UnsavedContent = data.Document.IsModified ? data.Document.Contents : string.Empty
                });
            }

            SessionService.Save(session);
        }

        private bool RestoreSession()
        {
            var session = SessionService.Load();
            if (session is null || session.Tabs.Count == 0) return false;

            foreach (var entry in session.Tabs)
            {
                if (entry.IsNew)
                    CreateTab(initialContent: entry.UnsavedContent);
                else
                    CreateTab(filePath: entry.FilePath);
            }

            // Restore the active tab index (clamp to valid range)
            int idx = Math.Clamp(session.ActiveTabIndex, 0, TabControl.TabItems.Count - 1);
            if (TabControl.TabItems.Count > 0)
                TabControl.SelectedIndex = idx;

            // Now load file content for saved tabs
            foreach (var (item, data) in _tabs)
            {
                if (data.Document.IsSaved && !string.IsNullOrEmpty(data.Document.FileName))
                {
                    _ = LoadIntoTabAsync(item, data.Document.FileName);
                }
            }

            return true;
        }

        private async Task LoadIntoTabAsync(TabViewItem item, string path)
        {
            if (!_tabs.TryGetValue(item, out var data)) return;
            await FileService.OpenFromPathAsync(path, data.Document);
            DispatcherQueue.TryEnqueue(() =>
            {
                data.Editor.Text = data.Document.Contents;
                if (item == _activeItem) UpdateStatusBar();
            });
        }

        //
        // Tab management
        //

        private void TabControl_AddTabButtonClick(TabView sender, object args) => CreateTab();

        private async void TabControl_TabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
        {
            if (args.Tab is TabViewItem item)
                await CloseTabAsync(item);
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TabControl.SelectedItem is TabViewItem item)
                ActivateTab(item);
        }

        private TabViewItem CreateTab(string? filePath = null, string? initialContent = null)
        {
            var data = new TabData();
            if (filePath is not null)
            {
                data.Document.FileName = filePath;
                data.Document.IsSaved = true;
            }
            if (initialContent is not null)
            {
                data.Document.Contents = initialContent;
                data.Document.IsModified = initialContent.Length > 0;
            }

            data.Editor = BuildEditor(data);

            var item = new TabViewItem
            {
                Header = data.Document.DisplayName,
                Content = null,   // content shown separately in TextBoxContainer
                Tag = data
            };

            data.Document.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(DocumentTab.DisplayName))
                    DispatcherQueue.TryEnqueue(() => item.Header = data.Document.DisplayName);
            };

            _tabs[item] = data;
            TabControl.TabItems.Add(item);
            TabControl.SelectedItem = item;
            return item;
        }

        private TextBox BuildEditor(TabData data)
        {
            var editor = new TextBox
            {
                AcceptsReturn = true,
                TextWrapping = Settings.WordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
                FontFamily = new FontFamily(Settings.FontFamily),
                FontSize = Settings.FontSize,
                FontWeight = Settings.FontBold ? FontWeights.Bold : FontWeights.Normal,
                FontStyle = Settings.FontItalic ? FontStyle.Italic : FontStyle.Normal,
                IsSpellCheckEnabled = Settings.SpellCheck,
                IsTextPredictionEnabled = Settings.Autocorrect && Settings.SpellCheck,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(-3, 0, -3, 0),
                Text = data.Document.Contents
            };
            editor.Resources["TextControlBorderThemeThicknessFocused"] = new Thickness(1, 1, 1, 0);
            editor.Resources["TextControlBorderThemeThickness"] = new Thickness(1, 1, 1, 0);

            editor.TextChanging += Editor_TextChanging;
            editor.SelectionChanging += Editor_SelectionChanging;
            editor.ContextMenuOpening += Editor_ContextMenuOpening;

            return editor;
        }

        private void Editor_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            // Find the document for this editor
            foreach (var (_, data) in _tabs)
            {
                if (!ReferenceEquals(data.Editor, sender)) continue;
                data.Document.Contents = sender.Text;
                data.Document.IsModified = true;
                break;
            }

            if (ReferenceEquals(sender, ActiveEditor))
            {
                UpdateCount();
                DispatcherQueue.TryEnqueue(() => UpdatePosition());
            }
        }

        private void Editor_SelectionChanging(TextBox sender, TextBoxSelectionChangingEventArgs args)
        {
            if (ReferenceEquals(sender, ActiveEditor))
                UpdatePosition(args.SelectionStart);
        }

        private void Editor_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            e.Handled = true;
            if (sender is not TextBox ed) return;

            var menu = new MenuFlyout();

            void Add(string text, Action action, bool enabled = true)
            {
                var item = new MenuFlyoutItem { Text = text, IsEnabled = enabled };
                item.Click += (_, _) => action();
                menu.Items.Add(item);
            }

            bool hasSelection = !string.IsNullOrEmpty(ed.SelectedText);
            Add("Undo", () => { if (ed.CanUndo) ed.Undo(); }, ed.CanUndo);
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Cut", () => ed.CutSelectionToClipboard(), hasSelection);
            Add("Copy", () => ed.CopySelectionToClipboard(), hasSelection);
            Add("Paste", () => ed.PasteFromClipboard());
            Add("Delete", () => { if (hasSelection) ed.SelectedText = string.Empty; }, hasSelection);
            menu.Items.Add(new MenuFlyoutSeparator());
            Add("Select all", () => { ed.SelectAll(); ed.Focus(FocusState.Programmatic); });

            menu.ShowAt(ed, new FlyoutShowOptions
            {
                Position = new Point(e.CursorLeft, e.CursorTop)
            });
        }

        private void ActivateTab(TabViewItem item)
        {
            TextBoxContainer.Children.Clear();
            _activeItem = item;

            if (_tabs.TryGetValue(item, out var data))
            {
                TextBoxContainer.Children.Add(data.Editor);
                data.Editor.Focus(FocusState.Programmatic);
                UpdateStatusBar();
            }
        }

        private async Task<bool> CloseTabAsync(TabViewItem item)
        {
            if (!_tabs.TryGetValue(item, out var data)) return true;

            if (data.Document.IsModified)
            {
                var dlg = new ContentDialog
                {
                    Title = "Save changes?",
                    Content = $"Do you want to save changes to {data.Document.ShortName}?",
                    PrimaryButtonText = "Save",
                    SecondaryButtonText = "Don't save",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = this.Content.XamlRoot
                };
                var result = await dlg.ShowAsync();
                if (result == ContentDialogResult.None) return false;       // Cancel
                if (result == ContentDialogResult.Primary)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                    if (!await FileService.SaveAsync(hwnd, data.Document)) return false;
                }
            }

            TabControl.TabItems.Remove(item);
            TextBoxContainer.Children.Remove(data.Editor);
            _tabs.Remove(item);

            if (item == _activeItem)
                _activeItem = null;

            // If last tab closed, open a blank one
            if (TabControl.TabItems.Count == 0)
                CreateTab();

            return true;
        }

        private void EncodingButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void LineEndingButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {

        }

        //
        // Settings
        //
        private void SettingsButton_Click(object sender, RoutedEventArgs e) => ShowSettings();

        private void ShowSettings()
        {
            MainAppGrid.Visibility = Visibility.Collapsed;
            SettingsGrid.Visibility = Visibility.Visible;
            SetTitleBar(SettingsDragRegion);
        }

        private void ApplySettingsUI()
        {
            if (ThemeRadioButtons != null)
            {
                SystemRadioButton.IsChecked = true; // default
                if (Settings.Theme == 0) LightRadioButton.IsChecked = true;
                else if (Settings.Theme == 1) DarkRadioButton.IsChecked = true;
                else SystemRadioButton.IsChecked = true;
            }

            StatusBarMenu.IsChecked = Settings.ShowStatusBar;
            StatusBarToggle.IsOn = Settings.ShowStatusBar;
            StatusBarGrid.Visibility = Settings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;

            WordWrapMenu.IsChecked = Settings.WordWrap;
            WordWrapToggle.IsOn = Settings.WordWrap;

            SpellCheckToggle.IsOn = Settings.SpellCheck;
            AutocorrectToggle.IsOn = Settings.Autocorrect;
            AutocorrectToggle.IsEnabled = Settings.SpellCheck;

            ZoomText.Text = $"{Settings.ZoomLevel:0}%";

            // Font combos
            var fonts = new List<string>
        {
            "Arial","Calibri","Cascadia Code","Cascadia Mono","Comic Sans MS","Consolas",
            "Courier New","Georgia","Lucida Console","Segoe UI","Times New Roman","Trebuchet MS",
            "Verdana"
        };
            FontFamilyCombo.ItemsSource = fonts;
            FontFamilyCombo.SelectedItem = fonts.Contains(Settings.FontFamily) ? Settings.FontFamily : fonts[0];

            FontStyleCombo.SelectedIndex = (Settings.FontBold, Settings.FontItalic) switch
            {
                (true, true) => 3,
                (true, false) => 2,
                (false, true) => 1,
                _ => 0
            };

            var sizes = new List<double> { 8, 9, 10, 11, 12, 14, 16, 18, 20, 22, 24, 26, 28, 36, 48, 72 };
            FontSizeCombo.ItemsSource = sizes;
            FontSizeCombo.SelectedItem = sizes.Contains(Settings.FontSize) ? Settings.FontSize : (object)14.0;

            UpdateFontPreview();
        }

        private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsGrid.Visibility = Visibility.Collapsed;
            MainAppGrid.Visibility = Visibility.Visible;
            SetTitleBar(TabControl);
            ActiveEditor?.Focus(FocusState.Programmatic);
        }

        private void ApplyTheme(int theme)
        {
            if (this.Content is FrameworkElement root)
                root.RequestedTheme = theme switch
                {
                    0 => ElementTheme.Light,
                    1 => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
        }

        private void ThemeRadio_Click(object sender, RoutedEventArgs e)
        {
            int theme = ReferenceEquals(sender, LightRadioButton) ? 0 : ReferenceEquals(sender, DarkRadioButton) ? 1 : 2;
            Settings.Theme = theme;
            ApplyTheme(theme);
            _settingsService.Save();
        }

        private void FontStyleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = FontStyleCombo.SelectedIndex;
            Settings.FontBold = idx == 2 || idx == 3;
            Settings.FontItalic = idx == 1 || idx == 3;
            ApplyFontToAllTabs();
            UpdateFontPreview();
            _settingsService.Save();
        }

        private void FontFamilyCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontFamilyCombo.SelectedItem is string family)
            {
                Settings.FontFamily = family;
                ApplyFontToAllTabs();
                UpdateFontPreview();
                _settingsService.Save();
            }
        }

        private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FontSizeCombo.SelectedItem is double size)
            {
                Settings.FontSize = size;
                // ApplyZoom(); // re-applies font size with current zoom
                UpdateFontPreview();
                _settingsService.Save();
            }
        }

        private void UpdateFontPreview()
        {
            FontPreviewText.FontFamily = new FontFamily(Settings.FontFamily);
            FontPreviewText.FontSize = Settings.FontSize;
            FontPreviewText.FontWeight = Settings.FontBold ? FontWeights.Bold : FontWeights.Normal;
            FontPreviewText.FontStyle = Settings.FontItalic ? FontStyle.Italic : FontStyle.Normal;
        }

        private void ApplyFontToAllTabs()
        {
            double scale = Settings.ZoomLevel / 100.0;
            var family = new FontFamily(Settings.FontFamily);
            var weight = Settings.FontBold ? FontWeights.Bold : FontWeights.Normal;
            var style = Settings.FontItalic ? FontStyle.Italic : FontStyle.Normal;
            foreach (var (_, data) in _tabs)
            {
                data.Editor.FontFamily = family;
                data.Editor.FontWeight = weight;
                data.Editor.FontStyle = style;
                data.Editor.FontSize = Settings.FontSize * scale;
            }
        }

        private void StatusBarToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool show = StatusBarToggle.IsOn;
            Settings.ShowStatusBar = show;
            StatusBarMenu.IsChecked = show;
            StatusBarGrid.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            _settingsService.Save();
        }

        private void WordWrapToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool wrap = WordWrapToggle.IsOn;
            Settings.WordWrap = wrap;
            WordWrapMenu.IsChecked = wrap;
            ApplyWordWrapToAllTabs(wrap);
            _settingsService.Save();
        }

        private void SpellCheckToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool on = SpellCheckToggle.IsOn;
            Settings.SpellCheck = on;
            AutocorrectToggle.IsEnabled = on;
            if (!on) { AutocorrectToggle.IsOn = false; Settings.Autocorrect = false; }
            foreach (var (_, data) in _tabs)
            {
                data.Editor.IsSpellCheckEnabled = on;
                data.Editor.IsTextPredictionEnabled = on && Settings.Autocorrect;
            }
            _settingsService.Save();
        }

        private void AutocorrectToggle_Toggled(object sender, RoutedEventArgs e)
        {
            bool on = AutocorrectToggle.IsOn;
            Settings.Autocorrect = on;
            foreach (var (_, data) in _tabs)
                data.Editor.IsTextPredictionEnabled = on && Settings.SpellCheck;
            _settingsService.Save();
        }

        // 
        // Window close
        // 
        private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs e)
        {
            e.Cancel = true;
            await TryExitAsync();
        }

        private async Task TryExitAsync()
        {
            // Try to close every tab in order; stop if user cancels
            foreach (var item in new List<TabViewItem>([.. _tabs.Keys]))
            {
                if (!await CloseTabAsync(item)) return;
            }
            SaveSession();
            SaveWindowSettings();
            Application.Current.Exit();
        }

        // 
        // App window settings
        // 

        private void ApplyWindowSettings()
        {
            if (_appWindow is null) return;
            if (Settings.WindowLeft >= 0 && Settings.WindowTop >= 0)
                _appWindow.Move(new Windows.Graphics.PointInt32(Settings.WindowLeft, Settings.WindowTop));
            _appWindow.Resize(new Windows.Graphics.SizeInt32(Settings.WindowWidth, Settings.WindowHeight));
            if (Settings.WindowMaximized && _appWindow.Presenter is OverlappedPresenter op)
                op.Maximize();
        }

        private void SaveWindowSettings()
        {
            if (_appWindow is null) return;
            bool max = _appWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Maximized };
            Settings.WindowMaximized = max;
            if (!max)
            {
                Settings.WindowLeft = _appWindow.Position.X;
                Settings.WindowTop = _appWindow.Position.Y;
                Settings.WindowWidth = _appWindow.Size.Width;
                Settings.WindowHeight = _appWindow.Size.Height;
            }
            _settingsService.Save();
        }
    }
}
