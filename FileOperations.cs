using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using Windows.Storage;

namespace WinUIpad
{
    public partial class FileOperations
    {
        public FileOperations()
        {

        }

        private async Task<ContentDialogResult> DisplayConfirmationDialog(MainWindow mw, String filename)
        {
            ContentDialog dialog = new ContentDialog()
            {
                XamlRoot = mw.Content.XamlRoot,
                Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style,
                Title = "WinUIpad",
                Content = "Do you want to save changes to " + filename,
                PrimaryButtonText = "Save",
                SecondaryButtonText = "Don't save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            return await dialog.ShowAsync();
        }

        public async void OpenFile(MainWindow mw, Document d)
        {
            var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
            filePicker.FileTypeFilter.Add(".txt");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(mw);
            WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

            StorageFile file = await filePicker.PickSingleFileAsync();
            if (file != null)
            {
                d.FileName = file.Path;
                d.Contents = await Windows.Storage.FileIO.ReadTextAsync(file);
                d.TextHasChanged = false;
                d.DocumentIsSaved = true;
            }
            if (file != null) 
                d.FileName = file.Path;
            else
                d.FileName = "Untitled.txt";
        }

        public async Task<bool> NeedsToBeSavedAsync(MainWindow mw, Document d)
        {
            // true: Continue
            // false: Cancel
            if (d.TextHasChanged)
            {
                // The document needs to be saved
                if (d.FileName != null)
                {
                    ContentDialogResult result = await DisplayConfirmationDialog(mw, d.FileName);
                    switch (result)
                    {
                        // Save
                        case ContentDialogResult.Primary:
                            if (await SaveDocument(mw, d) == true)
                                return true;
                            break;
                        // Don't save
                        case ContentDialogResult.Secondary:
                            return true;
                        default:
                            return false;
                    }
                }
            }
            // If nothing needs to be done, just continue normally
            return true;
        }

        public async Task<bool> SaveDocument(MainWindow mw, Document d)
        {
            // true: Success, continue
            // false: Cancel

            if (d.DocumentIsSaved)
            {
                // Save existing file
                if (d.FileName != null)
                {
                    if (File.Exists(Path.GetFullPath(d.FileName)))
                    {
                        File.WriteAllText(d.FileName, d.Contents);
                        d.TextHasChanged = false;
                        d.DocumentIsSaved = true;
                        return true;
                    }
                    else { return false; }
                }
                else { return false; }
            }
            else
            {
                // Save as
                if (d != null) { return await FileOperations.SaveAsDocument(WinRT.Interop.WindowNative.GetWindowHandle(mw), d); }
                else { return false; }
            }
        }

        public static async Task<bool> SaveAsDocument(nint hwnd, Document d)
        {
            var savePicker = new Windows.Storage.Pickers.FileSavePicker();
            // savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
            savePicker.SuggestedFileName = Path.GetFileNameWithoutExtension(d.FileName);
            savePicker.FileTypeChoices.Add("Text file (*.txt)", [".txt"]);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);
            try
            {
                StorageFile file = await savePicker.PickSaveFileAsync();

                if (file != null)
                {
                    await Windows.Storage.FileIO.WriteTextAsync(file, d.Contents);
                    d.FileName = file.Path;
                    d.DocumentIsSaved = true;
                    d.TextHasChanged = false;
                    return true;
                }
                // User cancelled the save operation
                else return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
