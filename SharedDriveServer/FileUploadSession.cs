using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SharedDriveLibrary
{
    public class FileUploadSession : IFileSession
    {
        private FileStream _fstream;

        public FileUploadSession(FileDatabase fDB,  string filename)
        {
            _fstream = File.OpenWrite(fDB.FolderPath + filename);
        }

        public void WriteToFile(byte[] data, int count)
        {
            _fstream.Write(data, 0, count);
        }

        public void TerminateSession()
        {
            _fstream.Dispose();
        }
    }
}
