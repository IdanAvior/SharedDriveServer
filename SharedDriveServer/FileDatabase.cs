using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedDriveLibrary
{
    [Serializable]
    public class FileDatabase
    {
        public string FolderPath { get; }
        public List<FileEntry> FileEntries { get; set; }

        public FileDatabase(string path)
        {
            FolderPath = path;
            FileEntries = new List<FileEntry>();
            PopulateFileEntries();
        }

        public byte[] GetFile(string filename)
        {
            return File.ReadAllBytes(FolderPath + filename);
        }

        public byte[] GetFileDataAtV2(FileDownloadSession session, int from, int to)
        {
            var data_buf = new byte[to - from + 1];
            session.GoTo(from, SeekOrigin.Begin);
            session.CopyFileDataToBuffer(data_buf);
            return data_buf;
        }

        public int GetFileLength(string filename)
        {
            return FileEntries.Where(f => f.Name == filename).First().Size;
        }

        public bool DeleteFile(string filename)
        {
            try
            {
                var fileEntry = FileEntries.Where(f => f.Name == filename).First();
                lock (fileEntry)
                {
                    fileEntry.ToBeDeleted = true;
                }
                while (fileEntry.CurrentUses > 0)
                {
                    System.Threading.Thread.Sleep(500);
                }
                var dirInfo = new DirectoryInfo(FolderPath);
                var files = dirInfo.GetFiles();
                files.Where(f => f.Name == filename).First().Delete();                 
                FileEntries.Remove(fileEntry);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void PopulateFileEntries()
        {
            var files = Directory.GetFiles(FolderPath);
            foreach (var file in files)
            {
                AddToFileEntries(file);
            }
        }

        private void AddToFileEntries(string file)
        {
            var size = (int)new FileInfo(file).Length;
            var filename = file.Substring(file.LastIndexOf('\\') + 1);
            FileEntries.Add(new FileEntry { Name = filename, Size = size });
        }
    }
}
