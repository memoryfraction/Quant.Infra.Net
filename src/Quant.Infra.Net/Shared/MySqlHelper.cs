using MySql.Data.MySqlClient;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;


namespace Quant.Infra.Net.Shared
{
    /// <summary>
    /// Provides helper methods to interact with a MySQL database asynchronously.
    /// </summary>
    public static class MySqlHelper
    {
        /// <summary>
        /// Creates and returns a new MySqlConnection using the provided connection string.
        /// </summary>
        /// <param name="connectionString">The connection string to connect to the database.</param>
        /// <returns>A new instance of MySqlConnection.</returns>
        public static MySqlConnection GetConnection(string connectionString)
        {
            return new MySqlConnection(connectionString);
        }

        /// <summary>
        /// Asynchronously executes a SQL command that does not return any results.
        /// </summary>
        /// <param name="conn">The MySQL connection to use.</param>
        /// <param name="cmdType">The command type (e.g., stored procedure, text).</param>
        /// <param name="cmdText">The SQL command text to execute.</param>
        /// <param name="commandParameters">The parameters to pass to the SQL command.</param>
        /// <returns>The number of rows affected.</returns>
        public static async Task<int> ExecuteNonQueryAsync(MySqlConnection conn, CommandType cmdType, string cmdText, params MySqlParameter[] commandParameters)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                await PrepareCommandAsync(cmd, conn, null, cmdType, cmdText, commandParameters);
                int val = await cmd.ExecuteNonQueryAsync();
                cmd.Parameters.Clear();
                return val;
            }
        }

        /// <summary>
        /// Asynchronously executes a SQL command that returns a result set.
        /// </summary>
        /// <param name="conn">The MySQL connection to use.</param>
        /// <param name="cmdType">The command type (e.g., stored procedure, text).</param>
        /// <param name="cmdText">The SQL command text to execute.</param>
        /// <param name="commandParameters">The parameters to pass to the SQL command.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains a DbDataReader.</returns>
        public static async Task<DbDataReader> ExecuteReaderAsync(MySqlConnection conn, CommandType cmdType, string cmdText, params MySqlParameter[] commandParameters)
        {
            MySqlCommand cmd = new MySqlCommand();
            await PrepareCommandAsync(cmd, conn, null, cmdType, cmdText, commandParameters);
            DbDataReader reader = await cmd.ExecuteReaderAsync();
            cmd.Parameters.Clear();
            return reader;
        }

        /// <summary>
        /// Asynchronously executes a SQL command that returns a single value.
        /// </summary>
        /// <param name="conn">The MySQL connection to use.</param>
        /// <param name="cmdType">The command type (e.g., stored procedure, text).</param>
        /// <param name="cmdText">The SQL command text to execute.</param>
        /// <param name="commandParameters">The parameters to pass to the SQL command.</param>
        /// <returns>A task representing the asynchronous operation. The task result contains the value returned by the SQL command.</returns>
        public static async Task<object> ExecuteScalarAsync(MySqlConnection conn, CommandType cmdType, string cmdText, params MySqlParameter[] commandParameters)
        {
            using (MySqlCommand cmd = new MySqlCommand())
            {
                await PrepareCommandAsync(cmd, conn, null, cmdType, cmdText, commandParameters);
                object val = await cmd.ExecuteScalarAsync();
                cmd.Parameters.Clear();
                return val;
            }
        }

        /// <summary>
        /// Prepares a MySqlCommand for execution by setting its properties and parameters.
        /// </summary>
        /// <param name="cmd">The MySqlCommand to prepare.</param>
        /// <param name="conn">The MySQL connection to use.</param>
        /// <param name="trans">The transaction to use, if any.</param>
        /// <param name="cmdType">The command type (e.g., stored procedure, text).</param>
        /// <param name="cmdText">The SQL command text to execute.</param>
        /// <param name="cmdParms">The parameters to pass to the SQL command.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task PrepareCommandAsync(MySqlCommand cmd, MySqlConnection conn, MySqlTransaction trans, CommandType cmdType, string cmdText, MySqlParameter[] cmdParms)
        {
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync();

            cmd.Connection = conn;
            cmd.CommandText = cmdText;
            cmd.CommandType = cmdType;

            if (trans != null)
                cmd.Transaction = trans;

            if (cmdParms != null)
            {
                foreach (MySqlParameter parm in cmdParms)
                    cmd.Parameters.Add(parm);
            }
        }
    }
}