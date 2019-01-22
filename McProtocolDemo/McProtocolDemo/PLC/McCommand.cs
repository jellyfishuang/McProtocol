using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace McProtocolDemo.PLC
{
    /// <summary>
    /// The command type.
    /// </summary>
    public static class CommandType
    {
        public static readonly short batchRead = 0x0401;
        public static readonly short batchWrite = 0x1401;
        public static readonly short readomRead = 0x0403;
        public static readonly short randomWrite = 0x1402;
    }

    /// <summary>
    /// The sub command type.
    /// </summary>
    public static class SubCommandType
    {
        public static readonly short word = 0x0000;
        public static readonly short bit = 0x0001;
    }

    /// <summary>
    /// The MC command class.
    /// </summary>
    public static class McCommand
    {
        private static readonly short basicFormat = 0x5000;
        private static readonly byte netwrokNumber = 0x00;
        private static readonly byte pcNumber = 0xff;
        private static readonly short ioNumber = 0x03FF;
        private static readonly byte channelNumber = 0x00;
        private static readonly short cpuTimer = 0x0010;

        public static readonly short MAX_BIT_RW_POINT = 3584;
        public static readonly short MAX_WORD_RW_POINT = 960;

        private const string TAG = "McCommand";

        /// <summary>
        /// Creates the McProtocol command.
        /// </summary>
        /// <param name="mainCommand">The main command.</param>
        /// <param name="subCommand">The sub command.</param>
        /// <param name="deviceName">Name of the device.</param>
        /// <param name="startAddress">The start address.</param>
        /// <param name="size">The number of words.</param>
        /// <param name="writeVal">The write values.</param>
        /// <returns></returns>
        public static string CreateCommand(short mainCommand, short subCommand, string deviceName, string startAddress, int size, short[] writeVal = null)
        {
            string address;
            string name = deviceName.PadRight(2, '*');

            if (name == "X*" || name == "Y*" || name == "B*" || name == "W*" || name == "SB" || name == "SW" || name == "DX" || name == "DY" || name == "ZR")
                address = int.Parse(startAddress).ToString("X").PadLeft(6, '0');
            else
                address = int.Parse(startAddress).ToString().PadLeft(6, '0');

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0:X4}", cpuTimer);
            sb.AppendFormat("{0:X4}", mainCommand);
            sb.AppendFormat("{0:X4}", subCommand);
            sb.AppendFormat("{0}", name);
            sb.AppendFormat("{0}", address);
            sb.AppendFormat("{0:X4}", size);

            string data = sb.ToString();

            if (writeVal != null)       //Write Command
            {
                if (subCommand == SubCommandType.bit)
                {
                    var tmpAry = writeVal.Select(x => x == 0 ? (short)0 : (short)1).ToArray();  // 0 => 0, other => 1
                    data += string.Join("", tmpAry);
                }
                else                    //Word
                {
                    byte[] writeBytes = writeVal.SelectMany(BitConverter.GetBytes).ToArray();
                    for (int i = 0; i < writeBytes.Length; i += 2) // big/little endian轉換
                        writeBytes.SwapBytes(i, i + 1);
                    data += ByteToMcString(writeBytes);
                }
            }

            StringBuilder sendCMD = new StringBuilder();
            sendCMD.AppendFormat("{0:X4}", basicFormat);
            sendCMD.AppendFormat("{0:X2}", netwrokNumber);
            sendCMD.AppendFormat("{0:X2}", pcNumber);
            sendCMD.AppendFormat("{0:X4}", ioNumber);
            sendCMD.AppendFormat("{0:X2}", channelNumber);
            sendCMD.AppendFormat("{0:X4}", data.Length);
            sendCMD.Append(data);

            return sendCMD.ToString();
        }

        public static bool IsBitDevice(string type)
        {
            return !((type == "D")
                  || (type == "SD")
                  || (type == "Z")
                  || (type == "ZR")
                  || (type == "R")
                  || (type == "W"));
        }

        #region Utility

        /// <summary>
        /// Convert Bytes array to MC string.
        /// </summary>
        /// <param name="byteAry">The byte array.</param>
        /// <returns></returns>
        public static string ByteToMcString(byte[] byteAry)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i <= byteAry.Length - 1; i++)
            {
                sb.Append((byteAry[i] / 16).ToString("X"));
                sb.Append((byteAry[i] % 16).ToString("X"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Convert Hexadecimals string to byte arrays.
        /// </summary>
        /// <param name="hexStr">The hexadecimal string.</param>
        /// <returns></returns>
        public static byte[] HexStringToByteArray(string hexStr)
        {
            return Enumerable.Range(0, hexStr.Length)
                                 .Where(x => x % 2 == 0)
                                 .Select(x => Convert.ToByte(hexStr.Substring(x, 2), 16))
                                 .ToArray();
        }

        /// <summary>
        /// Gets the complete code of McProtocol response.
        /// </summary>
        /// <param name="responseStr">The McProtocol response string.</param>
        /// <returns></returns>
        public static string GetCompleteCode(string responseStr)
        {
            if (!string.IsNullOrEmpty(responseStr))
                return responseStr.Substring(18, 4);
            else
                return string.Empty;
        }

        /// <summary>
        /// Convert DateTime to the BCD format short array.
        /// </summary>
        /// <param name="dateTime">The dateTime to be converted.</param>
        /// <param name="hasDayOfWeek">if set to <c>true</c> [has day of week].</param>
        /// <returns></returns>
        public static short[] DateTimeToBCDShorts(DateTime dateTime, bool hasDayOfWeek)
        {
            List<short> shorts = new List<short>();

            string str = dateTime.ToString("yyMMddHHmmss");

            for (int i = 0; i < str.Length; i += 4)
            {
                shorts.Add(BCDToShort(str.Substring(i, 4)));
            }

            if (hasDayOfWeek)
            {
                string dayOfWeek = string.Format("{0:X2}", (int)dateTime.DayOfWeek);
                shorts.Add(BCDToShort(dayOfWeek));
            }

            return shorts.ToArray();
        }

        /// <summary>
        /// Convert the BCD format of shorts to DateTime.
        /// </summary>
        /// <param name="shorts">The short array.</param>
        /// <returns></returns>
        public static DateTime BCDShortsToDateTime(params short[] shorts)
        {
            string dateStr = string.Empty;

            // No matter it has or not. Ignore day of week in shorts array index 3 
            for (int i = 0; i < 3; i++)
            {
                dateStr += ShortToBCD(shorts[i]).ToString();
            }

            DateTime dt = parseToDateTime(dateStr);
            return dt;
        }

        /// <summary>
        /// Parses dateTime string to dateTime.
        /// </summary>
        /// <param name="dateStr">The dateTime string.</param>
        /// <param name="format">The dateTime format.</param>
        /// <returns></returns>
        private static DateTime parseToDateTime(string dateStr, string format = "yyMMddHHmmss")
        {
            DateTime dt;

            if (DateTime.TryParseExact(dateStr, format, null, System.Globalization.DateTimeStyles.None, out dt))
            {
                return dt;
            }
            else
            {
                Console.WriteLine(string.Format("Fail to parse time `{0}`", dateStr));
                return DateTime.Now;
            }
        }

        /// <summary>
        /// Convert format from BCD to the short (PLC word).
        /// </summary>
        /// <param name="num">BCD format string value. (0000~FFFF).</param>
        /// <returns></returns>
        public static short BCDToShort(string num)
        {
            if (num.Length > 4)
                throw new Exception(string.Format("BCD string length can only 4. Data: {0}", num));

            num = num.PadLeft(4, '0');
            int a = Int16.Parse(num[0].ToString(), System.Globalization.NumberStyles.HexNumber);
            int b = Int16.Parse(num[1].ToString(), System.Globalization.NumberStyles.HexNumber);
            int c = Int16.Parse(num[2].ToString(), System.Globalization.NumberStyles.HexNumber);
            int d = Int16.Parse(num[3].ToString(), System.Globalization.NumberStyles.HexNumber);
            return (short)((d & 0x0F) | (c & 0x0F) << 4 | (b & 0x0F) << 8 | (a & 0x0F) << 12);
        }

        /// <summary>
        /// Convert Short to the BCD format string.
        /// </summary>
        /// <param name="a">Short value.</param>
        /// <returns></returns>
        public static string ShortToBCD(short a)
        {
            return ((a >> 12) & 0x0F).ToString("X") +
                    ((a >> 8) & 0x0F).ToString("X") +
                    ((a >> 4) & 0x0F).ToString("X") +
                           (a & 0x0F).ToString("X");
        }

        /// <summary>
        /// Parses the PLC word by format string.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <param name="format">The format.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static List<string> ParsePLCWord(string format, short word)
        {
            // Ex: McCommand.ParsePLCWord((int)12054, "00,b,ii,b,iii,ii,i,hhhh");
            // Ex: McCommand.ParsePLCWord((int)12054, "000,ii,b,ii,hhhhhhhh");

            // Convert to binary string
            string dataBinaryStr = Convert.ToString(word, 2).PadLeft(16, '0');
            List<string> results = new List<string>();

            // Remove space in format string
            format = format.Replace(" ", string.Empty);
            string[] splitFormats = format.Split(',');

            int index = 0;
            for (int i = 0; i < splitFormats.Length; i++)
            {
                char type = splitFormats[i][0];
                int length = splitFormats[i].Length;
                string subStr = dataBinaryStr.Substring(index, length);

                // b => Bool        (bit to bool
                // i => Integer     (Binary string to int
                // h => BCD Hex     (a byte to a BCD hex symbol
                // 0 => Ignonre
                switch (type)
                {
                    case 'b':
                        if (subStr.Length != 1)
                            throw new ArgumentException(string.Format("Boolean can only have 1 bit. Data: {0}", subStr));

                        results.Add(Convert.ToInt16(subStr).ToString());
                        break;

                    case 'i':
                        results.Add(Convert.ToInt16(subStr, 2).ToString());
                        break;

                    case 'h':
                        // BCD format. 4 bits binary (symbol: h) to present a digital bit number in Hex format
                        string tmp = string.Empty;
                        for (int j = 0; j < subStr.Length; j += 4)
                        {
                            tmp += Convert.ToInt16(subStr.Substring(j, 4), 2).ToString("X");
                        }
                        results.Add(tmp);
                        break;

                    case '0':
                        // Ignore 0
                        break;
                    default:
                        throw new ArgumentException(string.Format("Unknown format symbol: {0}", type));
                }
                index += length;
            }

            return results;
        }

        /// <summary>
        /// Creates the PLC word.
        /// </summary>
        /// <param name="format">The PLC word format.</param>
        /// <param name="datas">The datas.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Number of data object array is incorrect.</exception>
        /// <exception cref="ArgumentException">
        /// </exception>
        public static short CreatePLCWord(string format, params object[] datas)
        {
            // Remove space in format string
            format = format.Replace(" ", string.Empty);
            string[] splitFormats = format.Split(',');
            string result = string.Empty;
            int readIndex = 0;

            for (int i = 0; i < splitFormats.Length; i++)
            {
                char type = splitFormats[i][0];
                int length = splitFormats[i].Length;

                if (readIndex > datas.Length)
                    throw new Exception("Number of data object array is incorrect.");

                switch (type)
                {
                    case 'b':
                        if (length != 1)
                            throw new ArgumentException("Boolean can only have 1 bit.");

                        result += Convert.ToBoolean(datas[readIndex]);
                        readIndex++;
                        break;

                    case 'i':
                        result += Convert.ToString((int)datas[readIndex], 2).PadLeft(length, '0');
                        readIndex++;
                        break;

                    case 'h':
                        string str = Convert.ToString(Convert.ToInt16(datas[readIndex].ToString(), 16), 2);
                        result += str.PadLeft(length, '0');
                        readIndex++;
                        break;

                    case '0':
                        result += string.Concat(Enumerable.Repeat('0', length));
                        break;

                    default:
                        throw new ArgumentException(string.Format("Unknown format symbol: {0}", type));
                }
            }

            return Convert.ToInt16(result, 2);
        }

        /// <summary>
        /// Convert short array to binary string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        public static string ShortArrayToBinaryString(bool reverse, params short[] shortArray)
        {
            List<string> strList = new List<string>();

            for (int i = 0; i < shortArray.Length; i++)
            {
                short val = shortArray[i];
                strList.Add(Convert.ToString(val, 2).PadLeft(16, '0'));
            }

            if (reverse)
                strList.Reverse();

            return string.Join("", strList);
        }

        /// <summary>
        /// Convert short array to ASCII string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <param name="reverse">if set to <c>true</c> [reverse].</param>
        /// <returns></returns>
        public static string ShortArrayToASCIIString(bool reverse, params short[] shortArray)
        {
            byte[] result = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, result, 0, result.Length);

            if (reverse)
            {
                for (int i = 0; i < result.Length; i += 2)
                {
                    result.SwapBytes(i, i + 1);
                }
            }

            return Encoding.ASCII.GetString(result).Replace('\0', ' ');
        }

        /// <summary>
        /// Convert short array to hexadecimal string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        public static string ShortArrayToHEXString(params short[] shortArray)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < shortArray.Length; i++)
            {
                short val = shortArray[i];
                sb.Append(val.ToString("X2").PadLeft(4, '0'));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Convert short array to unsign int16 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error. Unsign Int16 need 1 word (16 bit).</exception>
        public static string ShortArrayToUInt16String(params short[] shortArray)
        {
            if (shortArray.Length != 1)
                throw new Exception("Size error. Unsign Int16 need 1 word (16 bit).");

            return Convert.ToUInt16(shortArray[0]).ToString();
        }

        /// <summary>
        /// Convert short array to unsign int32 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error. Unsign Int32 need 2 word (32 bit).</exception>
        public static string ShortArrayToUInt32String(params short[] shortArray)
        {
            if (shortArray.Length != 2)
                throw new Exception("Size error. Unsign Int32 need 2 word (32 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToUInt32(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert short array to unsign int64 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error. Unsign Int64 need 4 word (64 bit).</exception>
        public static string ShortArrayToUInt64String(params short[] shortArray)
        {
            if (shortArray.Length != 4)
                throw new Exception("Size error. Unsign Int64 need 4 word (64 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToUInt64(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert short array to int16 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error. Int16 need 1 word (16 bit).</exception>
        public static string ShortArrayToInt16String(params short[] shortArray)
        {
            if (shortArray.Length != 1)
                throw new Exception("Size error. Int16 need 1 word (16 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToInt16(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert short array to int32 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error! Int32 need 2 word (32 bit).</exception>
        public static string ShortArrayToInt32String(params short[] shortArray)
        {
            if (shortArray.Length != 2)
                throw new Exception("Size error! Int32 need 2 word (32 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToInt32(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert short array to int64 string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error! Int64 need 4 word (64 bit).</exception>
        public static string ShortArrayToInt64String(params short[] shortArray)
        {
            if (shortArray.Length != 4)
                throw new Exception("Size error! Int64 need 4 word (64 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToInt64(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert short array to float string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error! Float need 2 word (32 bit).</exception>
        public static string ShortArrayToFloatString(params short[] shortArray)
        {
            if (shortArray.Length != 2)
                throw new Exception("Size error! Float need 2 word (32 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToSingle(byteArray, 0).ToString("R");
        }

        /// <summary>
        /// Convert short array to double string.
        /// </summary>
        /// <param name="shortArray">The short array.</param>
        /// <returns></returns>
        /// <exception cref="Exception">Size error! Double need 4 word (64 bit).</exception>
        public static string ShortArrayToDoubleString(short[] shortArray)
        {
            if (shortArray.Length != 4)
                throw new Exception("Size error! Double need 4 word (64 bit).");

            byte[] byteArray = new byte[shortArray.Length * 2];
            Buffer.BlockCopy(shortArray, 0, byteArray, 0, byteArray.Length);

            return BitConverter.ToDouble(byteArray, 0).ToString();
        }

        /// <summary>
        /// Convert binary string to short array.
        /// </summary>
        /// <param name="binaryStr">The binary string.</param>
        /// <returns></returns>
        public static short[] BinStringToShortArray(string binaryStr)
        {
            List<short> shortList = new List<short>();

            for (int i = 0; i < binaryStr.Length; i++)
            {
                char c = binaryStr[i];
                shortList.Add(Convert.ToInt16(c));
            }

            return shortList.ToArray();
        }

        /// <summary>
        /// Convert Hex format string to short array.
        /// </summary>
        /// <param name="hexString">The hexadecimal string.</param>
        /// <returns></returns>
        public static short[] HEXStringToShortArray(string hexString)
        {
            hexString = ((hexString.Length % 4 == 0) ? hexString : hexString.PadRight((hexString.Length / 4 + 1) * 4, '0'));

            return Enumerable.Range(0, hexString.Length)
                                 .Where(x => x % 4 == 0)
                                 .Select(x => Convert.ToInt16(hexString.Substring(x, 4), 16))
                                 .ToArray();
        }

        /// <summary>
        /// Convert ASCII format string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <param name="reverse">if set to <c>true</c> [reverse].</param>
        /// <returns></returns>
        public static short[] ASCIIStringToShortArray(string ASCIIString, bool reverse = false)
        {
            if (string.IsNullOrEmpty(ASCIIString))
                throw new Exception("ASCII string cannot be null or empty.");

            if (ASCIIString.Length % 2 == 1)
                ASCIIString = ASCIIString.PadRight(ASCIIString.Length + 1, ' ');

            byte[] BA = Encoding.GetEncoding("UTF-8").GetBytes(ASCIIString.ToCharArray());
            List<short> ls = new List<short>();

            for (int i = 0; i < BA.Length; i += 2)
            {
                ls.Add(Convert.ToInt16(BA[i + 1].ToString("X2") + BA[i].ToString("X2"), 16));
            }

            return ls.ToArray();
        }

        /// <summary>
        /// Convert Int16 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Int16StringToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            return new short[]
            {
                Convert.ToInt16(ASCIIString)
            };
        }

        /// <summary>
        /// Convert Int32 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Int32ToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            short[] result = new short[2];
            int num = Convert.ToInt32(ASCIIString);
            short low = (short)(num << 16 >> 16);
            short high = (short)(num >> 16);

            result[0] = low;
            result[1] = high;

            return result;
        }

        /// <summary>
        /// Convert Int64 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Int64ToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            short[] result = new short[4];
            long num = Convert.ToInt64(ASCIIString);
            short first = (short)(num >> 48);
            short second = (short)(num << 16 >> 32);
            short third = (short)(num << 32 >> 16);
            short fourth = (short)(num << 48);

            result[0] = fourth;
            result[1] = third;
            result[2] = second;
            result[3] = first;

            return result;
        }

        /// <summary>
        /// Convert unsign int16 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Uint16ToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            return new short[]
            {
                (short)Convert.ToUInt16(ASCIIString)
            };
        }

        /// <summary>
        /// Convert unsign int32 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Uint32ToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            short[] result = new short[2];
            uint num = Convert.ToUInt32(ASCIIString);
            short high = (short)(num >> 16);
            short low = (short)(num << 16 >> 16);

            result[0] = low;
            result[1] = high;

            return result;
        }

        /// <summary>
        /// Convert unsign int64 string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] Uint64ToShortArray(string ASCIIString)
        {
            if (ASCIIString == string.Empty)
                throw new Exception(string.Format("Input arg cannot be empty. Function Name: {0}", System.Reflection.MethodBase.GetCurrentMethod().Name));

            short[] result = new short[4];
            ulong num = Convert.ToUInt64(ASCIIString);
            short first = (short)(num >> 48);
            short second = (short)(num << 16 >> 32);
            short third = (short)(num << 32 >> 16);
            short fourth = (short)(num << 48);

            result[0] = fourth;
            result[1] = third;
            result[2] = second;
            result[3] = first;

            return result;
        }

        /// <summary>
        /// Convert float string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] FloatToShortArray(string ASCIIString)
        {
            short[] result = new short[2];
            float num = Convert.ToSingle(ASCIIString);
            byte[] bt = BitConverter.GetBytes(num);

            result[0] = Convert.ToInt16(bt[1].ToString("X") + bt[0].ToString("X"), 16);
            result[1] = Convert.ToInt16(bt[3].ToString("X") + bt[2].ToString("X"), 16);

            return result;
        }

        /// <summary>
        /// Convert double string to short array.
        /// </summary>
        /// <param name="ASCIIString">The ASCII string.</param>
        /// <returns></returns>
        public static short[] DoubleToShortArray(string ASCIIString)
        {
            short[] result = new short[4];
            double num = Convert.ToDouble(ASCIIString);
            byte[] bt = BitConverter.GetBytes(num);

            result[0] = Convert.ToInt16(bt[1].ToString("X") + bt[0].ToString("X"), 16);
            result[1] = Convert.ToInt16(bt[3].ToString("X") + bt[2].ToString("X"), 16);
            result[2] = Convert.ToInt16(bt[5].ToString("X") + bt[4].ToString("X"), 16);
            result[3] = Convert.ToInt16(bt[7].ToString("X") + bt[6].ToString("X"), 16);
            return result;
        }

        #endregion
    }

    public static class Extension
    {
        /// <summary>
        /// Swaps the byte in byte array.
        /// </summary>
        /// <param name="buf">The byte array data.</param>
        /// <param name="i">The first position.</param>
        /// <param name="j">The second position.</param>
        public static void SwapBytes(this byte[] buf, int i, int j)
        {
            byte temp = buf[i];
            buf[i] = buf[j];
            buf[j] = temp;
        }
    }
}
