using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedDriveLibrary
{
    public class FileDownloadSession : IFileSession
    {
        private FileStream _fstream;

        public FileDownloadSession(FileDatabase fDB, string filename)
        {
            _fstream = File.OpenRead(fDB.FolderPath + filename);
        }

        public long GoTo(int offset, SeekOrigin origin)
        {
            return _fstream.Seek(offset, origin);
        }

        public int CopyFileDataToBuffer(byte[] buffer)
        {
            return _fstream.Read(buffer, 0, buffer.Length);
        }

        public void TerminateSession()
        {
            _fstream.Dispose();
        }
    }
}
