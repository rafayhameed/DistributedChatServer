using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LoadBalancer
{
    class LoadBalancerMain
    {
        public static Hashtable serverList = new Hashtable();
        public static Dictionary<int, int> clientServerList = new Dictionary<int, int>();
        private static int slctdServer;
        public static TcpClient Serversocket { get; private set; }
        private static Log log = Log.GetLog;
        private static int clientSize = 0;
        public static ManualResetEvent acceptWait = new ManualResetEvent(false);

        static void Main(string[] args)
        {
            StartLoadBalancerServer();
            acceptWait.WaitOne();

            if (serverList.Count > 0)
            {
                slctdServer = 1;
                Thread evalServer = new Thread(EvaluateServer);
                evalServer.Start();
                StartLoadBalancerClient();
            }

        }

        private static void StartLoadBalancerServer()
        {

            acceptWait.Reset();
            string localIPStr = Constants.loadBLNCIP;
            //string localIPStr = "127.0.0.1";
            int port = 9000;
            TcpListener serverSocket = new TcpListener(IPAddress.Parse(localIPStr), port);
            serverSocket.Start();
            log.Write("Load Balancer started at " + localIPStr + ":" + port);
            Thread ServerListener = new Thread(() => FetchServers(serverSocket));
            ServerListener.Start();
        }

        private static async void FetchServers(TcpListener serverSocket)
        {
            int serverID = 0;
            TcpClient clientSocket = default(TcpClient);
            while (true)
            {
                serverID = serverID + 1;
                clientSocket = await serverSocket.AcceptTcpClientAsync();
                log.Write("Server has connected to the Load Balancer with IP Adress" + clientSocket.Client.RemoteEndPoint);
                Console.WriteLine("Server has connected to the Load Balancer with IP Adress" + clientSocket.Client.RemoteEndPoint);
                Server server = new Server(clientSocket, clientSocket.Client.RemoteEndPoint, 0, serverID);
                BroadcastServerID(clientSocket.Client.RemoteEndPoint.ToString());
                serverList.Add(serverID, server);
                acceptWait.Set();
            }
        }


        private static void BroadcastServerID(string IpAddress)
        {
            Server server = null;
            NetworkStream networkStream = default(NetworkStream);
            byte[] sendMsg;
            if (serverList.Count > 0)
            {
                foreach (var serverVar in serverList.Values)
                {
                    server = (Server)serverVar;
                    log.Write("Sending new servirID " + IpAddress + " To " + server.IpAddress.ToString());
                    Console.WriteLine("Sending new servirID " + IpAddress + " To " + server.IpAddress.ToString());
                    sendMsg = Encoding.ASCII.GetBytes(IpAddress + "$");
                    networkStream = server.Serversocket.GetStream();
                    networkStream.Write(sendMsg, 0, sendMsg.Length);
                    networkStream.Flush();
                    sendMsg = null;
                }
            }
        }


        private static void StartLoadBalancerClient()
        {
            string localIPStr = Constants.loadBLNCIP;
            //string localIPStr = "127.0.0.1";
            int port = 8000;
            var clientSocketListener = new TcpListener(IPAddress.Parse(localIPStr), port);
            clientSocketListener.Start();
            log.Write("Load Balancer started listening for clients at " + localIPStr + ":" + port);
            ListenForClients(clientSocketListener);
        }

        private static async void ListenForClients(TcpListener clientSocketListener)
        {
            TcpClient clientSocket = default(TcpClient);
            NetworkStream networkStream = default(NetworkStream);
            int noOfclients = 0;
            string slctdServerStr = null;
            string clientName = null;
            while (true)
            {
                try
                {
                    clientSocket = clientSocketListener.AcceptTcpClient();
                    if (serverList.Count == 0)
                    {
                        log.Write("No server available in Load Balancer to Connect Client. Discarding this client. Try again");
                        continue;
                    }
                    networkStream = clientSocket.GetStream();
                    byte[] bytesForm = new byte[10];
                    await networkStream.ReadAsync(bytesForm, 0, 10);
                    clientName = Encoding.ASCII.GetString(bytesForm, 0, 10);
                    clientName = clientName.Substring(0, clientName.IndexOf("$"));
                    int clientNameInt = Int32.Parse(clientName);
                    if (clientServerList.ContainsKey(clientNameInt))
                    {
                        clientServerList.Remove(clientNameInt);
                        log.Write("Client already registerred so deleting it and assigning a new server");
                    }
                    slctdServerStr = ((Server)serverList[slctdServer]).IpAddress.ToString();
                    lock (clientServerList)
                        clientServerList.Add(clientNameInt, slctdServer);
                    byte[] sendMsg = Encoding.ASCII.GetBytes(slctdServerStr + "$");
                    string sendMsgStr = Encoding.ASCII.GetString(sendMsg);
                    log.Write("Assigning client " + clientName + " To " + slctdServerStr);
                    networkStream.Write(sendMsg, 0, sendMsg.Length);
                    noOfclients++;
                    Interlocked.Increment(ref clientSize);
                }
                catch (Exception ex)
                {
                    log.Write("Exception wihle assigning server to Client " + clientName + ex);
                    Console.WriteLine("Exception wihle assigning server to Client " + clientName + ex);
                }


            }
        }

        private static void EvaluateServer()
        {
            while (true)
            {
                if (serverList.Count == 0)
                {
                    log.Write("Load Balancer is Empty.");
                    Thread.Sleep(2000);
                    continue;
                }
                int min = 50000;
                foreach (DictionaryEntry server in serverList)
                {
                    Server curntServer = (Server)server.Value;
                    if (curntServer.ClientSize == Constants.maxClientSize)
                    {
                        serverList.Remove(server.Key);
                        continue;
                    }
                    if (curntServer.ClientSize <= min)
                    {
                        Interlocked.Exchange(ref slctdServer, curntServer.ServerID);
                        min = curntServer.ClientSize;
                    }

                }
                Thread.Sleep(2000);
                if (clientSize > 0)
                    log.Write("Total client size = " + clientSize);
            }
        }

        public static async void SendMessageToServer(byte[] msg, int serverID)
        {
            TcpClient serverSock = ((Server)serverList[serverID]).Serversocket;
            NetworkStream networkStream = serverSock.GetStream();
            await networkStream.WriteAsync(msg, 0, msg.Length);
            networkStream.Flush();

        }

        public static void RemoveServer(int serverID)
        {
            lock (serverList)
            {
                serverList.Remove(serverID);
                if (serverID == slctdServer)
                    EvaluateServer();
                Console.WriteLine("Connection to Server has Crashed ");
                log.Write("Connection to Server has Crashed ");
                log.Write("Shutting down server");
                log.Write("Removed server " + ((Server)serverList[serverID]).IpAddress + " from the ServerList");
            }
            lock (clientServerList)
            {
                clientServerList.Reverse();
                foreach (var clientID in clientServerList.Reverse())
                {
                    if (clientID.Value == serverID)
                    {
                        log.Write("Removing client " + clientID.Key + " from the ClientServerList");
                        clientServerList.Remove(clientID.Key);
                        Interlocked.Decrement(ref clientSize);
                    }
                }
            }


        }
    }
}
