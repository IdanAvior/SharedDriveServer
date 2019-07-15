using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;
using SharedDriveLibrary;
using System.Threading;
using static SharedDriveLibrary.ValueDefinitions;

namespace SharedDriveServer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Private data members 
        private readonly string _folderPath = @"D:\Shared Files\";
        private Server _server;
        private ObservableCollection<FileEntry> _fileCollection;
        private string _mainButtonValue;
        private int _portNumber;
        // Properties
        public bool IsListening { get; set; } = false;

        public ObservableCollection<FileEntry> FileCollection
        {
            get
            {
                return _fileCollection;
            }
            set
            {
                _fileCollection = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FileCollection"));
            }
        }

        public string MainButtonText
        {
            get
            {
                return _mainButtonValue;
            }
            set
            {
                _mainButtonValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MainButtonText"));
            }
        }

        public int PortNumber
        {
            get
            {
                return ServerPortNumber;
            }

        }

        // Events
        public event PropertyChangedEventHandler PropertyChanged;

        // Constructor
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
            MainButtonText = "Listen";
            try
            {
                _server = new Server(_folderPath);
                _server.NowListening += (sender, e) => { MessageBox.Show($"Now listening on port {e.PortNumber}"); };
                _server.NewFileUploaded += (sender, e) =>
                {
                    AddOnUI(FileCollection, new FileEntry { Name = e.FileName, Size = e.FileSize });
                };
                _server.FileDeleted += (sender, e) =>
                {
                    RemoveOnUI(FileCollection, FileCollection.Where(f => f.Name == e.FileName).First());
                };
                FileCollection = new ObservableCollection<FileEntry>();
                PopulateFileCollection();
            }
            catch
            {
                MessageBox.Show("An error has occurred");
            }
        }

        // Button event handlers

        // Listen button event handler
        private void ListenButton_Click(object sender, RoutedEventArgs e)
        {
            if (!IsListening)
                StartListening();
            else
                _server.StopListening();
            IsListening = !IsListening;
            MainButtonText = IsListening ? "Stop listening" : "Listen";
        }

        //Other methods
  
        private void PopulateFileCollection()
        {
            var fileList = _server.FileDB.FileEntries;
            foreach (var file in fileList)
                FileCollection.Add(file);
        }

        private void StartListening()
        {
            try
            {
                _server.Listen();
            }
            catch(Exception e)
            {
                MessageBox.Show(e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void AddOnUI<T>(ObservableCollection<T> collection, T item)
        {
            Action<T> addMethod = collection.Add;
            Application.Current.Dispatcher.BeginInvoke(addMethod, item);
        }

        public static void RemoveOnUI<T>(ObservableCollection<T> collection, T item)
        {
            Action<ObservableCollection<T>, T> removeMethod = RemoveFromOC;
            Application.Current.Dispatcher.BeginInvoke(removeMethod, collection, item);
        }

        public static void RemoveFromOC<T>(ObservableCollection<T> collection, T item)
        {
            collection.Remove(item);
        }
    }
}
