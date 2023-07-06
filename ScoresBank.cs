using System.Data.SQLite;
using System.IO;
using System;

namespace ChatInteractiveCommands
{
    public struct SUserRecord
    {
        public string idstring;
        public DateTime registration_time;
        public string registration_name;
        public DateTime last_visit_time;
        public string last_visit_name;
        public DateTime last_bonus_time;
        public int scores;

        public static SUserRecord GetEmpty()
        {
            SUserRecord result = new SUserRecord();
            result.idstring = "";

            var now = DateTime.Now;
            result.registration_time = now;
            result.registration_name = "";
            result.last_visit_time = now;
            result.last_visit_name = "";
            result.last_bonus_time = now;
            result.scores = 0;

            return result;
        }
    }


    interface IScoresBank
    {
        bool Init();        

        SUserRecord GetUserRecord(string idstring);
        bool RegisterUser(string idstring, string username, int initial_scores);
        bool UpdateUser(string idstring, string username, int new_scores, bool is_bonus);
    }

    public class ScoresBank: IScoresBank
    {
        const string DB_FILENAME = "scores.db";

        public bool Init()
        {            
            const string INIT_PERSONS = "CREATE TABLE IF NOT EXISTS [persons] ( [id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, [idstring] NVARCHAR(256) NOT NULL UNIQUE, [registration_time] NVARCHAR(30), [registration_name] NVARCHAR(256), [last_visit_time] NVARCHAR(30), [last_visit_name] NVARCHAR(256), [last_bonus_time] NVARCHAR(256), [scores] INTEGER NOT NULL)";

            if (!File.Exists(DB_FILENAME))
            {
                SQLiteConnection.CreateFile(DB_FILENAME);
            }

            using (var connection = EstabilishConnection())
            {
                using (var cmd = new SQLiteCommand(INIT_PERSONS, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }

            return true;
        }

        protected SQLiteConnection EstabilishConnection()
        {
            var connection = new SQLiteConnection(string.Format("Data Source={0};", DB_FILENAME));
            connection.Open();
            return connection;
        }

        protected int GetUserIdByIdString(string idstring)
        {
            const string QUERY = "SELECT id FROM [persons] WHERE [idstring] = @idstring";
            
            int result = -1;
            using (var connection = EstabilishConnection())
            {
                using (var cmd = new SQLiteCommand(QUERY, connection))
                {
                    cmd.Parameters.AddWithValue("@idstring", idstring);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = reader.GetInt32(0);
                        }
                    }
                }
            }

            return result;
        }

        public SUserRecord GetUserRecord(string idstring)
        {
            SUserRecord result = SUserRecord.GetEmpty();

            const string QUERY = "SELECT [idstring], [registration_time], [registration_name], [last_visit_time], [last_visit_name], [last_bonus_time], [scores]  FROM [persons] WHERE [idstring] = @idstring";

            using (var connection = EstabilishConnection())
            {
                using (var cmd = new SQLiteCommand(QUERY, connection))
                {
                    cmd.Parameters.AddWithValue("@idstring", idstring);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int i = 0;
                            result.idstring = reader.GetString(i++);
                            result.registration_time = FromDateSQLite(reader.GetString(i++));
                            result.registration_name = reader.GetString(i++);
                            result.last_visit_time = FromDateSQLite(reader.GetString(i++));
                            result.last_visit_name = reader.GetString(i++);
                            result.last_bonus_time = FromDateSQLite(reader.GetString(i++));
                            result.scores = reader.GetInt32(i++);
                        }
                    }
                }
            }

            return result;
        }

        public bool RegisterUser(string idstring, string username, int initial_scores)
        {
            if (GetUserIdByIdString(idstring) >= 0) return false;

            string now = ToDateSQLite(DateTime.Now);

            const string QUERY = "INSERT INTO [persons] ([idstring], [registration_time], [registration_name], [last_visit_time], [last_visit_name], [last_bonus_time], [scores]) VALUES(@idname, @registration_time, @registration_name, @last_visit_time, @last_visit_name, @last_bonus_time, @scores);";
            using (var connection = EstabilishConnection())
            {
                using (var cmd = new SQLiteCommand(QUERY, connection))
                {
                    cmd.Parameters.AddWithValue("@idname", idstring);
                    cmd.Parameters.AddWithValue("@registration_time", now);
                    cmd.Parameters.AddWithValue("@registration_name", username);
                    cmd.Parameters.AddWithValue("@last_visit_time", now);
                    cmd.Parameters.AddWithValue("@last_visit_name", username);
                    cmd.Parameters.AddWithValue("@last_bonus_time", now);
                    cmd.Parameters.AddWithValue("@scores", initial_scores);

                    cmd.ExecuteNonQuery();
                }
            }

            return true;
        }

        public bool UpdateUser(string idstring, string username, int new_scores, bool is_bonus)
        {
            bool result = false;
            string now = ToDateSQLite(DateTime.Now);

            int id = GetUserIdByIdString(idstring);

            if (id < 0)
            {
                result = false;
            }
            else if (!is_bonus)
            {
                const string QUERY = "UPDATE [persons] SET [last_visit_time] = @last_visit_time, [last_visit_name] = @last_visit_name, [scores] = @scores WHERE [idstring] = @idstring;";
                using (var connection = EstabilishConnection())
                {
                    using (var cmd = new SQLiteCommand(QUERY, connection))
                    {
                        cmd.Parameters.AddWithValue("@last_visit_time", now);
                        cmd.Parameters.AddWithValue("@last_visit_name", username);
                        cmd.Parameters.AddWithValue("@scores", new_scores);
                        cmd.Parameters.AddWithValue("@idstring", idstring);
                        int cnt = cmd.ExecuteNonQuery();

                        result = (cnt == 1);
                    }
                }
            }
            else
            {
                const string QUERY = "UPDATE [persons] SET [last_visit_time] = @last_visit_time, [last_visit_name] = @last_visit_name, [scores] = @scores, [last_bonus_time] = @last_bonus_time WHERE [idstring] = @idstring;";
                using (var connection = EstabilishConnection())
                {
                    using (var cmd = new SQLiteCommand(QUERY, connection))
                    {
                        cmd.Parameters.AddWithValue("@last_visit_time", now);
                        cmd.Parameters.AddWithValue("@last_visit_name", username);
                        cmd.Parameters.AddWithValue("@scores", new_scores);
                        cmd.Parameters.AddWithValue("@last_bonus_time", now);
                        cmd.Parameters.AddWithValue("@idstring", idstring);
                        int cnt = cmd.ExecuteNonQuery();
                        result = (cnt == 1);
                    }
                }
            }
            return result;
        }


        const string SQL_DATETIME_FORMAT = "yyyy-MM-dd HH:mm:ss.fff";
        public static string ToDateSQLite(DateTime value)
        {
            return value.ToString(SQL_DATETIME_FORMAT);
        }

        public static DateTime FromDateSQLite(string s)
        {
            return DateTime.ParseExact(s, SQL_DATETIME_FORMAT, null);
        }
    }
}
