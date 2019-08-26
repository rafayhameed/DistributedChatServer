﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace LoadBalancer
{
    class Log
    {

        private string filePath = "LoadBalancerLogs.txt";
        private Queue<string> writerQueue;
        private Queue<string> readerQueue1;
        private Queue<string> readerQueue2;
        private int currQueue/*, queue1Max, queue2Max*/;
        private Object lockObj = new object();
        private static Log log;
        private ManualResetEvent writeWait = new ManualResetEvent(false);
        private string date = DateTime.Now.ToLongDateString() + " ";

        private Log()
        {
            writeWait.Reset();
            readerQueue1 = new Queue<string>();
            readerQueue2 = new Queue<string>();
            writerQueue = readerQueue1;
            currQueue = 1;

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            Thread reader = new Thread(GetMessage);
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
            lock (writerQueue)
            {
                writerQueue.Enqueue(dateTime + message);
                count = writerQueue.Count;
            }
            if (count > 20000)
                Swap();
            writeWait.Set();
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
                {
                    writeWait.Reset();
                }

                Swap();
            }

        }

        private void Swap()
        {
            if (currQueue == 1)
            {
                lock (writerQueue)
                    writerQueue = readerQueue2;
                currQueue = 2;
            }
            else
            {
                lock (writerQueue)
                    writerQueue = readerQueue1;
                currQueue = 1;
            }

        }


        private void WriteInFile(StringBuilder message)
        {
            lock (lockObj)
            {
                using (StreamWriter streamWriter = new StreamWriter(filePath, true))
                {
                    streamWriter.Write(message);
                }
            }



        }

    }
}
