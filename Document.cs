using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinUIpad
{
    public partial class Document : INotifyPropertyChanged
    {
        private bool _textHasChanged;

        public bool TextHasChanged
        {
            get => _textHasChanged;
            set
            {
                if (_textHasChanged != value)
                {
                    _textHasChanged = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _documentIsSaved;

        public bool DocumentIsSaved
        {
            get => _documentIsSaved;
            set
            {
                if (_documentIsSaved != value)
                {
                    _documentIsSaved = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _fileName;

        public string? FileName
        {
            get => _fileName;
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged();
                }
            }
        }

        private string? _contents;
        public string? Contents
        {
            get => _contents;
            set
            {
                if (_contents != value)
                {
                    _contents = value;
                    OnPropertyChanged();
                }
            }
        }

        public void ResetDocument(App app)
        {
            FileName = "Untitled.txt";
            Contents = "";
            TextHasChanged = false;
            DocumentIsSaved = false;
            app.AppCanBeClosed = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
