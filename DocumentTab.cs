using System.ComponentModel;
using System.Text;

namespace WinUIpad;

public enum LineEnding { CRLF, LF, CR }

public class DocumentTab : INotifyPropertyChanged
{
    private string _fileName = string.Empty;
    private string _contents = string.Empty;
    private bool _isModified = false;
    private bool _isSaved = false;
    private Encoding _encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    private LineEnding _lineEnding = LineEnding.CRLF;

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(nameof(FileName)); OnPropertyChanged(nameof(DisplayName)); OnPropertyChanged(nameof(ShortName)); }
    }

    public string Contents
    {
        get => _contents;
        set { _contents = value; OnPropertyChanged(nameof(Contents)); }
    }

    public bool IsModified
    {
        get => _isModified;
        set { _isModified = value; OnPropertyChanged(nameof(IsModified)); OnPropertyChanged(nameof(DisplayName)); }
    }

    public bool IsSaved
    {
        get => _isSaved;
        set { _isSaved = value; OnPropertyChanged(nameof(IsSaved)); }
    }

    public Encoding Encoding
    {
        get => _encoding;
        set { _encoding = value; OnPropertyChanged(nameof(Encoding)); OnPropertyChanged(nameof(EncodingName)); }
    }

    public LineEnding LineEnding
    {
        get => _lineEnding;
        set { _lineEnding = value; OnPropertyChanged(nameof(LineEnding)); OnPropertyChanged(nameof(LineEndingName)); }
    }

    public string ShortName =>
        string.IsNullOrEmpty(FileName) ? "Untitled" : System.IO.Path.GetFileName(FileName);

    // Includes the unsaved dot indicator
    public string DisplayName => IsModified ? $"● {ShortName}" : ShortName;

    public string EncodingName => _encoding.GetPreamble().Length > 0
        ? $"UTF-8 BOM"
        : _encoding.WebName.ToUpperInvariant() switch
        {
            "UTF-8"    => "UTF-8",
            "UTF-16"   => "UTF-16 LE",
            "UTF-16BE" => "UTF-16 BE",
            "US-ASCII" => "ANSI",
            _          => _encoding.WebName
        };

    public string LineEndingName => _lineEnding switch
    {
        LineEnding.CRLF => "Windows (CRLF)",
        LineEnding.LF   => "Unix (LF)",
        LineEnding.CR   => "Mac (CR)",
        _               => "Windows (CRLF)"
    };

    public void Reset()
    {
        FileName  = string.Empty;
        Contents  = string.Empty;
        IsModified = false;
        IsSaved   = false;
        Encoding  = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        LineEnding = LineEnding.CRLF;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
