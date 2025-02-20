﻿using System.Text;
using MySql.Data.MySqlClient;

namespace S2Lobby
{
    public class Accounts
    {
        private MySqlConnection conn;

        public void Init(Program program)
        {

            string server = Config.Get("database/mysql/ip");
            string port = Config.Get("database/mysql/port");
            string database = Config.Get("database/mysql/name");
            string user = Config.Get("database/mysql/user");
            string password = Config.Get("database/mysql/pass");
            
            var connString =
                $"server={server};" +
                $"port={port};" +
                $"database={database};" +
                $"user id={user};" +
                $"password={password};" +
                "charset=utf8; pooling=true;";

            conn = new MySqlConnection(connString);

            try
            {    
                conn.Open();

                MySqlTransaction transaction = conn.BeginTransaction();

                StringBuilder cmd = new StringBuilder();
                cmd.AppendLine("CREATE TABLE IF NOT EXISTS accounts (");
                cmd.AppendLine("    account_id          INTEGER PRIMARY KEY AUTO_INCREMENT");
                cmd.AppendLine(",   user_name           VARCHAR(100) NOT NULL");
                cmd.AppendLine(",   user_name_upper     VARCHAR(100) NOT NULL");
                cmd.AppendLine(",   user_password       BLOB NOT NULL");
                cmd.AppendLine(",   user_cdkey          BLOB NOT NULL");
                cmd.AppendLine(",   user_email          VARCHAR(255)");
                cmd.AppendLine(",   user_data           BLOB");
                cmd.AppendLine(",   player_nickname     VARCHAR(100)");
                cmd.AppendLine(");");

                MySqlCommand command = conn.CreateCommand();
                command.CommandText = cmd.ToString();
                command.Transaction = transaction;

                command.ExecuteNonQuery();

                transaction.Commit();

                command.Dispose();
                transaction.Dispose();

                conn.Close();
                conn.Dispose();

                Logger.Log("[Account database ready]");
            } catch (System.Exception e)
            {
                Logger.Log("[Failed to access account database]");
                Logger.Log(connString);
                Logger.Log(e.Message);

                System.Environment.Exit(1);
            }
        }

        public uint Create(MySqlConnection mysql, string username, byte[] password, byte[] cdKey)
        {
            Account account = Get(mysql, username);
            if (account != null)
            {
                return 0;
            }

            long insertedId = -1;
            StringBuilder cmd = new StringBuilder();
            cmd.AppendLine("INSERT INTO accounts (user_name, user_name_upper, user_password, user_cdkey) VALUES (?p1, ?p2, ?p3, ?p4);");

            MySqlTransaction transaction = mysql.BeginTransaction();

            MySqlCommand command = mysql.CreateCommand();
            command.CommandText = cmd.ToString();
            command.Transaction = transaction;

            command.Parameters.Add(new MySqlParameter("p1", username));
            command.Parameters.Add(new MySqlParameter("p2", username.ToUpperInvariant()));
            command.Parameters.Add(new MySqlParameter("p3", password));
            command.Parameters.Add(new MySqlParameter("p4", cdKey));

            command.ExecuteNonQuery();
            insertedId = command.LastInsertedId;

            transaction.Commit();

            command.Dispose();
            transaction.Dispose();

            return (uint)insertedId;
        }

        public Account Get(MySqlConnection mysql, string name)
        {
            return GetInternal(mysql, "user_name_upper", new MySqlParameter("searchCondition", name.ToUpperInvariant()));
        }

        public Account Get(MySqlConnection mysql, uint id)
        {
            return GetInternal(mysql, "account_id", new MySqlParameter("searchCondition", id));
        }

        private Account GetInternal(MySqlConnection mysql, string searchKey, MySqlParameter searchCondition)
        {
            StringBuilder cmd = new StringBuilder();
            cmd.AppendLine("SELECT ");
            cmd.AppendLine("    account_id");
            cmd.AppendLine(",   user_name");
            cmd.AppendLine(",   user_password");
            cmd.AppendLine(",   user_cdkey");
            cmd.AppendLine(",   user_email");
            cmd.AppendLine(",   user_data");
            cmd.AppendLine(",   player_nickname");
            cmd.AppendLine("FROM accounts");
            cmd.AppendLine($"WHERE {searchKey} = ?searchCondition;");

            MySqlCommand command = new MySqlCommand(cmd.ToString(), mysql);
            command.Parameters.Add(searchCondition);

            using (MySqlDataReader reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    Account account = new Account()
                    {
                        Id = uint.Parse(reader["account_id"].ToString()),
                        UserName = reader["user_name"] as string,
                        Password = reader["user_password"] as byte[],
                        CdKey = reader["user_cdkey"] as byte[],
                        Email = reader["user_email"] as string,
                        UserData = reader["user_data"] as byte[],
                        PlayerName = reader["player_nickname"] as string,
                    };
                    reader.Close();
                    command.Dispose();
                    return account;
                }
                else {
                    Logger.LogDebug("Failure on Account creation");
                }              
            };
            return null;
        }

        public void SetNickname(MySqlConnection mysql, uint id, string name)
        {
            StringBuilder cmd = new StringBuilder();
            cmd.AppendLine("UPDATE accounts SET player_nickname=?p1 WHERE account_id=?p2;");

            MySqlTransaction transaction = mysql.BeginTransaction();

            MySqlCommand command = mysql.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = cmd.ToString();

            command.Parameters.Add(new MySqlParameter("p1", name));
            command.Parameters.Add(new MySqlParameter("p2", id));

            command.ExecuteNonQuery();

            transaction.Commit();

            command.Dispose();
            transaction.Dispose();
        }

        public void SetEmail(MySqlConnection mysql, uint id, string email)
        {
            StringBuilder cmd = new StringBuilder();
            cmd.AppendLine("UPDATE accounts SET user_email=?p1 WHERE account_id=?p2;");

            MySqlTransaction transaction = mysql.BeginTransaction();

            MySqlCommand command = mysql.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = cmd.ToString();

            command.Parameters.Add(new MySqlParameter("p1", email));
            command.Parameters.Add(new MySqlParameter("p2", id));

            command.ExecuteNonQuery();

            transaction.Commit();

            command.Dispose();
            transaction.Dispose();
        }

        public void SetUserData(MySqlConnection mysql, uint id, byte[] data)
        {
            StringBuilder cmd = new StringBuilder();
            cmd.AppendLine("UPDATE accounts SET user_data=?p1 WHERE account_id=?p2;");

            MySqlTransaction transaction = mysql.BeginTransaction();

            MySqlCommand command = mysql.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = cmd.ToString();

            command.Parameters.Add(new MySqlParameter("p1", data));
            command.Parameters.Add(new MySqlParameter("p2", id));

            command.ExecuteNonQuery();

            transaction.Commit();

            command.Dispose();
            transaction.Dispose();
        }
    }

    public class Account
    {
        public uint Id;
        public string UserName;
        public byte[] Password;
        public byte[] CdKey;
        public string Email;
        public byte[] UserData;
        public string PlayerName;

        public string GetUserNameStripped()
        {
            return UserName.Substring(0, UserName.Length - 1);
        }
    }
}
