using System;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ConsoleApp1
{
    public class HandleClient
    {

        private TcpClient clientSocket;
        private string clientID;
        private Hashtable clientsList;
        private Log log = Log.GetLog;

        private ManualResetEvent socketWait = new ManualResetEvent(false);


        public void StartClient(TcpClient inClientSocket, string clineNo, Hashtable cList)
        {
            this.clientSocket = inClientSocket;
            this.clientID = clineNo;
            this.clientsList = cList;
            socketWait.Set();
            // Thread ctThread = new Thread(Listen);
            // ctThread.Start();
            Listen();
        }

        private async void Listen()
        {
            NetworkStream networkStream = null;
            try
            {
                networkStream = clientSocket.GetStream();
                clientSocket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                Byte[] buffer = new Byte[4];
                int msgSize = 0, sizeIndex = 0, remainingSize = 4, startIndex = 0;
                SendMsg send = new SendMsg();
                while (true)
                {
                    socketWait.WaitOne();
                    try
                    {
                        sizeIndex += await networkStream.ReadAsync(buffer, startIndex, remainingSize);

                    }
                    catch (ObjectDisposedException)
                    {
                        log.Write("Socket Connection with client " + clientID + " has crashed. Going to close it EXCEP");
                        networkStream.Close();
                        clientSocket.Close();
                        SendRemoveClientMSG();
                        socketWait.Set();
                        remainingSize = 4;
                        startIndex = sizeIndex = msgSize = 0;
                        break;
                    }
                    catch (IOException)
                    {
                        log.Write("Socket Connection with client " + clientID + " has crashed. Going to close it EXCEP");
                        networkStream.Close();
                        clientSocket.Close();
                        SendRemoveClientMSG();
                        socketWait.Set();
                        remainingSize = 4;
                        startIndex = sizeIndex = msgSize = 0;
                        break;
                    }


                    socketWait.Reset();
                    if (sizeIndex > 0 && sizeIndex < 4)
                    {
                        remainingSize = 4 - sizeIndex;
                        startIndex = sizeIndex;
                        continue;
                    }
                    else if (sizeIndex == 0)
                    {
                        log.Write(" sizeIndex == 0 so closing connection for " + clientID);
                        networkStream.Close();
                        clientSocket.Close();
                        return;
                    }
                    using (MemoryStream mem = new MemoryStream(buffer))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgSize = BR.ReadInt32();
                        }
                    }
                    networkStream.Flush();
                    if (msgSize > 147)
                    {
                        msgSize = 142;
                        log.Write(clientID + " ERROR msgSize " + msgSize + " " + Encoding.Default.GetString(buffer));
                        Console.WriteLine(clientID + " ye WRITTING FROM HANDLE CLIENT LISTEN " + " " + msgSize + " " + Encoding.Default.GetString(buffer));
                    }
                    send.SendMsgToClient(msgSize, networkStream, false);
                    socketWait.Set();
                    remainingSize = 4;
                    startIndex = sizeIndex = msgSize = 0;
                }
            }
            catch (IOException)
            {
                log.Write("Socket Connection with client " + clientID + " has crashed. Going to close it EXCEP");
                networkStream.Close();
                clientSocket.Close();
                SendRemoveClientMSG();
            }
            catch (Exception ex)
            {
                log.Write("Socket Connection with client " + clientID + " has crashed. Going to close it " + ex);
                Console.WriteLine("Socket Connection with client " + clientID + " has crashed. Going to close it " + ex);
                networkStream.Close();
                clientSocket.Close();
                SendRemoveClientMSG();
            }
        }

        private void SendRemoveClientMSG()
        {
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
                Byte[] msgTypeBuff = new Byte[4];
                Byte[] Size = new Byte[4];
                Byte[] finalMsg;

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

                foreach (HandleServer server in ServerMain.serverList.Values)
                {
                    log.Write("Trting to ssend to " + server.ServerIP + server.Connected);
                    if (server.Connected)
                    {
                        try
                        {
                            NetworkStream netwrokStream = server.ServerSocket.GetStream();
                            netwrokStream.Write(finalMsg, 0, finalMsg.Length);
                            netwrokStream.Flush();
                            log.Write("Sending message to server " + server.ServerIP + " to Remove " + clientID);
                        }
                        catch (IOException)
                        {
                            log.Write("Socket Connection with Server " + server.ServerID + " has crashed from SendRemoveClientMSG. Going to close it EXCEP");
                            Console.WriteLine("Socket Connection with Server " + server.ServerID + " has crashed from SendRemoveClientMSG. Going to close it EXCEP");
                            lock (ServerMain.disconnectedServerList)
                            {
                                if (!ServerMain.disconnectedServerList.Contains(server.ServerID))
                                {
                                    server.ServerSocket.Close();
                                    server.Connected = false;
                                    Console.WriteLine("adding listen");
                                    ServerMain.disconnectedServerList.Add(server.ServerID);
                                    ServerMain.reconnectServerWait.Set();
                                }
                            }

                        }
                        catch (ObjectDisposedException)
                        {
                            log.Write("Socket Connection with Server " + server.ServerID + " has crashed from SendRemoveClientMSG. Going to close it EXCEP");
                            Console.WriteLine("Socket Connection with Server " + server.ServerID + " has crashed from SendRemoveClientMSG. Going to close it EXCEP");
                            lock (ServerMain.disconnectedServerList)
                            {
                                if (!ServerMain.disconnectedServerList.Contains(server.ServerID))
                                {
                                    server.ServerSocket.Close();
                                    server.Connected = false;
                                    Console.WriteLine("adding listen");
                                    ServerMain.disconnectedServerList.Add(server.ServerID);
                                    ServerMain.reconnectServerWait.Set();
                                }
                            }

                        }

                    }
                    else
                    {
                        log.Write("Socket conneection with " + server.ServerIP + " was closed ");
                    }

                }
            }
            catch (Exception ex)
            {
                log.Write("Exception in RemoveClientMSG " + ex);
                Console.WriteLine("Exception in RemoveClientMSG " + ex);
            }
        }

    }
}
