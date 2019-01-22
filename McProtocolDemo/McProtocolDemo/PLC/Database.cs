using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using System.Data.SqlClient;

namespace McProtocolDemo.PLC
{
    public class Database
    {
        public string dbHost;
        public string dbUser;
        public string dbPass;
        public string dbName;

        public Database()
        {
            this.dbHost = "127.0.0.1";//資料庫位址
            this.dbUser = "root";//資料庫使用者帳號
            this.dbPass = "";//資料庫使用者密碼
            this.dbName = "test_mc";//資料庫名稱
        }
        public Database(string dbHost, string dbUser, string dbPass, string dbName)
        {
            this.dbHost = dbHost;//資料庫位址
            this.dbUser = dbUser;//資料庫使用者帳號
            this.dbPass = dbPass;//資料庫使用者密碼
            this.dbName = dbName;//資料庫名稱
        }
        private MySqlConnection establishConnection()
        {
            MySqlConnection connection;
            String MySQLConnectionString = "server=" + dbHost + ";uid=" + dbUser + ";pwd=" + dbPass + ";database=" + dbName;
            connection = new MySqlConnection(MySQLConnectionString);
            return connection;
        }

        public void Insert(string name,ref short[] value,int size,int startaddress)
        {
            String query;
            for (int i = 0; i < size; i++)
            {
                var startaddress_string = Convert.ToString(startaddress + i);
                string index = name + startaddress_string;
                query = "INSERT INTO d(address,num) values('" + index + "','" + value[i] + ")";
                MySqlConnection connection = establishConnection();
                MySqlCommand command = new MySqlCommand(query, connection);
                command.CommandTimeout = 60;

                try
                {
                    connection.Open();

                    command.CommandText = "INSERT INTO d(address,num) values('" + index + "'," + value[i] + ")";
                    command.ExecuteNonQuery();

                    connection.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}