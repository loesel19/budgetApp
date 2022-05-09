using System.Data.SqlClient;
namespace budgetApp
{
    public class clsDatabase
    {


        public static Boolean ExecuteSQLNonQuery(String strSQL)
        {
            SqlConnection conn = null;
            SqlCommand cmd = new SqlCommand();
            try
            {
                conn.Open();

            }catch (Exception ex)
            {
                return false;
            }
            cmd.CommandText = strSQL;
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
