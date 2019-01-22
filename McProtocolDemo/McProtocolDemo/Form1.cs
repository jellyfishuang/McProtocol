using McProtocolDemo.PLC;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace McProtocolDemo
{
    public partial class Form1 : Form
    {
        private McProtocol mcProtocol = null;

        public Form1()
        {
            InitializeComponent();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);

            connect();
            read();
        }

        protected override void OnClosed(EventArgs e)
        {
            if (mcProtocol != null)
                mcProtocol.Dispose();

            base.OnClosed(e);
        }

        private void connect()
        {
            string ip = "192.168.2.12";
            int port = 3000;

            mcProtocol = new McProtocol(ip, port);
            mcProtocol.Connect();

            if (mcProtocol.IsConnected)
                Text = "McProtocol connected.";
            else
                Text = "McProtocol disconnect";
        }

        private void read()
        {
            if (mcProtocol == null || !mcProtocol.IsConnected)
                return;

            short[] readData = new short[1];
            var rtn = mcProtocol.ExecuteRead("D", 1000, 10, ref readData);

            if (rtn != 0)
                Console.WriteLine("Error code: " + rtn);
            else
            {
                Console.WriteLine("Write success.");

                // Data format convert. All convert function in McCommand
                string strData = McCommand.ShortArrayToASCIIString(false, readData[0]);
                string intDataStr = McCommand.ShortArrayToInt16String(readData[1]);
            }
        }

        private void write()
        {
            if (mcProtocol == null || !mcProtocol.IsConnected)
                return;

            short[] writeData = McCommand.Int16StringToShortArray("1");

            // 1 word = 16 bit = 2 character
            //short[] writeData = McCommand.ASCIIStringToShortArray("HI");

            int rtn = mcProtocol.ExecuteWrite("D", 1000, 1, writeData);

            if (rtn != 0)
                Console.WriteLine("Error code: " + rtn);
            else
                Console.WriteLine("Write success.");
        }
    }
}
