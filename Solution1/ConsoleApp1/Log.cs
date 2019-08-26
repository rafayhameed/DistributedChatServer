using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ConsoleApp1
{
    class Log
    {

        private string filePath = "ServerLogs.txt";

        private Queue<string> /*writerQueue,*/ readerQueue1, readerQueue2;
        private int currQueue;

        private static Log log;
        private ManualResetEvent writeWait = new ManualResetEvent(false);
        private string date = DateTime.Now.ToLongDateString() + " ";

        private Log()
        {
            writeWait.Reset();
            readerQueue1 = new Queue<string>();
            readerQueue2 = new Queue<string>();
            //writerQueue = readerQueue1;
            currQueue = 1;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            Thread reader = new Thread(GetMessage)
            {
                Priority = ThreadPriority.AboveNormal
            };
            reader.Start();
        }

        public static Log GetLog
        {
            get
            {
                if (log == null)
                {
                    log = new Log();
                }
                return log;
            }
        }


        public void Write(string message)
        {
            int count = 0;
            string dateTime = date + DateTime.Now.ToLongTimeString() + " : ";
            try
            {
                //count = writerQueue.Count;
                if (currQueue == 1)
                {
                    lock (readerQueue1)
                    {
                        readerQueue1.Enqueue(dateTime + message);
                        count = readerQueue1.Count;
                    }
                }
                else
                {
                    lock (readerQueue2)
                    {
                        readerQueue2.Enqueue(dateTime + message);
                        count = readerQueue2.Count;
                    }
                }
                //if (count > 40000)
                //    Swap();
                writeWait.Set();
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR IN LOG FILE msg= " + ex);
            }

        }

        private void GetMessage()
        {
            StringBuilder msg = new StringBuilder();
            while (true)
            {
                writeWait.WaitOne();

                if (currQueue == 1 && readerQueue2.Count != 0)
                {
                    while (readerQueue2.Count != 0)
                    {
                        lock (readerQueue2)
                            msg.AppendLine(readerQueue2.Dequeue());
                        if (msg.Length > 500)
                        {
                            WriteInFile(msg);
                            msg.Clear();
                        }

                    }
                    if (msg.Length != 0)
                    {
                        WriteInFile(msg);
                        msg.Clear();
                    }
                }
                else if (currQueue == 2 && readerQueue1.Count != 0)
                {
                    while (readerQueue1.Count != 0)
                    {
                        lock (readerQueue1)
                            msg.AppendLine(readerQueue1.Dequeue());
                        if (msg.Length > 500)
                        {
                            WriteInFile(msg);
                            msg.Clear();
                        }

                    }
                    if (msg.Length != 0)
                    {
                        WriteInFile(msg);
                        msg.Clear();
                    }
                }
                if (readerQueue1.Count == 0 && readerQueue2.Count == 0)
                    writeWait.Reset();

                Swap();
            }

        }

        private void Swap()
        {

            if (currQueue == 1)
            {
                //lock (writerQueue)
                //    writerQueue = readerQueue2;
                currQueue = 2;
            }
            else
            {
                //lock (writerQueue)
                //    writerQueue = readerQueue1;
                currQueue = 1;
            }

        }


        private void WriteInFile(StringBuilder message)
        {
            // Console.WriteLine(message);

            using (StreamWriter streamWriter = new StreamWriter(filePath, true))
            {
                streamWriter.Write(message);
            }


        }

    }
}
