using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace ConsoleApp1
{
    class SendMsg
    {
        private static Log log = Log.GetLog;
        public static Queue<byte[]> serverCurrQueue, serverQueue1, serverQueue2;
        public static Queue<byte[]> clientCurrQueue, clientQueue1, clientQueue2;
        private static ManualResetEvent serverQueueWait, clientQueueWait;
        private static int serverQueue, clientQueue;

        public void SendMsgToClient(int msgSize, NetworkStream networkStream, bool enque)
        {

            int recieveID = 0;
            int msgIndex = 0;
            int startIndex, remainingSize;
            byte[] bytesFrom;
            remainingSize = msgSize;
            try
            {
                bytesFrom = new byte[msgSize];
                startIndex = 0;

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



                    byte[] recieverIDBuffer = new byte[4];
                    Array.Copy(bytesFrom, 0, recieverIDBuffer, 0, 4);
                    using (MemoryStream mem = new MemoryStream(recieverIDBuffer))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            recieveID = BR.ReadInt32();
                        }
                    }

                }
                if (enque)
                {
                    //if (!Monitor.TryEnter(serverCurrQueue))
                    //    Swap("server");
                    if (serverQueue == 1)
                    {
                        lock (serverQueue1)
                            serverCurrQueue.Enqueue(bytesFrom);
                    }
                    else
                    {
                        lock (serverQueue2)
                            serverCurrQueue.Enqueue(bytesFrom);
                    }

                    serverQueueWait.Set();
                    //if (serverCurrQueue.Count > 60000)
                    //    Swap("server");
                }
                else
                {
                    //if (!Monitor.TryEnter(clientCurrQueue))
                    //Swap("client");
                    if (clientQueue == 1)
                    {
                        lock (clientQueue1)
                        {
                            clientQueue1.Enqueue(bytesFrom);
                        }

                    }
                    else
                    {
                        lock (clientQueue2)
                        {
                            clientQueue2.Enqueue(bytesFrom);
                        }
                    }


                    clientQueueWait.Set();
                    //if (clientCurrQueue.Count > 60000)
                    //    Swap("client");
                    //ProcessMsg(bytesFrom);
                }
            }
            catch (IOException)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.Write("Exception in SendMsgToClient " + ex);
                Console.WriteLine("Exception in SendMsgToClient " + ex);
                return;
            }
        }

        private void ProcessMsg(byte[] bytesFrom)
        {
            int recieveID;
            byte[] recieverIDBuffer;
            byte[] messageBuffer;

            try
            {
                recieverIDBuffer = new byte[4];
                messageBuffer = new byte[bytesFrom.Length - 4];
                Array.Copy(bytesFrom, 0, recieverIDBuffer, 0, 4);
                Array.Copy(bytesFrom, 4, messageBuffer, 0, messageBuffer.Length);
                using (MemoryStream mem = new MemoryStream(recieverIDBuffer))
                {
                    using (BinaryReader BR = new BinaryReader(mem))
                    {
                        recieveID = BR.ReadInt32();
                    }
                }
                if (null != messageBuffer)
                {
                    Interlocked.Increment(ref ServerMain.recCounter);
                    if (ServerMain.clientsList.Contains(recieveID.ToString()))
                    {
                        ServerMain.SendMessage(messageBuffer, recieveID, true);
                    }
                    else
                    {
                        log.Write("Client " + recieveID + " not registered with this server.");
                        ServerMain.SendMessage(bytesFrom, recieveID, false);
                    }

                }
                else
                {
                    log.Write("Either message for client or reciever ID is null");
                }
            }
            catch (IOException ex)
            {
                log.Write("Exception in ProcessMsg " + ex);
                Console.WriteLine("Exception in ProcessMsg " + ex);
                throw;
            }
            catch (Exception ex)
            {
                log.Write("Exception in ProcessMsg " + ex);
                Console.WriteLine("Exception in ProcessMsg " + ex);
            }
        }

        public void IntitializeQueue()
        {
            serverQueue1 = new Queue<byte[]>();
            serverQueue2 = new Queue<byte[]>();
            serverCurrQueue = serverQueue1;
            serverQueue = 1;
            serverQueueWait = new ManualResetEvent(false);
            serverQueueWait.Reset();

            clientQueue1 = new Queue<byte[]>();
            clientQueue2 = new Queue<byte[]>();
            clientCurrQueue = clientQueue1;
            clientQueue = 1;
            clientQueueWait = new ManualResetEvent(false);
            clientQueueWait.Reset();


            Thread serverProcessorThread = new Thread(ProcessServerQueue);
            serverProcessorThread.Start();

            Thread clientProcessorThread = new Thread(ProcessClientQueue);
            clientProcessorThread.Start();
        }

        private void ProcessServerQueue()
        {
            try
            {
                while (true)
                {
                    serverQueueWait.WaitOne();
                    if (serverQueue == 1 && serverQueue2.Count != 0)
                    {
                        while (serverQueue2.Count != 0)
                        {
                            lock (serverQueue2)
                                ProcessMsg(serverQueue2.Dequeue());

                        }
                    }
                    else if (serverQueue == 2 && serverQueue1.Count != 0)
                    {
                        while (serverQueue1.Count != 0)
                        {
                            lock (serverQueue1)
                                ProcessMsg(serverQueue1.Dequeue());
                        }
                    }

                    //else if (serverCurrQueue.Count > 20000)
                    //{
                    //    Swap("client");
                    //}
                    if (serverQueue1.Count == 0 && serverQueue2.Count == 0)
                    {
                        serverQueueWait.Reset();
                    }
                    Swap("server");

                }

            }
            catch (Exception ex)
            {
                log.Write("Exception in ProcessClientQueue " + ex);
                Console.WriteLine("Exception in ProcessClientQueue " + ex);
            }


        }

        private void ProcessClientQueue()
        {
            try
            {
                while (true)
                {
                    clientQueueWait.WaitOne();
                    if (clientQueue == 1 && clientQueue2.Count != 0)
                    {
                        lock (clientQueue2)
                        {
                            while (clientQueue2.Count != 0)
                            {
                                ProcessMsg(clientQueue2.Dequeue());
                            }
                        }

                    }
                    else if (clientQueue == 2 && clientQueue1.Count != 0)
                    {
                        lock (clientQueue1)
                        {
                            while (clientQueue1.Count != 0)
                            {

                                ProcessMsg(clientQueue1.Dequeue());
                            }
                        }

                    }
                    //else if (clientCurrQueue.Count > 20000)
                    //{
                    //    Swap("client");
                    //}
                    if (clientQueue1.Count == 0 && clientQueue2.Count == 0)
                    {
                        clientQueueWait.Reset();
                    }
                    Swap("client");
                }
            }
            catch (Exception ex)
            {
                log.Write("Exception in ProcessClientQueue " + ex);
                Console.WriteLine("Exception in ProcessClientQueue " + ex);
            }
        }

        private void Swap(string type)
        {
            switch (type)
            {
                case "server":
                    if (serverQueue == 1)
                    {
                        lock (serverCurrQueue)
                            serverCurrQueue = serverQueue2;
                        serverQueue = 2;
                    }
                    else
                    {
                        lock (serverCurrQueue)
                            serverCurrQueue = serverQueue1;
                        serverQueue = 1;
                    }
                    break;
                case "client":
                    if (clientQueue == 1)
                    {
                        lock (clientCurrQueue)
                            clientCurrQueue = clientQueue2;
                        clientQueue = 2;
                    }
                    else
                    {
                        lock (clientCurrQueue)
                            clientCurrQueue = clientQueue1;
                        clientQueue = 1;
                    }
                    break;
            }


        }
    }
}
