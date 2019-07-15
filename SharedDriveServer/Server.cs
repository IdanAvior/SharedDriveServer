using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using SharedDriveLibrary;
using System.Windows;
using static SharedDriveLibrary.ValueDefinitions;

namespace SharedDriveServer
{
    public class NowListeningEventArgs : EventArgs
    {
        public int PortNumber { get; set; }
    }

    public class NewFileUploadedEventArgs : EventArgs
    {
        public string FileName { get; set; }
        public int FileSize { get; set; }
    }

    public class FileDeletedEventArgs : EventArgs
    {
        public string FileName { get; set; }
    }


    class Server : INotifyPropertyChanged
    {
        private const int BlockSize = ValueDefinitions.BlockSize;
        private string path;
        private int port;
        private TcpListener listener;
        public string IPAddr { get; }
        public int Port
        {
            get
            {
                return port;
            }
            set
            {
                if (port != value)
                {
                    port = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Port"));
                }
            }
        }
        public FileDatabase FileDB;
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<NowListeningEventArgs> NowListening;
        public event EventHandler<NewFileUploadedEventArgs> NewFileUploaded;
        public event EventHandler<FileDeletedEventArgs> FileDeleted;

        public Server(string folder_path, int port_num = ServerPortNumber)
        {
            path = folder_path;
            IPAddr = ServerIPAddress;
            Port = port_num;
            listener = null;
            FileDB = new FileDatabase(folder_path);
        }

