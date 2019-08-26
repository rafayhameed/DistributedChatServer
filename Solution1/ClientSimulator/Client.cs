using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientSimulator
{
    class Client
    {
        private int clientID;
        private byte[] clientIDmsg;
        private byte[] pendingMsg = null;
        private TcpClient clientSocket;
        private string serverIP;
        private int port;
        private Log log = Log.GetLog;

        private void ConnectToServer()
        {
            try
            {
                NetworkStream networkStream = default(NetworkStream);
                clientSocket = new TcpClient();
                clientSocket.Connect(serverIP, port);
                //clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, 5);
                clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                ClientIDMsg = Encoding.ASCII.GetBytes(clientID + "$");
                networkStream = clientSocket.GetStream();
                networkStream.Write(ClientIDMsg, 0, ClientIDMsg.Length);
                ClientIDMsg = null;
                networkStream.Flush();
                log.Write(" CLIENT " + clientID + " Connected to " + serverIP + ":" + port);
                Listen();
            }
            catch (Exception ex)
            {
                log.Write(ex.ToString());
            }

        }

        public void StartClient()
        {
            ConnectToServer();
            // Thread ctThread = new Thread(Listen);
            // ctThread.Start();
        }
        public async void Listen()
        {
            byte[] bytesFrom;
            NetworkStream networkStream = null;
            int count = 0;
            try
            {
                while (Constants.keepRunning)
                {
                    int msgSize = 0;
                    int startIndex = 0;
                    int msgIndex = 0;
                    int remainingSize;
                    Byte[] sizeBuffer = new Byte[4];
                    networkStream = clientSocket.GetStream();
                    await networkStream.ReadAsync(sizeBuffer, 0, 4);

                    using (MemoryStream mem = new MemoryStream(sizeBuffer))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgSize = BR.ReadInt32();
                        }
                    }
                    remainingSize = msgSize;
                    bytesFrom = new byte[msgSize];
                    while (msgIndex < msgSize)
                    {
                        msgIndex += await networkStream.ReadAsync(bytesFrom, startIndex, remainingSize);
                        if (msgIndex > 0 && msgIndex < msgSize)
                        {
                            remainingSize = msgSize - msgIndex;
                            startIndex = msgIndex;
                            continue;
                        }
                        else if (0 == msgIndex)
                        {
                            log.Write("closing connection for " + clientID);
                            networkStream.Close();
                            clientSocket.Close();
                            return;
                        }
                    }
                    count++;
                    log.Write("Message No " + count + " Received by " + clientID);
                    Interlocked.Increment(ref Simulator.receiverCounter);
                    networkStream.Flush();
                }
            }
            catch (IOException)
            {
                log.Write("Connection of client " + clientID + " has been closed attempting to reconnect for " + clientID + " EXCEP");
                networkStream.Close();
                clientSocket.Close();
                if (!Simulator.DisconnectedClientList.Contains(clientID))
                {
                    lock (Simulator.DisconnectedClientList)
                        Simulator.DisconnectedClientList.Add(clientID);
                    Simulator.ReconnectClient(clientID);
                }

            }
            catch (ObjectDisposedException)
            {
                log.Write("Connection of client " + clientID + " has been closed attempting to reconnect for " + clientID + " EXCEP");
                networkStream.Close();
                clientSocket.Close();
                if (!Simulator.DisconnectedClientList.Contains(clientID))
                {
                    lock (Simulator.DisconnectedClientList)
                        Simulator.DisconnectedClientList.Add(clientID);
                    Simulator.ReconnectClient(clientID);
                }
            }
            catch (Exception ex)
            {
                log.Write("Exception Occurred in Listen Function for " + clientID + ex);
                Console.WriteLine("Exception Occurred in Listen Function for " + clientID + ex);
                networkStream.Close();
                clientSocket.Close();
                if (!Simulator.DisconnectedClientList.Contains(clientID))
                {
                    lock (Simulator.DisconnectedClientList)
                        Simulator.DisconnectedClientList.Add(clientID);
                    Simulator.ReconnectClient(clientID);
                }
            }

        }

        public int ClientID { get => clientID; set => clientID = value; }
        public byte[] ClientIDMsg { get => clientIDmsg; set => clientIDmsg = value; }
        public TcpClient ClientSocket { get => clientSocket; set => clientSocket = value; }
        public string ServerIP { get => serverIP; set => serverIP = value; }
        public int Port { get => port; set => port = value; }
        public byte[] PendingMsg { get => pendingMsg; set => pendingMsg = value; }
    }
}
