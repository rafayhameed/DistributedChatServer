using System;
using System.Linq;
using System.Threading;

namespace ClientSimulator
{
    class CSMain
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);
        private static Log log = Log.GetLog;
        static void Main(string[] args)
        {
            try
            {
                string msgStr;
                Simulator simulator = new Simulator();
                simulator.ConnectClients();
                Console.WriteLine("Waiting for your Command Sir");
                Console.ReadKey();
                Console.WriteLine("OKI DOki Sir");
                allDone.Set();
                Thread.Sleep(2000);
                Console.WriteLine("ALL CLIENTS CONNECTED SUCCESSFULLY");
                msgStr = simulator.CreateMsg();

                Thread messageSender1 = new Thread(simulator.SendMessageThread);
                Thread messageSender2 = new Thread(simulator.SendMessageThread2);
                Thread CounterPrinterThread = new Thread(simulator.CounterPrinter)
                {
                    Priority = ThreadPriority.AboveNormal
                };

                //messageSender1.Start();
                //messageSender2.Start();
                //SendMessage(simulator, msgStr);


                CounterPrinterThread.Start();



                //Console.ReadLine();
                //Constants.keepRunning = false;

                while (true)
                {
                    //if (!messageSender1.IsAlive && !messageSender2.IsAlive)
                    //{
                    //    Console.WriteLine("########## Executing again ############");
                    //    //messageSender1 = new Thread(simulator.SendMessageThread);
                    //    //messageSender2 = new Thread(simulator.SendMessageThread2);
                    //    //messageSender1.Start();
                    //    //messageSender2.Start();
                    //    //SendMessage(simulator, msgStr);
                    //}
                    Console.WriteLine("########## Executing again ############");
                    SendMessage(simulator, msgStr);
                    Thread.Sleep(200);
                }

            }


            catch (Exception ex)
            {
                log.Write(ex.ToString());
                Console.WriteLine("Exception in Main Function " + ex);
            }

        }

        private static void SendMessage(Simulator simulator, string msgStr)
        {
            switch (Constants.owner)
            {
                case "Danish":
                    //for (int i = 45000; i < 59000; i++)     //Danish
                    //{
                    //    simulator.SendMessage("FOR " + (59000 - i + 45000) + msgStr, i, (59000 - i) + 45000);       // Danish
                    //}
                    for (int i = 30001; i < 59000; i++)     //Danish
                    {
                        simulator.SendMessage("FOR " + (59000 - i + 30001) + msgStr, i, (59000 - i) + 30001);       // Danish
                        if (i % 400 == 0)
                        {
                            Thread.Sleep(50);
                        }
                    }

                    Console.WriteLine();
                    break;
                case "Rafay":
                    //for (int i = 15000; i < 29000; i++)     //Rafay
                    //{
                    //    simulator.SendMessage("FOR " + (29000 - i + 15000) + msgStr, i, (29000 - i) + 15000);       // Rafay
                    //}
                    for (int i = 1; i < 29000; i++)     //Rafay
                    {
                        simulator.SendMessage("FOR " + (29000 - i) + msgStr, i, 29000 - i);       // Rafay
                        if (i % 400 == 0)
                        {
                            Thread.Sleep(50);
                        }
                    }
                    Console.WriteLine();
                    break;
                case "Other":
                    for (int i = 60001; i < 89000; i++)     //Danish
                    {
                        simulator.SendMessage("FOR " + (89000 - i + 60001) + msgStr, i, (89000 - i) + 60001);       // Danish
                    }
                    Console.WriteLine();
                    break;
                default:
                    Console.WriteLine("Error in you owner");
                    break;

            }




            if (simulator.msgFailList.Count != 0)
            {

                simulator.msgFailList.Reverse();
                foreach (var failListObj in simulator.msgFailList.Reverse())
                {
                    log.Write("Resending message for " + failListObj.Key);
                    simulator.SendMessage("FOR " + failListObj + msgStr, failListObj.Key, failListObj.Value);
                    simulator.msgFailList.Remove(failListObj.Key);
                }
            }
        }

    }
}