        public Task Listen()
        {
            return Task.Factory.StartNew(() =>
            {
                var addr = IPAddress.Parse(IPAddr);
                listener = new TcpListener(addr, Port);
                listener.Start();
                OnNowListening(new NowListeningEventArgs { PortNumber = Port });
                while (true)
                {
                    // Listen for requests
                    var socket = listener.AcceptSocket();
                    if (socket.Connected)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            var networkStream = new NetworkStream(socket);
                            var data_buffer = new byte[sizeof(int)];
                            var bytes_read = networkStream.Read(data_buffer, 0, data_buffer.Length);
                            var req_type = BitConverter.ToInt32(data_buffer, 0);
                            EvaluateAndExecuteRequest(req_type, networkStream);
                        });
                    }
                }
            });
        }

        public void StopListening()
        {
            listener.Stop();
        }

        private void HandleFileUpload(NetworkStream networkStream)
        {
            var header_len_bytes = new byte[sizeof(int)];
            networkStream.Read(header_len_bytes, 0, header_len_bytes.Length);
            var header_len = BitConverter.ToInt32(header_len_bytes, 0);
            var header_bytes = new byte[header_len];
            networkStream.Read(header_bytes, 0, header_bytes.Length);
            var header = (ClientUploadFileHeader)SerializationMethods.Deserialize(header_bytes);
            var session = new FileUploadSession(FileDB, header.Filename);
            var totalBytesRead = 0;
            while (totalBytesRead < header.FileSize)
            {
                var data = new byte[BlockSize];
                var bytesRead = networkStream.Read(data, 0, data.Length);
                session.WriteToFile(data, bytesRead);
                totalBytesRead += bytesRead;
            }
            session.TerminateSession();
            FileDB.FileEntries.Add(new FileEntry { Name = header.Filename, Size = header.FileSize });
            var response_buffer = BitConverter.GetBytes((int)ResponeType.Success);
            networkStream.Write(response_buffer, 0, sizeof(int));
            NewFileUploaded?.Invoke(this, new NewFileUploadedEventArgs { FileName = header.Filename, FileSize = header.FileSize });
        }

        private void EvaluateAndExecuteRequest(int request, NetworkStream networkStream)
        {
            try
            {
                switch (request)
                {
                    case (int)RequestType.GetList:
                        SendFilesList(networkStream);
                        break;
                    case (int)RequestType.UploadFile:
                        HandleFileUpload(networkStream);
                        break;
                    case (int)RequestType.DownloadFile:
                        HandleFileDownload(networkStream);
                        break;
                    case (int)RequestType.DeleteFile:
                        HandleFileDeletion(networkStream);
                        break;
                    default:
                        throw new Exception("Invalid request");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

        }

        private void HandleFileDeletion(NetworkStream networkStream)
        {
            var filename_length_bytes = new byte[sizeof(int)];
            networkStream.Read(filename_length_bytes, 0, filename_length_bytes.Length);
            var filename_length = BitConverter.ToInt32(filename_length_bytes, 0);
            var filename_bytes = new byte[filename_length];
            networkStream.Read(filename_bytes, 0, filename_bytes.Length);
            var filename = Encoding.ASCII.GetString(filename_bytes, 0, filename_bytes.Length);
            var success = FileDB.DeleteFile(filename);
            byte[] response_buffer;
            if (success)
            {
                response_buffer = BitConverter.GetBytes((int)ResponeType.Success);
            }
            else
            {
                response_buffer = BitConverter.GetBytes((int)ResponeType.Failure);
            }
            networkStream.Write(response_buffer, 0, response_buffer.Length);
            FileDeleted?.Invoke(this, new FileDeletedEventArgs() { FileName = filename });
        }


        private void HandleFileDownload(NetworkStream networkStream)
        {
            // Get client download file header
            var clientHeader = GetClientDownloadFileHeader(networkStream);
            // Check file availability
            var query = FileDB.FileEntries.Where(f => f.Name == clientHeader.Filename);
            if (query.Count() < 1 || query.First().ToBeDeleted)
            {
                var negativeResponseBytes = BitConverter.GetBytes((int)ResponeType.Failure);
                networkStream.Write(negativeResponseBytes, 0, negativeResponseBytes.Length);
                return;
            }
            else
            {
                ++query.First().CurrentUses;
                var positiveResponseBytes = BitConverter.GetBytes((int)ResponeType.Success);
                networkStream.Write(positiveResponseBytes, 0, positiveResponseBytes.Length);
            }
            int fileSize = FileDB.GetFileLength(clientHeader.Filename);
            int numBlocks = fileSize / BlockSize;
            int lastBlockSize = fileSize % BlockSize;
            var serverHeader = new ServerDownloadFileHeader()
            {
                FileSize = fileSize,
                NumBlocks = numBlocks,
                LastBlockSize = lastBlockSize
            };
            var serverHeaderBytes = SerializationMethods.Serialize(serverHeader);
            var serverHeaderLengthBytes = BitConverter.GetBytes(serverHeaderBytes.Length);
            networkStream.Write(serverHeaderLengthBytes, 0, serverHeaderLengthBytes.Length);
            networkStream.Write(serverHeaderBytes, 0, serverHeaderBytes.Length);
            var downloadSession = new FileDownloadSession(FileDB, clientHeader.Filename);
            for (int i = 0; i < numBlocks; i++)
            {
                var fileData = FileDB.GetFileDataAtV2(downloadSession, i * BlockSize, (i + 1) * BlockSize - 1);
                networkStream.Write(fileData, 0, fileData.Length);
            }
            if (lastBlockSize > 0)
            {
                var file_data = FileDB.GetFileDataAtV2(downloadSession, numBlocks * BlockSize, fileSize - 1);
                networkStream.Write(file_data, 0, file_data.Length);
            }
            downloadSession.TerminateSession();
            --query.First().CurrentUses;
        }

        private static ClientDownloadFileHeader GetClientDownloadFileHeader(NetworkStream networkStream)
        {
            var headerLengthBytes = new byte[sizeof(int)];
            networkStream.Read(headerLengthBytes, 0, headerLengthBytes.Length);
            var headerLength = BitConverter.ToInt32(headerLengthBytes, 0);
            var headerBytes = new byte[headerLength];
            networkStream.Read(headerBytes, 0, headerBytes.Length);
            var header = (ClientDownloadFileHeader)SerializationMethods.Deserialize(headerBytes);
            return header;
        }

        private void SendFilesList(NetworkStream networkStream)
        {
            var data = SerializationMethods.Serialize(FileDB.FileEntries);
            var length = BitConverter.GetBytes(data.Length);
            networkStream.Write(length, 0, sizeof(int));
            networkStream.Write(data, 0, data.Length);
        }

        protected void OnNowListening(NowListeningEventArgs e)
        {
            NowListening?.Invoke(this, e);
        }

    }
}
