using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ClientSimulator
{
    class Simulator
    {
        public static Hashtable clientsList = new Hashtable();
        public static int receiverCounter = 0;
        private static Log log = Log.GetLog;
        private int senderCounter = 0;
        private int totalSent = 0;
        private int totalRecieved = 0;
        private static List<int> disconnectedClientList = new List<int>();

        public Dictionary<int, int> msgFailList = new Dictionary<int, int>();
        public int TotalRecieved { get => totalRecieved; set => totalRecieved = value; }
        public int TotalSent { get => totalSent; set => totalSent = value; }
        public int SenderCounter { get => senderCounter; set => senderCounter = value; }
        public static int ReceiverCounter { get => receiverCounter; set => receiverCounter = value; }
        public static List<int> DisconnectedClientList { get => disconnectedClientList; set => disconnectedClientList = value; }

        public string CreateMsg()
        {
            string msgStr = "The quick brown fox jumps End msg";
            for (int i = 0; i < 2; i++)
            {
                msgStr = msgStr + msgStr;
            }
            return msgStr;
        }
        public void CounterPrinter()
        {
            int counter = 0;
            bool loopBreaker = false;
            Console.WriteLine("++++++++++++++++++++++  Performance  ++++++++++++++++++++++");
            while (Constants.keepRunning)
            {
                if (loopBreaker && 0 == ReceiverCounter && 0 == SenderCounter)
                    break;
                if (0 != ReceiverCounter)
                {
                    counter++;
                    TotalRecieved = TotalRecieved + ReceiverCounter;
                    TotalSent = TotalSent + SenderCounter;
                    log.Write("------> Recieved = " + ReceiverCounter + " sent " + SenderCounter + " TotalRec " + totalRecieved);
                    Console.WriteLine("                                           Recieved = " + ReceiverCounter + " sent " + SenderCounter + " AVG Rec " + TotalRecieved / counter + " AVG sent " + TotalSent / counter);
                    Interlocked.Exchange(ref receiverCounter, 0);
                    Interlocked.Exchange(ref senderCounter, 0);
                    //loopBreaker = true;
                    log.Write("TOTAL SENT = " + TotalSent + " TOTAL RECIEVED = " + TotalRecieved + " DROPPED MESSAGES = " + (TotalSent - TotalRecieved));
                    Console.WriteLine("TotalSent= " + TotalSent + " TotalReceived = " + TotalRecieved /*+ " DROPPED MESSAGES = " + (TotalSent - TotalRecieved)*/);
                }

                Thread.Sleep(1000);
            }
            //Console.WriteLine("+++++++++++++++++++++++++++++++++++++++++++++++++++++++");
            log.Write("TOTAL SENT = " + TotalSent + " TOTAL RECIEVED = " + TotalRecieved + " DROPPED MESSAGES = " + (TotalSent - TotalRecieved));
            Console.WriteLine("TotalSent= " + TotalSent + " TotalReceived = " + TotalRecieved /*+ " DROPPED MESSAGES = " + (TotalSent - TotalRecieved)*/);
        }

        public void SendMessageThread()
        {
            string msgStr = CreateMsg();

            switch (Constants.owner)
            {
                case "Danish":
                    for (int i = 30001; i < 37000; i++)          // Danish
                    {
                        SendMessage("FOR " + (37000 - i + 30000) + msgStr, i, (37000 - i) + 30000);           // Danish
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FailMSg FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    break;
                case "Rafay":
                    for (int i = 1; i < 7000; i++)      // Rafay
                    {
                        SendMessage("FOR " + (7000 - i) + msgStr, i, (7000 - i));       // Rafay
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FailMSg FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    break;
                case "Other":
                    for (int i = 60001; i < 67000; i++)          // Other
                    {
                        SendMessage("FOR " + (67000 - i + 60000) + msgStr, i, (67000 - i) + 60000);           // Other
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FailMSg FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    break;

            }


            //SendMessageThread2();
        }


        public void SendMessageThread2()
        {
            string msgStr = CreateMsg();

            switch (Constants.owner)
            {
                case "Danish":
                    for (int i = 37001; i < 44050; i++)          // Danish
                    {
                        SendMessage("FOR " + (44050 - i + 37001) + msgStr, i, (44050 - i) + 37001);     // Danish
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    Console.WriteLine();
                    break;
                case "Rafay":
                    for (int i = 7001; i < 14050; i++)        // Rafay
                    {
                        SendMessage("FOR " + (14050 - i + 7000) + msgStr, i, (14050 - i) + 7000);     // Rafay
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    Console.WriteLine();
                    break;
                case "Other":
                    for (int i = 67001; i < 74050; i++)          // Other
                    {
                        SendMessage("FOR " + (74050 - i + 67001) + msgStr, i, (74050 - i) + 67001);     // Other
                    }
                    if (msgFailList.Count != 0)
                    {
                        msgFailList.Reverse();
                        foreach (var failListObj in msgFailList.Reverse())
                        {
                            log.Write("Sending Failed Msg");
                            SendMessage("FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                            msgFailList.Remove(failListObj.Key);
                        }
                    }
                    Console.WriteLine();
                    break;

            }


        }


        public void ConnectClients()
        {
            string dataFromClient = null;
            byte[] bytesFrom = null;
            byte[] msg = null;
            NetworkStream networkStream = default(NetworkStream);
            try
            {
                switch (Constants.owner)
                {
                    case "Danish":
                        for (int i = 30001; i < 60000; i++)   // Danish
                        {
                            Connect(dataFromClient, bytesFrom, msg, networkStream, i);
                        }
                        break;
                    case "Rafay":
                        for (int i = 1; i < 30000; i++)         //Rafay
                        {
                            Connect(dataFromClient, bytesFrom, msg, networkStream, i);
                        }
                        break;
                    case "Other":
                        for (int i = 60001; i < 90000; i++)         //Other
                        {
                            Connect(dataFromClient, bytesFrom, msg, networkStream, i);
                        }
                        break;
                    default:
                        Console.WriteLine("ERROR in your Owner");
                        break;

                }

            }
            catch (Exception)
            {
                log.Write("Exception in ConnectClients Function");
                throw;
            }
        }

        private void Connect(string dataFromClient, byte[] bytesFrom, byte[] msg, NetworkStream networkStream, int i)
        {
            TcpClient clientSocket = new TcpClient();
            bytesFrom = new byte[30];
            clientSocket.Connect(Constants.loadBLNCIP, 8000);
            //clientSocket.Connect("127.0.0.1", 8000);
            msg = Encoding.ASCII.GetBytes(i + "$");
            networkStream = clientSocket.GetStream();
            networkStream.Write(msg, 0, msg.Length);
            networkStream.Flush();
            networkStream.Read(bytesFrom, 0, 30);
            dataFromClient = Encoding.ASCII.GetString(bytesFrom);
            networkStream = clientSocket.GetStream();
            networkStream.Flush();
            networkStream.Close();
            clientSocket.Close();
            dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
            IList<string> msgSplit = dataFromClient.Split(':');
            dataFromClient = msgSplit[0];

            Client client = new Client { ClientID = i, ClientIDMsg = Encoding.ASCII.GetBytes(i + "$"), ServerIP = msgSplit[0], Port = Int32.Parse(msgSplit[1]) + 1 };
            clientsList.Add(i, client);
            client.StartClient();
        }

        public static void ReconnectClient(int clientID)
        {
            string dataFromClient;
            byte[] bytesFrom;
            byte[] msg;
            NetworkStream networkStream = default(NetworkStream);
            try
            {
                TcpClient clientSocket = new TcpClient();
                bytesFrom = new byte[30];
                log.Write("Reconnecting client " + clientID);
                clientSocket.Connect(Constants.loadBLNCIP, 8000);
                //clientSocket.Connect("127.0.0.1", 8000);
                msg = Encoding.ASCII.GetBytes(clientID + "$");
                networkStream = clientSocket.GetStream();
                networkStream.Write(msg, 0, msg.Length);
                networkStream.Flush();
                networkStream.Read(bytesFrom, 0, 30);
                dataFromClient = Encoding.ASCII.GetString(bytesFrom);
                networkStream = clientSocket.GetStream();
                networkStream.Flush();
                clientSocket.Close();
                dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
                IList<string> msgSplit = dataFromClient.Split(':');
                dataFromClient = msgSplit[0];
                Client client = (Client)clientsList[clientID];
                client.Port = Int32.Parse(msgSplit[1]) + 1;
                client.ServerIP = msgSplit[0];
                client.StartClient();
                lock (DisconnectedClientList)
                    DisconnectedClientList.Remove(clientID);
            }
            catch (Exception ex)
            {
                log.Write("Exception in ConnectClients Function " + ex);
            }

        }

        public async void SendMessage(String msg, int senderID, int recieverID)
        {

            Client client = null;
            Byte[] finalMsg;
            NetworkStream networkStream;
            try
            {
                client = (Client)clientsList[senderID];
                networkStream = default(NetworkStream);
                if (null != client.ClientSocket && client.ClientSocket.Connected)
                {
                    networkStream = client.ClientSocket.GetStream();
                    finalMsg = CreateMsgByte(msg, recieverID);

                    await networkStream.WriteAsync(finalMsg, 0, finalMsg.Length);
                    networkStream.Flush();
                    //await networkStream.WriteAsync(finalMsg, 0, finalMsg.Length);
                    //networkStream.Flush();


                    int rec;
                    Byte[] recID = new Byte[4];

                    Array.Copy(finalMsg, 4, recID, 0, 4);

                    using (MemoryStream mem = new MemoryStream(recID))
                    {
                        using (BinaryReader BW = new BinaryReader(mem))
                        {
                            rec = BW.ReadInt32();
                        }
                    }
                    Interlocked.Increment(ref senderCounter);
                    //Interlocked.Increment(ref senderCounter);
                    log.Write("sending To " + recieverID + " from " + senderID);
                }
                else
                {
                    log.Write("Socket connection is closed for  " + client.ClientID);
                    if (!DisconnectedClientList.Contains(client.ClientID))
                    {
                        lock (DisconnectedClientList)
                            DisconnectedClientList.Add(client.ClientID);
                        ReconnectClient(client.ClientID);
                    }
                }

            }
            catch (IOException)
            {
                log.Write("CANNOT Send further message from " + client.ClientID + " because Socket connection has been closed EXCEP");
                if (!DisconnectedClientList.Contains(client.ClientID))
                {
                    lock (DisconnectedClientList)
                        DisconnectedClientList.Add(client.ClientID);
                    ReconnectClient(client.ClientID);
                }
            }
            catch (ObjectDisposedException ex)
            {
                log.Write("CANNOT Send further message from " + client.ClientID + ex);
                if (!DisconnectedClientList.Contains(client.ClientID))
                {
                    lock (DisconnectedClientList)
                        DisconnectedClientList.Add(client.ClientID);
                    ReconnectClient(client.ClientID);
                }
            }

            catch (Exception ex)
            {
                log.Write("Exception in SendMessage Function " + ex.ToString());
                Console.WriteLine("Exception in SendMessage Function sender " + senderID + " rec " + recieverID + ex.ToString());
            }

        }

        public Byte[] CreateMsgByte(string msg, int recieverID)
        {
            Byte[] Size = new Byte[4];
            Byte[] recieverIDByte = new byte[4];
            byte[] sendMsg = Encoding.ASCII.GetBytes(msg + "$");
            Byte[] finalMsg = new Byte[4 + 4 + sendMsg.Length];

            try
            {
                using (MemoryStream mem = new MemoryStream(Size))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(4 + sendMsg.Length);
                    }
                }
                using (MemoryStream mem = new MemoryStream(recieverIDByte))
                {
                    using (BinaryWriter BW = new BinaryWriter(mem))
                    {
                        BW.Write(recieverID);
                    }
                }
                Array.Copy(Size, finalMsg, Size.Length);
                Array.Copy(recieverIDByte, 0, finalMsg, 4, Size.Length);
                Array.Copy(sendMsg, 0, finalMsg, 8, sendMsg.Length);

                int msgSize;
                using (MemoryStream mem = new MemoryStream(Size))
                {
                    using (BinaryReader BR = new BinaryReader(mem))
                    {
                        msgSize = BR.ReadInt32();
                    }
                }
            }
            catch (Exception)
            {
                log.Write("Exception in CreateMsgByte Function");
                throw;
            }
            return finalMsg;
        }
    }
}
