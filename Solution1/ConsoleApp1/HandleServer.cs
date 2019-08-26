using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleApp1
{
    class HandleServer
    {
        private TcpClient serverSocket;
        private int serverID;
        HashSet<string> clientsList;
        //private List<string> clientsList;
        private Log log = Log.GetLog;
        private string serverIP;
        private int port;
        private bool connected = false;
        private bool sender;


        public void StartServer()
        {

            serverSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            //IList<string> msgSplit;
            //msgSplit = serverSocket.Client.RemoteEndPoint.ToString().Split(':');
            //ServerIP = msgSplit[0];
            //port = Int32.Parse(msgSplit[1]);
            //ServerIP = serverSocket.Client.RemoteEndPoint.ToString();
            log.Write("Starting Listening Thread for Server " + ServerIP + " : " + port);
            Console.WriteLine("Starting Listening Thread for Server " + ServerIP + " : " + port);
            if (null == clientsList)
                clientsList = new HashSet<string>();
            Connected = true;

            Thread ctThread = new Thread(Listen);
            ctThread.Start();
        }

        private void Listen()
        {
            NetworkStream networkStream = null;
            try
            {
                networkStream = serverSocket.GetStream();
                Byte[] buffer = new Byte[8];
                Byte[] sizeBuff = new Byte[4];
                Byte[] typeBuff = new Byte[4];
                int msgType, msgSize = 0, sizeIndex = 0, remainingSize = 8, startIndex = 0;
                SendMsg send = new SendMsg();

                log.Write("enterred and started listening");
                while (true)
                {
                    try
                    {
                        sizeIndex += networkStream.Read(buffer, startIndex, remainingSize);
                    }
                    catch (ObjectDisposedException)
                    {
                        log.Write("Socket Connection with Server " + serverID + " has crashed. Local try Catch");
                        lock (ServerMain.disconnectedServerList)
                        {
                            if (!ServerMain.disconnectedServerList.Contains(serverID))
                            {
                                serverSocket.Close();
                                Connected = false;
                                Console.WriteLine("adding listen");
                                ServerMain.disconnectedServerList.Add(serverID);
                                ServerMain.reconnectServerWait.Set();
                            }
                        }
                        startIndex = sizeIndex = msgSize = 0;
                        remainingSize = 8;
                        break;
                    }
                    catch (IOException)
                    {
                        log.Write("Socket Connection with Server " + serverID + " has crashed. Local try Catch");
                        lock (ServerMain.disconnectedServerList)
                        {
                            if (!ServerMain.disconnectedServerList.Contains(serverID))
                            {
                                serverSocket.Close();
                                Connected = false;
                                Console.WriteLine("adding listen");
                                ServerMain.disconnectedServerList.Add(serverID);
                                ServerMain.reconnectServerWait.Set();
                            }
                        }
                        startIndex = sizeIndex = msgSize = 0;
                        remainingSize = 8;
                        break;
                    }
                    if (sizeIndex > 0 && sizeIndex < 8)
                    {
                        remainingSize = 8 - sizeIndex;
                        startIndex = sizeIndex;
                        continue;
                    }
                    else if (sizeIndex == 0)
                    {
                        log.Write("closing connection for " + serverID);
                        networkStream.Close();
                        serverSocket.Close();
                        return;
                    }
                    Array.Copy(buffer, typeBuff, 4);
                    Array.Copy(buffer, 4, sizeBuff, 0, 4);
                    using (MemoryStream mem = new MemoryStream(typeBuff))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgType = BR.ReadInt32();
                        }
                    }
                    using (MemoryStream mem = new MemoryStream(sizeBuff))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgSize = BR.ReadInt32();
                        }
                    }
                    networkStream.Flush();
                    if (msgType == 1)
                    {
                        GetClientList(msgSize, networkStream);
                    }
                    else if (msgType == 2)
                    {
                        send.SendMsgToClient(msgSize, networkStream, true);
                    }
                    else if (msgType == 3)
                    {
                        RecRemoveClientMsg(msgSize, networkStream);
                    }

                    startIndex = sizeIndex = msgSize = 0;
                    remainingSize = 8;
                }
            }
            catch (ObjectDisposedException)
            {
                log.Write("Socket Connection with Server " + serverID + " has crashed. Going to close it EXCEP");
                Console.WriteLine("Socket Connection with Server " + serverID + " has crashed. Going to close it EXCEP");
                lock (ServerMain.disconnectedServerList)
                {
                    if (!ServerMain.disconnectedServerList.Contains(serverID))
                    {
                        serverSocket.Close();
                        Connected = false;
                        Console.WriteLine("adding listen");
                        ServerMain.disconnectedServerList.Add(serverID);
                        ServerMain.reconnectServerWait.Set();
                    }
                }


            }
            catch (IOException)
            {
                log.Write("Socket Connection with Server " + serverID + " has crashed. Going to close it EXCEP");
                Console.WriteLine("Socket Connection with Server " + serverID + " has crashed. Going to close it EXCEP");
                lock (ServerMain.disconnectedServerList)
                {
                    if (!ServerMain.disconnectedServerList.Contains(serverID))
                    {
                        serverSocket.Close();
                        Connected = false;
                        Console.WriteLine("adding listen");
                        ServerMain.disconnectedServerList.Add(serverID);
                        ServerMain.reconnectServerWait.Set();
                    }
                }

            }
            catch (Exception)
            {
                log.Write("An Exception has occured in Server Listen msg for server " + serverID + " Going to close it ");
                Console.WriteLine("Socket Connection with Server " + serverID + " has crashed. Going to close it EXCEP");
                serverSocket.Close();
            }
        }

        private void RecRemoveClientMsg(int msgSize, NetworkStream networkStream)
        {
            int msgIndex = 0;
            int startIndex, remainingSize;
            byte[] bytesFrom;
            string client;
            remainingSize = msgSize;
            bytesFrom = new byte[msgSize];
            startIndex = 0;
            try
            {
                while (msgIndex < msgSize)
                {
                    msgIndex += networkStream.Read(bytesFrom, startIndex, remainingSize);
                    if (msgIndex > 0 && msgIndex < msgSize)
                    {
                        remainingSize = msgSize - msgIndex;
                        startIndex = msgIndex;
                        continue;
                    }
                    else if (0 == msgIndex)
                    {
                        networkStream.Close();
                        return;
                    }
                    networkStream.Flush();

                    client = Encoding.ASCII.GetString(bytesFrom);
                    log.Write("Server requested so Removing ClientID " + client + " from client List for server " + ServerIP);
                    lock (ClientsList)
                        ClientsList.Remove(client);

                }
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Write("Exception in RemoveClient " + ex);
                Console.WriteLine("Exception in RemoveClient " + ex);
            }
        }


        private void GetClientList(int msgSize, NetworkStream networkStream)
        {


            int msgIndex = 0;
            int startIndex, remainingSize;
            byte[] bytesFrom;
            remainingSize = msgSize;
            bytesFrom = new byte[msgSize];
            startIndex = 0;
            try
            {
                while (msgIndex < msgSize)
                {
                    msgIndex += networkStream.Read(bytesFrom, startIndex, remainingSize);
                    if (msgIndex > 0 && msgIndex < msgSize)
                    {
                        remainingSize = msgSize - msgIndex;
                        startIndex = msgIndex;
                        continue;
                    }
                    else if (0 == msgIndex)
                    {
                        networkStream.Close();
                        return;
                    }
                    string msg = Encoding.ASCII.GetString(bytesFrom);
                    networkStream.Flush();
                    List<string> tempList = msg.Split(',').ToList();
                    tempList.RemoveAt(0);
                    //ClientsList.AddRange(tempList);
                    ClientsList.UnionWith(tempList);
                    log.Write(" .Printing servers List of clients: " + msg);

                }
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Write("Exception in SendMsgToClient " + ex);
                Console.WriteLine("Exception in SendMsgToClient " + ex);
            }
        }

        public void ReconnectServer()
        {
            log.Write("Reconnecting server " + ServerIP);
            Console.WriteLine("=================== Reconnecting server with" + ServerIP + ':' + Port);
            serverSocket = new TcpClient();
            serverSocket.Connect(ServerIP, Port);
            if (serverSocket.Connected)
            {
                log.Write("successfully connected with " + ServerIP + ":" + Port);
                Console.WriteLine("successfully connected with " + ServerIP + ":" + Port);
                Connected = true;
                StartServer();
            }
            else
            {
                log.Write("Error in connecting with " + ServerIP + ":" + Port);
                Console.WriteLine("Error in connecting with " + ServerIP + ":" + Port);
            }
        }


        public TcpClient ServerSocket { get => serverSocket; set => serverSocket = value; }
        public int ServerID { get => serverID; set => serverID = value; }
        public string ServerIP { get => serverIP; set => serverIP = value; }
        public int Port { get => port; set => port = value; }
        public HashSet<string> ClientsList { get => clientsList; set => clientsList = value; }
        public bool Connected { get => connected; set => connected = value; }
        public bool Sender { get => sender; set => sender = value; }
    }
}
