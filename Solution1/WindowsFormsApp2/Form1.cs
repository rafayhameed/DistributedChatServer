using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp2
{
    public partial class Form1 : Form
    {
        TcpClient clientSocket = new TcpClient();
        NetworkStream networkStream = default(NetworkStream);
        string readData = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void Button1_Click_1(object sender, EventArgs e)
        {
            //Encoding.ASCII.GetBytes(textBox2.Text + "/" + DateTime.Now.ToLongTimeString() + "$");
            byte[] myData = CreateMsgByte(textBox2.Text + "#" + DateTime.Now.ToString(), 125000);

            networkStream.Write(myData, 0, myData.Length);
            networkStream.Flush();
            textBox2.Focus();
        }

        public void Msg(string mesg)
        {
            textBox1.Text = textBox1.Text + Environment.NewLine + " >> " + mesg;

        }

        private void TextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void TextBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void Button2_Click(object sender, EventArgs e)
        {
            byte[] msg = new byte[textBox1.Text.Length];
            TcpClient lbSocket = new TcpClient();
            lbSocket.Connect("10.105.26.185", 8000);

            msg = Encoding.ASCII.GetBytes(textBox1.Text + "$");
            networkStream = lbSocket.GetStream();
            networkStream.Write(msg, 0, msg.Length);
            networkStream.Flush();

            byte[] bytesFrom = new byte[30];

            networkStream = lbSocket.GetStream();
            networkStream.Read(bytesFrom, 0, 30);
            string dataFromClient = Encoding.ASCII.GetString(bytesFrom);
            networkStream.Flush();
            networkStream.Close();
            lbSocket.Close();
            dataFromClient = dataFromClient.Substring(0, dataFromClient.IndexOf("$"));
            IList<string> msgSplit = dataFromClient.Split(':');
            dataFromClient = msgSplit[0];
            MessageBox.Show(dataFromClient);
            ConnectToServer(msgSplit[0], Int32.Parse(msgSplit[1]) + 1);

            Thread clientThread = new Thread(GetMessage);
            clientThread.Start();
        }

        private void ConnectToServer(string serverIP, int port)
        {
            try
            {
                clientSocket = new TcpClient();
                clientSocket.Connect(serverIP, port);
                networkStream = clientSocket.GetStream();
                byte[] name = Encoding.ASCII.GetBytes(textBox1.Text + "$");
                networkStream.Write(name, 0, name.Length);
                networkStream.Flush();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }

        }

        private void GetMessage()
        {

            try
            {
                byte[] bytesFrom;
                while (true)
                {
                    int msgSize = 0;
                    int startIndex = 0;
                    int msgIndex = 0;
                    int remainingSize;
                    Byte[] sizeBuffer = new Byte[4];
                    networkStream.Read(sizeBuffer, 0, 4);

                    using (MemoryStream mem = new MemoryStream(sizeBuffer))
                    {
                        using (BinaryReader BR = new BinaryReader(mem))
                        {
                            msgSize = BR.ReadInt32();
                        }
                    }
                    remainingSize = msgSize;
                    bytesFrom = new byte[msgSize];
                    while (msgIndex < msgSize)
                    {
                        msgIndex += networkStream.Read(bytesFrom, startIndex, remainingSize);
                    }
                    String datafromClient = Encoding.ASCII.GetString(bytesFrom);
                    if (datafromClient.Contains("#"))
                    {
                        string time = datafromClient.Substring(datafromClient.IndexOf("#") + 1);
                        //datafromClient = (DateTime.Now - DateTime.Parse(time)).ToString();
                        TimeSpan duration = DateTime.Now.ToLocalTime() - DateTime.Parse(time);
                        datafromClient = duration.ToString();
                    }
                    readData = datafromClient;
                    Msg();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void Msg()
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(Msg));
            else
                textBox1.Text = readData;
        }

        public Byte[] CreateMsgByte(string msg, int recieverID)
        {
            Byte[] Size = new Byte[4];
            Byte[] recieverIDByte = new byte[4];
            byte[] sendMsg = Encoding.ASCII.GetBytes(msg);
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
            catch (Exception ex)
            {
                MessageBox.Show("Exception Occurred ==== " + ex.ToString());
            }
            return finalMsg;
        }
    }
}
