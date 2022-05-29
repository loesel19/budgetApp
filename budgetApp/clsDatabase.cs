using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
namespace budgetApp
{
    public static class clsDatabase
    { 
       
        public static Boolean ExecuteSQLNonQuery(string strSQL, string connString)
        {
            //get connection to server
            
            NpgsqlConnection conn = new NpgsqlConnection(connString);
            NpgsqlCommand cmd = new NpgsqlCommand(strSQL, conn);
            try
            {
                conn.Open();

            }catch (Exception ex)
            {
                return false;
            }
            try
            {
                cmd.ExecuteNonQuery();
                conn.Close();
                return true;
            }
            catch(Exception ex)
            {
                conn.Close();
                return false;
            }
        }
    }
}
