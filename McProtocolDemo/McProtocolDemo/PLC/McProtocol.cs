using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace McProtocolDemo.PLC
{
    /// <summary>
    /// The McProtocol class to connect to PLC.
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class McProtocol : IDisposable
    {
        private const string TAG = "McProtocol";

        private object _lock = new object();
        private Socket socketTCP;
        private string hostIP;
        private int hostPort;

        private static readonly int MAX_RETRY_TIMES = 1;
        private Database DataBase = new Database();
        /// <summary>
        /// Initializes a new instance of the <see cref="McProtocol"/> class.
        /// </summary>
        /// <param name="ip">The ip.</param>
        /// <param name="port">The port.</param>
        /// <exception cref="ArgumentException"></exception>
        public McProtocol(string ip, int port)
        {
            IPAddress address;
            if (!IPAddress.TryParse(ip, out address))
                throw new ArgumentException(string.Format("IP = {0} is not valid.", ip));

            hostIP = ip;
            hostPort = port;

            init();
        }

        private void init()
        {
            socketTCP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, new LingerOption(false, 1));
            socketTCP.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, 2500);
        }

        /// <summary>
        /// Gets a value indicating whether this protocol socket is connected.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this instance is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get
            {
                if (socketTCP != null)
                    return socketTCP.Connected;
                else
                    return false;
            }
        }

        /// <summary>
        /// Connects the TCP socket.
        /// </summary>
        public void Connect()
        {
            if (socketTCP == null)
                init();

            if (!IsConnected)
            {
                try
                {
                    Console.WriteLine(string.Format("Try to connect McProtocol at address {0}:{1}", hostIP, hostPort));
                    socketTCP.Connect(hostIP, hostPort);

                    Console.WriteLine(string.Format("Connect McProtocol at address {0}:{1} success", hostIP, hostPort));
                }
                catch (SocketException se)
                {
                    Console.WriteLine(string.Format("Connect McPortocol at address {0}:{1} fail. SocketException: {2}", hostIP, hostPort, se));
                }
            }
        }

        /// <summary>
        /// ReConnects the TCP socket.
        /// </summary>
        public void Reconnect()
        {
            Console.WriteLine(string.Format("Reconnect MC protocol. ({0}:{1})", hostIP, hostPort));
            Dispose();
            Connect();
        }

        /// <summary>
        /// Disconnect the TCP socket.
        /// </summary>
        public void Disconnect()
        {
            if (IsConnected)
            {
                socketTCP.Shutdown(SocketShutdown.Both);
                Console.WriteLine("McProtocol socket shutdown");
            }

            socketTCP.Close();
            Console.WriteLine("McProtocol socket close.");
        }

        /// <summary>
        /// 執行與釋放 (Free)、釋放 (Release) 或重設 Unmanaged 資源相關聯之應用程式定義的工作。
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            socketTCP = null;
        }

        public int ExecuteRead(string name, int startAddress, int size, ref short[] values)
        {
            if (McCommand.IsBitDevice(name))
                return executeReadBitVal(name, startAddress, size, ref values);
            else
                return executeReadWordVal(name, startAddress, size, ref values);
        }

        public int ExecuteWrite(string name, int startAddress, int size, short[] writeValues)
        {
            if (McCommand.IsBitDevice(name))
                return executeWriteBitVal(name, startAddress, size, writeValues);
            else
                return executeWriteWordVal(name, startAddress, size, writeValues);
        }

        private int executeReadBitVal(string name, int startAddress, int size, ref short[] values)
        {
            int times = 0;
            int val = -1;

            if (size > McCommand.MAX_BIT_RW_POINT)
                throw new ArgumentException(string.Format("Out of query size. Not valid. Size: {0}", size));

            if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be null or empty!"); }

            lock (_lock)
            {
                while (times <= MAX_RETRY_TIMES && val != 0)
                {
                    if (!IsConnected)
                    {
                        Console.WriteLine(string.Format("({0}:{1}) Disconnect. Fail to read bit: {2}{3}", hostIP, hostPort, name, startAddress));
                        Reconnect();
                    }

                    val = readBitVal(name, startAddress, size, ref values);

                    if (val != 0)
                    {
                        Console.WriteLine(string.Format("Read bit fail. Retry times: {0}. Address: {1}{2}. Return code: {3}", times, name, startAddress, val));
                        times++;
                    }
                }
            }

            if (val == 0)
                Console.WriteLine(string.Format("Read PLC value success. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", values)));
              
            else
                Console.WriteLine(string.Format("Read PLC value error. Address: {0}{1}. Size: {2}. Error code: {3}", name, startAddress, size, val));

            return val;
        }

        private int readBitVal(string name, int startAddress, int size, ref short[] values)
        {
            byte[] sendDataByte;
            byte[] recvDataByte = new byte[4999];

            try
            {
                if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be empty!"); }

                int address = startAddress;
                int readAddress = address;
                string readString = "";
                do
                {
                    string command = McCommand.CreateCommand(CommandType.batchRead, SubCommandType.bit, name, readAddress.ToString(), size);
                    readAddress += McCommand.MAX_BIT_RW_POINT;
                    sendDataByte = Encoding.ASCII.GetBytes(command);
                    socketTCP.Send(sendDataByte, sendDataByte.Length, SocketFlags.None);
                    int byteRead = socketTCP.Receive(recvDataByte, recvDataByte.Length, SocketFlags.None);
                    if (byteRead > 0)
                    {
                        string recvStr = Encoding.ASCII.GetString(recvDataByte, 0, byteRead);
                        string completeCode = McCommand.GetCompleteCode(recvStr);
                        if (completeCode != "0000") { return Convert.ToInt32(completeCode, 16); }
                        readString += recvStr.Substring(22);
                    }
                    else
                        return -1;
                } while (readAddress < (address + size));
                values = Enumerable.Range(0, size).Select(x => short.Parse(readString[x].ToString())).ToArray();
                return 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Reads the word value from PLC.
        /// </summary>
        /// <param name="name">The device code.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="size">The number of words to read.</param>
        /// <param name="values">The reference values to keep response data.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Out of query size. Not valid.</exception>
        /// <exception cref="Exception">Device name can not be empty!</exception>
        private int executeReadWordVal(string name, int startAddress, int size, ref short[] values)
        {
            int times = 0;
            int val = -1;

            if (size > McCommand.MAX_WORD_RW_POINT)
                throw new ArgumentException(string.Format("Out of query size. Not valid.", size));

            if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be null or empty!"); }

            lock (_lock)
            {
                while (times <= MAX_RETRY_TIMES && val != 0)
                {
                    if (!IsConnected)
                    {
                        Console.WriteLine(string.Format("({0}:{1}) Disconnect. Fail to read world: {2}{3}", hostIP, hostPort, name, startAddress));
                        Reconnect();
                    }

                    val = readWordVal(name, startAddress, size, ref values);

                    if (val != 0)
                    {
                        Console.WriteLine(string.Format("Read word fail. Retry times: {0}. Address: {1}{2}. Return code: {3}", times, name, startAddress, val));
                        times++;
                    }
                }
            }

            if (val == 0)
            {
                Console.WriteLine(string.Format("Read PLC value success. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", values)));
                DataBase.Insert(name, ref values, size, startAddress);
            }
            else
                Console.WriteLine(string.Format("Read PLC value error. Address: {0}{1}. Size: {2}. Error code: {3}", name, startAddress, size, val));

            return val;
        }

        private int readWordVal(string name, int startAddress, int size, ref short[] values)
        {
            byte[] sendDataBytes;
            byte[] recvDataBytes = new byte[4999];

            try
            {
                int address = startAddress;
                int readAddress = address;
                string readString = "";
                do
                {
                    string command = McCommand.CreateCommand(CommandType.batchRead, SubCommandType.word, name, readAddress.ToString(), size);
                    readAddress += McCommand.MAX_WORD_RW_POINT;
                    sendDataBytes = Encoding.ASCII.GetBytes(command);
                    socketTCP.Send(sendDataBytes, sendDataBytes.Length, SocketFlags.None);
                    int byteRead = socketTCP.Receive(recvDataBytes, recvDataBytes.Length, SocketFlags.None);
                    if (byteRead > 0)
                    {
                        string recvStr = Encoding.ASCII.GetString(recvDataBytes, 0, byteRead);
                        string completeCOde = McCommand.GetCompleteCode(recvStr);
                        if (completeCOde != "0000") { return Convert.ToInt32(completeCOde, 16); }
                        readString += recvStr.Substring(22);
                    }
                    else
                        return -1;
                } while (readAddress < (address + size));

                values = McCommand.HEXStringToShortArray(readString);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Read word value fail. Socket disconnect. Exception: {0}", ex));
                return -1;
            }
        }

        private int executeWriteBitVal(string name, int startAddress, int size, short[] writeValues)
        {
            int times = 0;
            int val = -1;

            if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be empty!"); }

            lock (_lock)
            {
                while (times <= MAX_RETRY_TIMES && val != 0)
                {
                    if (!IsConnected)
                    {
                        Console.WriteLine(string.Format("({0}:{1}) Disconnect. Fail to write bit: {2}{3}", hostIP, hostPort, name, startAddress));
                        Reconnect();
                    }

                    val = writeBitVal(name, startAddress, size, writeValues);

                    if (val != 0)
                    {
                        Console.WriteLine(string.Format("Write bit fail. Retry times: {0}. Address: {1}{2}. Return code: {3}", times, name, startAddress, val));
                        times++;
                    }
                }
            }

            if (val == 0)
                Console.WriteLine(string.Format("Write PLC value success. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", writeValues)));
            else
                Console.WriteLine(string.Format("Write PLC value error. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", writeValues)));

            return val;
        }

        private int writeBitVal(string name, int startAddress, int size, short[] writeValues)
        {
            byte[] writeByte;
            byte[] recvDataByte = new byte[4999];

            try
            {
                if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be empty!"); }

                int address = startAddress;
                var writeAry = split(writeValues, McCommand.MAX_BIT_RW_POINT);
                var qArray = writeAry.Select((val, idx) => new { Index = idx, Value = val });               //產生index
                foreach (var wAry in qArray)
                {
                    string writeAddress = (address + (wAry.Index * McCommand.MAX_BIT_RW_POINT)).ToString();
                    string command = McCommand.CreateCommand(CommandType.batchWrite, SubCommandType.bit, name, writeAddress, wAry.Value.Count(), wAry.Value.ToArray());
                    writeByte = Encoding.ASCII.GetBytes(command);
                    socketTCP.Send(writeByte, writeByte.Length, SocketFlags.None);
                    int returnBytes = socketTCP.Receive(recvDataByte, recvDataByte.Length, SocketFlags.None);
                    if (returnBytes > 0)
                    {
                        string recvStr = Encoding.ASCII.GetString(recvDataByte, 0, returnBytes);
                        string completeCode = McCommand.GetCompleteCode(recvStr);
                        if (completeCode != "0000") { return Convert.ToInt32(completeCode, 16); }
                    }
                    else
                        return -1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// Writes the word value to PLC.
        /// </summary>
        /// <param name="name">The device code.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="size">The number of words to write.</param>
        /// <param name="writeValues">The write values.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Device name can not be empty!</exception>
        private int executeWriteWordVal(string name, int startAddress, int size, params short[] writeValues)
        {
            int times = 0;
            int val = -1;

            if (string.IsNullOrEmpty(name)) { throw new Exception("Device name can not be empty!"); }

            lock (_lock)
            {
                while (times <= MAX_RETRY_TIMES && val != 0)
                {
                    if (!IsConnected)
                    {
                        Console.WriteLine(string.Format("({0}:{1}) Disconnect. Fail to write world: {2}{3}", hostIP, hostPort, name, startAddress));
                        Reconnect();
                    }

                    val = writeWordVal(name, startAddress, size, writeValues);

                    if (val != 0)
                    {
                        Console.WriteLine(string.Format("Write word fail. Retry times: {0}. Address: {1}{2}. Return code: {3}", times, name, startAddress, val));
                        times++;
                    }
                }
            }

            if (val == 0)
                Console.WriteLine(string.Format("Write PLC value success. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", writeValues)));
            else
                Console.WriteLine(string.Format("Write PLC value error. Address: {0}{1}. Size: {2}. Values: {3}.", name, startAddress, size, string.Join(",", writeValues)));

            return val;
        }

        private int writeWordVal(string name, int startAddress, int size, short[] writeValues)
        {
            byte[] writeDataBytes;
            byte[] recvDataBytes = new byte[4999];

            try
            {
                var writeArray = split(writeValues, McCommand.MAX_WORD_RW_POINT);
                var qArray = writeArray.Select((val, idx) => new { Index = idx, Value = val });//產生index
                foreach (var wAry in qArray)
                {
                    string writeAddress = (startAddress + (wAry.Index * McCommand.MAX_WORD_RW_POINT)).ToString();
                    string command = McCommand.CreateCommand(CommandType.batchWrite, SubCommandType.word, name, writeAddress, wAry.Value.Count(), wAry.Value.ToArray());
                    writeDataBytes = Encoding.ASCII.GetBytes(command);
                    socketTCP.Send(writeDataBytes, writeDataBytes.Length, SocketFlags.None);
                    int returnBytes = socketTCP.Receive(recvDataBytes, recvDataBytes.Length, SocketFlags.None);
                    if (returnBytes > 0)
                    {
                        string recvStr = Encoding.ASCII.GetString(recvDataBytes, 0, returnBytes);
                        string completeCode = McCommand.GetCompleteCode(recvStr);
                        if (completeCode != "0000") { return Convert.ToInt32(completeCode, 16); }
                    }
                    else
                        return -1;
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(string.Format("Write word value fail. Socket disconnect. Exception: {0}", ex));
                return -1;
            }
        }

        private static IEnumerable<IEnumerable<T>> split<T>(T[] array, int size)
        {
            int num = 0;
            while ((float)num < (float)array.Length / (float)size)
            {
                yield return array.Skip(num * size).Take(size);
                int num2 = num;
                num = num2 + 1;
            }
            yield break;
        }
    }
}
