using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WinUIpad.Helpers;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WinUIpad;

namespace WinUIpad.Services;

public static class FileService
{
    // 
    // Encoding / line-ending detection
    // 
    public static (string content, Encoding encoding, LineEnding lineEnding) Read(byte[] bytes)
    {
        Encoding encoding;
        int skip = 0;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
            skip = 3;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            encoding = Encoding.Unicode;      // UTF-16 LE
            skip = 2;
        }
        else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            encoding = Encoding.BigEndianUnicode;  // UTF-16 BE
            skip = 2;
        }
        else
        {
            encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        string content = encoding.GetString(bytes, skip, bytes.Length - skip);

        LineEnding lineEnding;
        if (content.Contains("\r\n"))      lineEnding = LineEnding.CRLF;
        else if (content.Contains('\r'))   lineEnding = LineEnding.CR;
        else                               lineEnding = LineEnding.LF;

        return (content, encoding, lineEnding);
    }

    // 
    // Open
    // 

    public static async Task<bool> OpenPickerAsync(IntPtr hwnd, DocumentTab doc)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        foreach (var ext in new[] { ".txt", ".md", ".log", ".cs", ".json", ".xml",
                                    ".html", ".htm", ".css", ".js", ".ts", ".py",
                                    ".sql", ".ini", ".cfg", ".yaml", ".yml", "*" })
            picker.FileTypeFilter.Add(ext);

        var file = await picker.PickSingleFileAsync();
        if (file == null) return false;
        return await OpenFromPathAsync(file.Path, doc);
    }

    public static async Task<bool> OpenFromPathAsync(string path, DocumentTab doc)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(path);
            var (content, encoding, lineEnding) = Read(bytes);

            doc.FileName   = path;
            doc.Contents   = content;
            doc.Encoding   = encoding;
            doc.LineEnding = lineEnding;
            doc.IsModified = false;
            doc.IsSaved    = true;

            // RecentFilesManager.AddRecentFile(path);
            return true;
        }
        catch { return false; }
    }

    // 
    // Save
    // 

    public static async Task<bool> SaveAsync(IntPtr hwnd, DocumentTab doc)
    {
        if (!doc.IsSaved || string.IsNullOrEmpty(doc.FileName))
            return await SaveAsAsync(hwnd, doc);
        return await WriteAsync(doc.FileName, doc);
    }

    public static async Task<bool> SaveAsAsync(IntPtr hwnd, DocumentTab doc)
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeChoices.Add("Text Documents", new[] { ".txt" });
        picker.FileTypeChoices.Add("All Files", new[] { "." });
        picker.SuggestedFileName = doc.IsSaved && !string.IsNullOrEmpty(doc.FileName)
            ? Path.GetFileNameWithoutExtension(doc.FileName) : "Untitled";

        var file = await picker.PickSaveFileAsync();
        if (file == null) return false;

        doc.FileName = file.Path;
        doc.IsSaved  = true;
        return await WriteAsync(file.Path, doc);
    }

    private static async Task<bool> WriteAsync(string path, DocumentTab doc)
    {
        try
        {
            // Convert internal line endings back to the document's native format
            string text = doc.Contents;
            text = doc.LineEnding switch
            {
                LineEnding.CRLF => text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\r\n"),
                LineEnding.LF   => text.Replace("\r\n", "\n").Replace("\r", "\n"),
                LineEnding.CR   => text.Replace("\r\n", "\r").Replace('\n', '\r'),
                _               => text
            };

            byte[] payload = doc.Encoding.GetBytes(text);
            byte[] bom     = doc.Encoding.GetPreamble();

            byte[] output;
            if (bom.Length > 0)
            {
                output = new byte[bom.Length + payload.Length];
                Buffer.BlockCopy(bom, 0, output, 0, bom.Length);
                Buffer.BlockCopy(payload, 0, output, bom.Length, payload.Length);
            }
            else
            {
                output = payload;
            }

            await File.WriteAllBytesAsync(path, output);
            doc.IsModified = false;
            // RecentFilesManager.AddRecentFile(path);
            return true;
        }
        catch { return false; }
    }
}
