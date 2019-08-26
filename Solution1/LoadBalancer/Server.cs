using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LoadBalancer
{
    class Server
    {
        private TcpClient serversocket;
        private EndPoint ipAddress;
        private int clientSize;
        private int serverID;
        private Log log = Log.GetLog;

        public Server(TcpClient serversocket, EndPoint ipAddress, int clientSize, int serverID)
        {
            this.serversocket = serversocket;
            serversocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            this.ipAddress = ipAddress;
            this.clientSize = clientSize;
            this.serverID = serverID;
            Thread ctThread = new Thread(ListenFromServer);
            ctThread.Start();
        }

        private async void ListenFromServer()
        {
            int msgType;
            byte[] msgTypeBytes;
            int sizeIndexType = 0, remainingSizeTypr = 4, startIndexType = 0;

            try
            {
                while ((true))
                {
                    NetworkStream networkStream = serversocket.GetStream();
                    msgTypeBytes = new byte[Constants.intByteLength];
                    sizeIndexType += await networkStream.ReadAsync(msgTypeBytes, startIndexType, remainingSizeTypr);
                    if (sizeIndexType > 0 && sizeIndexType < 4)
                    {
                        remainingSizeTypr = 4 - sizeIndexType;
                        startIndexType = sizeIndexType;
                        continue;
                    }
                    else if (0 == sizeIndexType)
                    {
                        networkStream.Close();
                        serversocket.Close();
                        return;
                    }
                    networkStream.Flush();
                    using (MemoryStream mem = new MemoryStream(msgTypeBytes))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgType = BR.ReadInt32();
                        }
                    }
                    if (msgType == Constants.ClientSizeMsg)
                    {
                        if (0 != serversocket.ReceiveBufferSize)
                        {
                            byte[] bytesFrom = new byte[Constants.intByteLength];
                            await networkStream.ReadAsync(bytesFrom, 0, Constants.intByteLength);
                            networkStream.Flush();
                            using (MemoryStream mem = new MemoryStream(bytesFrom))
                            {
                                using (BinaryReader BR = new BinaryReader(mem))
                                {
                                    clientSize = BR.ReadInt32();
                                }
                            }
                            log.Write("updating server size for server : - " + ipAddress + " To " + clientSize);
                        }
                    }

                    else if (msgType == Constants.ServerMsg)
                    {
                        int msgIndex = 0;
                        Byte[] buffer = new Byte[Constants.intByteLength];
                        int msgSize, recieverID, startIndex = 0, remainingSize = 4;
                        byte[] recieverIDBuffer;
                        byte[] bytesFrom;
                        byte[] copyArray;
                        int sizeIndex = 0;

                        while (sizeIndex < 4)
                        {
                            sizeIndex += await networkStream.ReadAsync(buffer, startIndex, remainingSize);
                            if (sizeIndex > 0 && sizeIndex < 4)
                            {
                                remainingSize = 4 - sizeIndex;
                                startIndex = sizeIndex;
                                continue;
                            }
                            else if (0 == sizeIndex)
                            {
                                networkStream.Close();
                                return;
                            }
                        }

                        networkStream.Flush();
                        using (MemoryStream mem = new MemoryStream(buffer))
                        {
                            using (BinaryReader BR = new BinaryReader(mem))
                            {
                                msgSize = BR.ReadInt32();
                            }
                        }
                        startIndex = 4;
                        remainingSize = msgSize;
                        bytesFrom = new byte[msgSize + 4];
                        Array.Copy(buffer, bytesFrom, 4);
                        copyArray = new byte[msgSize];

                        while (msgIndex < msgSize)
                        {
                            msgIndex += await networkStream.ReadAsync(bytesFrom, startIndex, remainingSize);
                            if (msgIndex < msgSize)
                            {
                                log.Write("++++++++++++++++Enterring IF Statement+++++++++++++++");
                                remainingSize = msgSize - msgIndex;
                                startIndex = msgIndex;
                                networkStream.Flush();
                                continue;
                            }
                            recieverIDBuffer = new byte[Constants.intByteLength];
                            Array.Copy(bytesFrom, 4, recieverIDBuffer, 0, Constants.intByteLength);
                            using (MemoryStream mem = new MemoryStream(recieverIDBuffer))
                            {
                                using (BinaryReader BR = new BinaryReader(mem))
                                {
                                    recieverID = BR.ReadInt32();
                                }
                            }

                            if (msgSize > 150 || recieverID > 29999)
                            {
                                log.Write("Error in msgSize " + msgSize + " recID " + recieverID);
                            }
                            networkStream.Flush();
                            if (LoadBalancerMain.clientServerList.ContainsKey(recieverID))
                            {
                                int serverID = (int)LoadBalancerMain.clientServerList[recieverID];
                                //log.Write("From ServerID " + ipAddress.ToString() + " Sending Message to client -> " + recieverID + " registered on server -> " + serverID);
                                LoadBalancerMain.SendMessageToServer(bytesFrom, serverID);
                            }
                            else
                            {
                                log.Write("Client " + recieverID + " is not registered with any server.");
                                log.Write("size= " + msgSize + " sizeIndex= " + sizeIndex + " msgIndex= " + msgIndex + "Client " + recieverID + " msg= " + Encoding.ASCII.GetString(bytesFrom, 4, msgSize - 4));
                            }


                        }
                    }
                    else if (msgType == Constants.RemoveClientMsg)
                    {
                        Byte[] buffer = new Byte[Constants.intByteLength];
                        int clientID;

                        await networkStream.ReadAsync(buffer, 0, Constants.intByteLength);
                        networkStream.Flush();
                        using (MemoryStream mem = new MemoryStream(buffer))
                        {
                            using (BinaryReader BR = new BinaryReader(mem))
                            {
                                clientID = BR.ReadInt32();
                            }
                        }
                        log.Write("Removing client " + clientID + " from the client server list.");
                        lock (LoadBalancerMain.clientServerList)
                        {
                            LoadBalancerMain.clientServerList.Remove(clientID);
                        }
                    }


                }

            }
            catch (IOException)
            {
                serversocket.Close();
                Console.WriteLine("SERVER crashed");
                LoadBalancerMain.RemoveServer(serverID);
            }
            catch (Exception ex)
            {
                log.Write("Exception in Listen function of Class Server " + ex.ToString());
                log.Write("Shutting down server");
                Console.WriteLine("Shutting down server");
                serversocket.Close();
            }

        }

        public int ClientSize { get => clientSize; set => clientSize = value; }
        public EndPoint IpAddress { get => ipAddress; set => ipAddress = value; }
        public TcpClient Serversocket { get => serversocket; set => serversocket = value; }
        public int ServerID { get => serverID; set => serverID = value; }
    }
}
