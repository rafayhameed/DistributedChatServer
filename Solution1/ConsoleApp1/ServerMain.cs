using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleApp1
{
    class ServerMain
    {
        public static Hashtable clientsList = new Hashtable();
        public static string clientListStr = null;
        public static Hashtable serverList = new Hashtable();
        private static Log log = Log.GetLog;
        public static int recCounter = 0;
        public static int sendCounter = 0;
        public static int totalRecieved = 1;
        private static string clientsIDs = null;
        private static List<string> discardedClientsList = new List<string>();
        private static Socket loadBlncSock;
        private static TcpClient tcpClient;
        //private static NetworkStream LBStream;
        private static Object lockOnMe = new Object();
        private static Object clientListLock = new Object();
        private static int sereverounter = 0;
        public static List<int> disconnectedServerList = new List<int>();
        private static ManualResetEvent clientListWait = new ManualResetEvent(false);
        public static ManualResetEvent reconnectServerWait = new ManualResetEvent(false);

        static void Main(string[] args)
        {

            ManualResetEvent allDone = new ManualResetEvent(false);
            int loadBlncPort = Constants.loadBlncPort;
            int clientPort = loadBlncPort + 1;
            int serverPort = loadBlncPort + 2;
            //string localIPStr = "127.0.0.1";
            string localIPStr = GetLocalIPAddress();
            IPAddress localIP = IPAddress.Parse(localIPStr);
            IPEndPoint localEndPoint = new IPEndPoint(localIP, loadBlncPort);
            TcpListener clientSocket = new TcpListener(localIP, clientPort);
            TcpListener serverSocket = new TcpListener(localIP, serverPort);
            string clientName = null;

            TcpClient clientTCPSocket = default(TcpClient);
            int requestCount = 0;
            try
            {
                clientSocket.Start();
                serverSocket.Start();
                Console.WriteLine("starting " + localIP + " : " + serverPort);
                Thread ServerListener = new Thread(() => FetchServers(serverSocket));
                ServerListener.Start();

                ConnectToLoadBalancer(localEndPoint);
                log.Write("Server Started listening for clients at " + localIP + ":" + clientPort);
                Thread CounterPrinterThread = new Thread(CounterPrinter)
                {
                    Priority = ThreadPriority.AboveNormal
                };
                CounterPrinterThread.Start();

                clientListWait.Reset();
                //Thread clientIDsPrinter = new Thread(LogClientIDs);
                //clientIDsPrinter.Start();

                Thread broadCastThread = new Thread(BroadcastClientList);
                broadCastThread.Start();

                Thread precessDiscServersThread = new Thread(DiscServersProcessor);
                //precessDiscServersThread.Start();

                while (true)
                {
                    requestCount = requestCount + 1;
                    clientTCPSocket = clientSocket.AcceptTcpClient();

                    NetworkStream networkStream = clientTCPSocket.GetStream();
                    byte[] bytesForm = new byte[10];
                    networkStream.Read(bytesForm, 0, 10);
                    clientName = Encoding.ASCII.GetString(bytesForm, 0, 10);
                    clientName = clientName.Substring(0, clientName.IndexOf("$"));
                    log.Write("Client " + clientName + " has joined server");
                    lock (lockOnMe)
                        clientsIDs += clientName + " , ";
                    if (clientsList.Contains(clientName))
                    {
                        SendRemoveClientMSG(clientName);
                    }
                    clientsList.Add(clientName, clientTCPSocket);
                    lock (clientListLock)
                        clientListStr += "," + clientName;
                    clientListWait.Set();
                    HandleClient client = new HandleClient();
                    client.StartClient(clientTCPSocket, clientName, clientsList);

                }
            }
            catch (IOException)
            {
                log.Write("Connection with client " + clientName + " was closed from simulater");
                SendRemoveClientMSG(clientName);
            }
            catch (Exception ex)
            {
                log.Write("Exception in Main Function " + ex);
                Console.WriteLine("Excepption in Main Function " + ex);
                if (clientsList.ContainsKey(clientName))
                {
                    clientsList.Remove(clientsList);
                }
            }
            finally
            {
                clientTCPSocket.Close();
                clientSocket.Stop();
                log.Write("exit");
                Console.ReadLine();
            }
        }

        private static void FetchServers(TcpListener serverSocket)
        {
            bool newServer;
            try
            {
                while (true)
                {
                    newServer = true;
                    TcpClient serverTCPSocket = default(TcpClient);
                    Console.WriteLine("Started Listening .....");
                    serverTCPSocket = serverSocket.AcceptTcpClient();


                    IList<string> msgSplit;
                    msgSplit = serverTCPSocket.Client.RemoteEndPoint.ToString().Split(':');
                    foreach (HandleServer handleserver in serverList.Values)
                    {
                        Console.WriteLine(msgSplit[0] + " = " + handleserver.ServerIP + " " + msgSplit[0].Equals(handleserver.ServerIP));
                        if (msgSplit[0].Equals(handleserver.ServerIP))
                        {
                            handleserver.Connected = true;
                            handleserver.ServerSocket = serverTCPSocket;
                            handleserver.StartServer();
                            newServer = false;
                            log.Write("server " + handleserver.ServerIP + " has reconected ");
                            Console.WriteLine("server " + handleserver.ServerIP + " has reconected ");
                            //lock (disconnectedServerList)
                            //    disconnectedServerList.Remove(handleserver.ServerID);
                            //Console.WriteLine("Clearing List " + disconnectedServerList.Count + " deleted " + handleserver.ServerID + handleserver.Connected + handleserver.ServerSocket.Connected);
                        }
                    }
                    if (newServer)
                    {
                        HandleServer server = new HandleServer { ServerID = Interlocked.Increment(ref sereverounter), ServerSocket = serverTCPSocket, Sender = false, ServerIP = msgSplit[0] };
                        server.StartServer();
                        serverList.Add(server.ServerID, server);
                    }

                }
            }
            catch (Exception ex)
            {
                log.Write("Exception in FetchServers " + ex);
                Console.WriteLine("Exception in FetchServers " + ex);
            }

        }

        private static void ConnectToLoadBalancer(IPEndPoint localEndPoint)
        {
            try
            {
                SendMsg send;
                NetworkStream networkStream;
                log.Write("Connecting with the Load Balancer...........");
                loadBlncSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                loadBlncSock.Bind(localEndPoint);
                loadBlncSock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                tcpClient = new TcpClient()
                {
                    Client = loadBlncSock
                };
                //LBStream = default(NetworkStream);
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse(Constants.loadBLNCIP), 9000);
                //IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 9000);
                loadBlncSock.Connect(remoteEndPoint);
                networkStream = new NetworkStream(loadBlncSock);
                log.Write("Connected successfully with the Load Balancer, Starting thread for sending Number of connected clients");
                Thread sendSizeThread = new Thread(() => SendClientSize(loadBlncSock));
                sendSizeThread.Start();
                log.Write("Starting thread for Listening from Load Balancer");
                //Thread listenLoadBalnc = new Thread(() => ListenLoadBalnc(networkStream));
                //listenLoadBalnc.Start();

                Thread recServerFromLB = new Thread(() => RecServerFromLB(networkStream));
                recServerFromLB.Start();

                send = new SendMsg();
                send.IntitializeQueue();
            }
            catch (Exception ex)
            {
                log.Write("Exception in ConnectToLoadBalancer " + ex);
                Console.WriteLine("Exception in ConnectToLoadBalancer " + ex);
            }
        }


        private static void RecServerFromLB(NetworkStream networkStream)
        {
            byte[] buffer = new byte[30];
            string dataFromClient;
            IList<string> msgSplit;
            string serverIp;
            int port;

            try
            {
                while (true)
                {
                    networkStream.Read(buffer, 0, 30);
                    dataFromClient = Encoding.ASCII.GetString(buffer);
                    dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                    msgSplit = dataFromClient.Split(':');
                    serverIp = msgSplit[0];
                    port = Int32.Parse(msgSplit[1]) + 2;
                    log.Write("Starting Listening from Server " + serverIp + ":" + port);
                    TcpClient serverSocket = new TcpClient();
                    serverSocket.Connect(serverIp, port);
                    HandleServer server = new HandleServer { ServerID = Interlocked.Increment(ref sereverounter), ServerSocket = serverSocket, Sender = true, Port = port, ServerIP = serverIp };
                    server.StartServer();
                    serverList.Add(server.ServerID, server);
                }
            }
            catch (Exception ex)
            {
                log.Write("Exception in RecServerFromLB " + ex);
                Console.WriteLine("Exception in RecServerFromLB " + ex);
            }
        }


        private static void BroadcastClientList()
        {
            int counter = 0;
            byte[] buffer;
            try
            {
                Byte[] Size = new Byte[4];
                Byte[] type = new Byte[4];
                Byte[] finalMsg;
                while (true)
                {

                    clientListWait.WaitOne();
                    if (clientListStr != null)
                    {
                        lock (clientListLock)
                        {
                            buffer = Encoding.Default.GetBytes(clientListStr);
                            clientListStr = null;
                        }
                        finalMsg = new Byte[8 + buffer.Length];
                        using (MemoryStream mem = new MemoryStream(Size))
                        {
                            using (BinaryWriter BW = new BinaryWriter(mem))
                            {
                                BW.Write(buffer.Length);
                            }
                        }
                        using (MemoryStream mem = new MemoryStream(type))
                        {
                            using (BinaryWriter BW = new BinaryWriter(mem))
                            {
                                BW.Write(1);
                            }
                        }
                        Array.Copy(type, finalMsg, 4);
                        Array.Copy(Size, 0, finalMsg, 4, 4);
                        Array.Copy(buffer, 0, finalMsg, 8, buffer.Length);

                        foreach (HandleServer server in serverList.Values)
                        {
                            if (server.Connected)
                            {
                                NetworkStream netwrokStream = server.ServerSocket.GetStream();
                                netwrokStream.Write(finalMsg, 0, finalMsg.Length);
                                netwrokStream.Flush();
                                counter++;
                                log.Write(counter + " .BroadCasting client List to " + server.ServerIP + " [ " + Encoding.Default.GetString(finalMsg, 8, finalMsg.Length - 8));
                                Console.WriteLine("                             " + counter + " .BroadCasting client List to " + server.ServerIP);
                            }

                        }
                    }

                    Thread.Sleep(500);
                }
            }
            catch (Exception ex)
            {
                log.Write("exception in BroadCast Function " + ex);
                Console.WriteLine("exception in BroadCast Function " + ex);
                Console.ReadKey();
            }
        }

        private static void LogClientIDs()
        {
            while (true)
            {
                if (null != clientsIDs)
                {
                    lock (lockOnMe)
                    {
                        log.Write("Connected Clients :: " + clientsIDs);
                        clientsIDs = null;
                    }
                }

                Thread.Sleep(10000);
            }
        }
        private static void CounterPrinter()
        {
            //List<String> strArray = new List<String>();
            //bool loopBreaker = false;
            int counter = 1;
            int testCounter = 1;
            long avgSend = 0;
            long avgRec = 0;
            int totalsent = 1;
            int discCounter = 0;
            Console.WriteLine("++++++++++++++++++++++  Performance  ++++++++++++++++++++++");
            while (true)
            {

                //if (loopBreaker && 0 == recCounter && 0 == sendCounter)
                //    Console.WriteLine("break");//break;
                if (0 != recCounter)
                {
                    counter++;
                    totalRecieved = totalRecieved + recCounter;
                    totalsent = totalsent + sendCounter;
                    log.Write(testCounter + "------> Recieved = " + recCounter + " sent " + sendCounter + " TotalRec " + totalRecieved);
                    Console.WriteLine(testCounter + " ------> Recieved = " + recCounter + " sent " + sendCounter + /*" TotalRec " + totalRecieved +*/ " AVG = " + totalRecieved / counter);
                    if (discCounter != discardedClientsList.Count)
                    {
                        Console.WriteLine("                                                 Discarded Msg Size= " + discardedClientsList.Count);
                        discCounter = discardedClientsList.Count;
                    }


                    avgSend = (avgSend + sendCounter);
                    avgRec = (recCounter + avgRec);
                    Interlocked.Exchange(ref recCounter, 0);
                    Interlocked.Exchange(ref sendCounter, 0);
                    //loopBreaker = true;
                    testCounter++;
                    //if (15 == testCounter && Constants.loadBlncPort == 12000)
                    //{
                    //    log.Write("0000000000000000000000   close connection");
                    //    Console.WriteLine("0000000000000000000000   close connection");
                    //    HandleServer a = (HandleServer)serverList[1];
                    //    a.ServerSocket.GetStream().Close();
                    //    a.ServerSocket.Close();
                    //}
                }

                if (counter % 10 == 0 && null != clientsIDs)
                {
                    lock (lockOnMe)
                    {
                        log.Write("Connected Clients :: " + clientsIDs);
                        clientsIDs = null;
                    }
                }
                Thread.Sleep(1000);
            }

        }

        public static async void SendMessage(byte[] msg, int recieverID, bool sendToClient)
        {
            Byte[] Size = new Byte[4];
            Byte[] finalMsg = new Byte[4 + msg.Length];
            TcpClient clientSocket;

            try
            {


                using (MemoryStream mem = new MemoryStream(Size))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(msg.Length);
                    }
                }
                Array.Copy(Size, finalMsg, 4);
                Array.Copy(msg, 0, finalMsg, 4, msg.Length);
                Interlocked.Increment(ref sendCounter);
                if (sendToClient)
                {
                    clientSocket = (TcpClient)clientsList[recieverID.ToString()];
                    if (clientSocket.Connected)
                    {

                        log.Write("Sending message to client " + recieverID);
                        NetworkStream networkStream = clientSocket.GetStream();
                        await networkStream.WriteAsync(finalMsg, 0, finalMsg.Length);
                        networkStream.Flush();
                    }
                    else
                    {
                        log.Write("Socket for receiverID " + recieverID + " closed so going to remove it.");
                        SendRemoveClientMSG(recieverID.ToString());
                    }

                }
                else
                {

                    //FrwrdMsgToLB(finalMsg, 2);
                    if (recieverID == 125000)
                        log.Write("in sendMessage " + recieverID);
                    FrwrdMsgToServer(finalMsg, 2, recieverID.ToString());
                }
            }
            catch (IOException)
            {
                log.Write("In SendMessage() Connection closed exception for " + recieverID + " EXCEP");
                SendRemoveClientMSG(recieverID.ToString());
            }
            catch (ObjectDisposedException)
            {
                log.Write("In SendMessage() ObjectDisposedException for " + recieverID + " EXCEP");
                SendRemoveClientMSG(recieverID.ToString());
            }

            catch (Exception ex)
            {
                log.Write("Exception in SendMessage" + ex);
                Console.WriteLine("Exception in SendMessage for " + recieverID + " " + ex);
                //RemoveClientMSG(recieverID.ToString());
            }

        }

        private static void SendRemoveClientMSG(string clientID)
        {
            HandleServer serverFail = null;
            Byte[] msgTypeBuff = new Byte[4];
            Byte[] Size = new Byte[4];
            Byte[] finalMsg;

            try
            {
                lock (clientsList)
                {
                    if (clientsList.Contains(clientID))
                        clientsList.Remove(clientID);
                    else
                    {
                        log.Write("Client already removed from this server List. So not sending it to servers");
                        return;
                    }
                }

                log.Write("Sending client ID " + clientID + " to Servers to remove from List ");
                using (MemoryStream mem = new MemoryStream(msgTypeBuff))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(3);
                    }
                }
                Byte[] buffer = Encoding.Default.GetBytes(clientID);

                using (MemoryStream mem = new MemoryStream(Size))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(buffer.Length);
                    }
                }
                finalMsg = new byte[8 + buffer.Length];
                Array.Copy(msgTypeBuff, finalMsg, 4);
                Array.Copy(Size, 0, finalMsg, 4, 4);
                Array.Copy(buffer, 0, finalMsg, 8, buffer.Length);

                foreach (HandleServer server in serverList.Values)
                {
                    serverFail = server;
                    if (server.Connected && server.ServerSocket.Connected)
                    {
                        NetworkStream netwrokStream = server.ServerSocket.GetStream();
                        netwrokStream.Write(finalMsg, 0, finalMsg.Length);
                        netwrokStream.Flush();
                        log.Write("Sending message to server " + server.ServerIP + " to Remove " + clientID);
                    }
                    else
                    {
                        log.Write("server " + server.ServerIP + " has crashed so reconnecting it SendRemoveClientMSG Excep ");
                        //server.Connected = false;
                        lock (disconnectedServerList)
                        {
                            if (!disconnectedServerList.Contains(serverFail.ServerID))
                            {
                                serverFail.ServerSocket.Close();
                                serverFail.Connected = false;
                                disconnectedServerList.Add(serverFail.ServerID);
                                reconnectServerWait.Set();
                            }
                        }

                    }

                }
            }
            catch (ObjectDisposedException ex)
            {
                log.Write("ObjectDisposedException in RemoveClientMSG " + ex);
                lock (disconnectedServerList)
                {
                    if (!disconnectedServerList.Contains(serverFail.ServerID))
                    {
                        serverFail.ServerSocket.Close();
                        serverFail.Connected = false;
                        disconnectedServerList.Add(serverFail.ServerID);
                        reconnectServerWait.Set();
                    }
                }


            }
            catch (InvalidOperationException ex)
            {
                log.Write("InvalidOperationException in RemoveClientMSG " + ex);
                lock (disconnectedServerList)
                {
                    if (!disconnectedServerList.Contains(serverFail.ServerID))
                    {
                        serverFail.ServerSocket.Close();
                        serverFail.Connected = false;
                        disconnectedServerList.Add(serverFail.ServerID);
                        reconnectServerWait.Set();
                    }
                }

            }
            catch (IOException ex)
            {
                log.Write("IOException in RemoveClientMSG " + ex);
                lock (disconnectedServerList)
                {
                    if (!disconnectedServerList.Contains(serverFail.ServerID))
                    {
                        serverFail.ServerSocket.Close();
                        serverFail.Connected = false;
                        disconnectedServerList.Add(serverFail.ServerID);
                        reconnectServerWait.Set();
                    }
                }

            }
            catch (Exception ex)
            {
                log.Write("Exception in RemoveClientMSG " + ex);
                Console.WriteLine("Exception in RemoveClientMSG " + ex);
            }
        }

        public async static void FrwrdMsgToServer(byte[] msg, int msgType, string recieverID)
        {
            Byte[] msgTypeBuff = new Byte[4];
            Byte[] finalMsg = new Byte[4 + msg.Length];
            NetworkStream networkStream;
            bool clientFound = false;
            HandleServer serverFail = null;
            try
            {
                using (MemoryStream mem = new MemoryStream(msgTypeBuff))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(msgType);
                    }
                }
                Array.Copy(msgTypeBuff, finalMsg, 4);
                Array.Copy(msg, 0, finalMsg, 4, msg.Length);

                foreach (HandleServer server in serverList.Values)
                {
                    serverFail = server;
                    if (server.ClientsList.Contains(recieverID))
                    {
                        clientFound = true;
                        if (server.Connected && null != server.ServerSocket)
                        {
                            try
                            {
                                networkStream = server.ServerSocket.GetStream();
                                await networkStream.WriteAsync(finalMsg, 0, finalMsg.Length);
                                networkStream.Flush();
                            }
                            catch (IOException)
                            {
                                log.Write(serverList.Count + " Server socket has been closed so discarding the message " + recieverID);
                                discardedClientsList.Add(recieverID);
                                // Console.WriteLine("Discarding" + serverFail.ServerID);
                                lock (disconnectedServerList)
                                    log.Write("enterred " + recieverID);
                                {
                                    if (!disconnectedServerList.Contains(serverFail.ServerID))
                                    {
                                        serverFail.ServerSocket.Close();
                                        serverFail.Connected = false;
                                        Console.WriteLine("adding serverIP" + recieverID);
                                        disconnectedServerList.Add(serverFail.ServerID);
                                        reconnectServerWait.Set();
                                    }
                                }
                                continue;
                            }
                            catch (ObjectDisposedException)
                            {
                                log.Write(serverList.Count + " Server socket has been closed so discarding the message local try catch " + recieverID);
                                discardedClientsList.Add(recieverID);
                                // Console.WriteLine("Discarding" + serverFail.ServerID);
                                lock (disconnectedServerList)
                                {
                                    log.Write("enterred " + recieverID);
                                    if (!disconnectedServerList.Contains(serverFail.ServerID))
                                    {
                                        serverFail.ServerSocket.Close();
                                        serverFail.Connected = false;
                                        Console.WriteLine("adding serverIP " + recieverID);
                                        disconnectedServerList.Add(serverFail.ServerID);
                                        reconnectServerWait.Set();
                                    }
                                }
                                continue;
                            }


                            break;
                        }
                        else
                        {
                            log.Write(" server " + server.ServerIP + " has crashed so Discarding this msg  FrwrdMsgToServer");
                            // Console.WriteLine(recieverID);
                            discardedClientsList.Add(recieverID);
                            lock (disconnectedServerList)
                            {
                                if (!disconnectedServerList.Contains(serverFail.ServerID))
                                {
                                    serverFail.ServerSocket.Close();
                                    serverFail.Connected = false;
                                    Console.WriteLine("adding FrwrdMsgToServer");
                                    disconnectedServerList.Add(serverFail.ServerID);
                                    reconnectServerWait.Set();
                                }

                            }

                        }
                    }
                }
                if (!clientFound)
                {
                    HandleServer server = (HandleServer)serverList[1];
                    log.Write("Receiver not found in any srever " + recieverID);
                }
            }

            catch (Exception ex)
            {
                log.Write("Exception occured in FrwrdMsgToServer " + ex);
                Console.WriteLine("Exception occured in FrwrdMsgToServer " + ex);
            }
        }

        private static void DiscServersProcessor()
        {
            try
            {
                bool clear = false;
                while (true)
                {
                    reconnectServerWait.WaitOne();

                    lock (disconnectedServerList)
                    {
                        if (0 != disconnectedServerList.Count)
                        {
                            log.Write("Sometihng has crashed ");
                            Console.WriteLine("Sometihng has crashed " + disconnectedServerList.Count);

                            foreach (int serverID in disconnectedServerList)
                            {
                                HandleServer server = (HandleServer)serverList[serverID];
                                TcpClient serverSocket = new TcpClient();
                                if (!server.Connected)
                                {
                                    if (server.Sender)
                                    {
                                        server.ReconnectServer();
                                        clear = true;
                                    }
                                    else
                                    {
                                        log.Write("Reconnecting server but it is not a sender so waiting for " + server.ServerIP);
                                        Console.WriteLine("=================== Reconnecting server but it is not a sender so waiting for " + server.ServerIP);

                                    }
                                }
                            }

                        }
                    }

                    if (clear)
                    {
                        Thread.Sleep(15000);
                        log.Write("Clearing List " + disconnectedServerList.Count);
                        Console.WriteLine("Clearing List " + disconnectedServerList.Count);
                        lock (disconnectedServerList)
                        {
                            foreach (int serverID in disconnectedServerList)
                            {
                                HandleServer server = (HandleServer)serverList[serverID];
                                server.Connected = true;
                            }
                            disconnectedServerList.Clear();
                        }

                        clear = false;
                    }
                    reconnectServerWait.Reset();
                }
            }
            catch (Exception ex)
            {
                log.Write("Exception in DiscServersProcessor " + ex);
                Console.WriteLine("Exception in DiscServersProcessor " + ex);
            }
        }

        //public static async void FrwrdMsgToLB(byte[] msg, int msgType)
        //{
        //    Byte[] msgTypeBuff = new Byte[4];
        //    Byte[] finalMsg = new Byte[4 + msg.Length];

        //    using (MemoryStream mem = new MemoryStream(msgTypeBuff))
        //    {
        //        using (BinaryWriter BW = new BinaryWriter(mem))
        //        {
        //            BW.Write(msgType);
        //        }
        //    }
        //    Array.Copy(msgTypeBuff, finalMsg, msgTypeBuff.Length);
        //    Array.Copy(msg, 0, finalMsg, 4, msg.Length);

        //    LBStream = tcpClient.GetStream();
        //    await LBStream.WriteAsync(finalMsg, 0, finalMsg.Length);
        //    LBStream.Flush();

        //    int rec;
        //    Byte[] recID = new Byte[4];

        //    Array.Copy(finalMsg, 8, recID, 0, 4);

        //    using (MemoryStream mem = new MemoryStream(recID))
        //    {
        //        using (BinaryReader BW = new BinaryReader(mem))
        //        {
        //            rec = BW.ReadInt32();
        //        }
        //    }
        //    if (rec > 29999)
        //    {
        //        log.Write("MAAAAAAR dia " + rec + " type = " + msgType + Encoding.Default.GetString(finalMsg));
        //    }

        //}

        //private static async void ListenLoadBalnc(NetworkStream networkStream)
        //{
        //    Byte[] buffer = new Byte[4];
        //    SendMsg send;

        //    try
        //    {
        //        int msgSize = 0, sizeIndex = 0, remainingSize = 4, startIndex = 0;
        //        send = new SendMsg();
        //        send.IntitializeQueue();
        //        while (true)
        //        {
        //            while (sizeIndex < 4)
        //            {
        //                sizeIndex += await networkStream.ReadAsync(buffer, startIndex, remainingSize);
        //                if (sizeIndex > 0 && sizeIndex < 4)
        //                {
        //                    log.Write("Enterring IF " + sizeIndex);
        //                    remainingSize = 4 - sizeIndex;
        //                    startIndex = sizeIndex;
        //                    networkStream.Flush();
        //                    continue;
        //                }
        //                else if (0 == sizeIndex)
        //                {
        //                    networkStream.Close();
        //                    return;
        //                }
        //            }

        //            //socketLocal.Receive(buffer, 4, 0);
        //            //networkStream.Read(buffer, 0, 4);
        //            string a = Encoding.ASCII.GetString(buffer);
        //            using (MemoryStream mem = new MemoryStream(buffer))
        //            {
        //                using (BinaryReader BR = new BinaryReader(mem))
        //                {
        //                    msgSize = BR.ReadInt32();
        //                }
        //            }
        //            networkStream.Flush();
        //            if (msgSize > 150)
        //            {
        //                log.Write("KHARAB from LB" + a + " " + msgSize + " msgIND " + sizeIndex + " " + remainingSize + " " + startIndex);
        //                Console.WriteLine("MSGSIZE");
        //                //Console.WriteLine("KHARAB " + a + " " + msgSize);
        //                msgSize = 500;
        //            }
        //            else if (sizeIndex != 4 || remainingSize != 4)
        //            {
        //                log.Write("FITT Size " + msgSize + " msgIND " + sizeIndex + " " + remainingSize + " " + startIndex);
        //            }
        //            //send.SendMsgToClient(msgSize, networkStream, true);
        //            Test(msgSize, networkStream, true);
        //            //remainingSize = 4;
        //            startIndex = sizeIndex = msgSize = 0;
        //            remainingSize = 4;
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        log.Write("Exception occured while listening from Load Balancer" + ex);
        //    }
        //    finally
        //    {
        //        log.Write("Closing Load Balancer Socket");
        //        loadBlncSock.Close();
        //    }

        //}


        //public static async void Test(int msgSize, NetworkStream networkStream, bool enque)
        //{


        //    int msgIndex = 0;
        //    int startIndex, remainingSize;
        //    byte[] bytesFrom;
        //    remainingSize = msgSize;
        //    bytesFrom = new byte[msgSize];
        //    startIndex = 0;
        //    while (msgIndex < msgSize)
        //    {
        //        try
        //        {
        //            // log.Write("remainingSize size =  " + remainingSize + " msgSize = " + msgSize + " startIndex = " + startIndex + " client" + clNo);
        //            msgIndex += await networkStream.ReadAsync(bytesFrom, startIndex, remainingSize);
        //            if (msgIndex < msgSize)
        //            {
        //                remainingSize = msgSize - msgIndex;
        //                startIndex = msgIndex;
        //                networkStream.Flush();
        //                continue;
        //            }

        //            byte[] buffer = new byte[4];
        //            Array.Copy(bytesFrom, buffer, 4);
        //            string a = Encoding.ASCII.GetString(buffer);
        //            int recID = 0;
        //            using (MemoryStream mem = new MemoryStream(buffer))
        //            {
        //                using (BinaryReader BR = new BinaryReader(mem))
        //                {
        //                    recID = BR.ReadInt32();
        //                }
        //            }

        //            networkStream.Flush();
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Exception in SendMsgToClient " + ex);
        //        }
        //    }

        //}


        private static void SendClientSize(Socket socketLocal)
        {
            int lastCount = 0;
            Byte[] msgType = new byte[4];
            Byte[] finalMsg;
            Byte[] dataByte;
            using (MemoryStream mem = new MemoryStream(msgType))
            {
                using (BinaryWriter BW = new BinaryWriter(mem))
                {
                    BW.Write(1);
                }
            }
            while (true)
            {
                if (lastCount != clientsList.Count)
                {
                    lastCount = clientsList.Count;
                    log.Write("Sending Client List size to load balancer " + clientsList.Count);
                    dataByte = new byte[4];
                    using (MemoryStream mem = new MemoryStream(dataByte))
                    {
                        using (BinaryWriter BW = new BinaryWriter(mem))
                        {
                            BW.Write(clientsList.Count);
                        }
                    }
                    finalMsg = new byte[4 + dataByte.Length];
                    Array.Copy(msgType, finalMsg, msgType.Length);
                    Array.Copy(dataByte, 0, finalMsg, 4, dataByte.Length);
                    socketLocal.Send(finalMsg, finalMsg.Length, 0);
                }
                Thread.Sleep(4000);
            }
        }


        public static void BroadCastMsg(string msg, string senderName)
        {
            foreach (DictionaryEntry client in clientsList)
            {
                TcpClient broadCastClient = (TcpClient)client.Value;
                NetworkStream networkStream = broadCastClient.GetStream();
                Byte[] dataByte = Encoding.ASCII.GetBytes(msg);
                networkStream.Write(dataByte, 0, dataByte.Length);
                networkStream.Flush();
            }
        }

        private static string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Write("No network adapters with an IPv4 address in the system! " + ex);
            }
            return null;
        }
    }
}
