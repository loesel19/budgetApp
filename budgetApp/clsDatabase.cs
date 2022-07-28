using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
namespace budgetApp
{
    public class clsDatabase
    { 
        private NpgsqlConnection _connection; //our connection with the database
        private bool _connected = false;
        /* The following methods are more rudementary setup methods. First is a parameterized constructor and the next 2 are 
         * control methods for the database. Note that we will need to open and close the connection everytime we want to do
         * something with an instance of this class in the controller. After implementing this as a static class I did some 
         * researching and thinking and think that for this instance it is better implement this as a nonstatic class, and 
         * make sure to clean up after ourselves in the controller, leaving no loose connections open */
        public clsDatabase(string strConn)
        {
            /* parameterized constructor, here we will take in the connection string to connect to our database. an 
             * assign field _connection to a new npgsqlConnection with that connection string. The reason we do not
             * do this in a default constructor is because we grab that conn string from the config, which can only be
             * accessed in the controller. */
            _connection = new NpgsqlConnection(strConn);
        }
        public bool openConnection()
        {
            /* here we just want to open the connection that should have received its connection string when the object was 
             * constructed. We return true if the connection was sucessfully opened otherwise we return false. */
            try
            {
                _connection.Open();
                //set our connected boolean to true
                _connected = true;
                return true;
            } catch (Exception ex)
            {
                return false;
            }
        }
        public bool closeConnection()
        {
            /* all this method will do is close the connection, and return true if it succeeds in that. Otherwise return false */
            try
            {
                _connection.Close();
                //set the connected boolean to false
                _connected = false;
                return true;
            }catch (Exception ex)
            {
                return false;
            }
        }
        public NpgsqlConnection getConnection()
        {
            /* this method will return our connection. It is needed for when we use transactions. */
            return _connection;
        }
        /* the following method is a dispose method */
        public void Dispose()
        {
            /* by calling dispose here we will clean up any hanging connections or other loose ends within the class. 
             * This method should be called everytime we finish using the clsDatabase object to keep our application
             * running smoothing on this end. */
            if (_connection != null)
                _connection.Dispose();
        }
       // The following subprograms are used to interact with data in our database
        public Boolean ExecuteSQLNonQuery(string strSQL)
        {
            /* This method will be used to execute any npgsql commands that do not provide results. 
             * if the command is executed we will return true, and return false if an exception occurrs
             * so that we can handle it in our controller and update the GUI accordingly. */

            //first lets check if we are connected to the database.
            if (!_connected)
            {
                //we can not do anything without a connection, so return false.
                return false;
            }
            //create a new npgsql command to execute our nonquery
            NpgsqlCommand cmd = new NpgsqlCommand(strSQL, _connection);
            //now try to execute it and return true on success, and false on failure.
            try
            {
                cmd.ExecuteNonQuery();
                return true;
            }
            catch(Exception ex)
            {
                return false;
            }
        }
        public NpgsqlDataReader ExecuteDataReader(string strSQL)
        {
            //see if the connection is opened
            if (!_connected)
            {
                //return null here since without an open connection we cannot get a data reader
                return null;
            }
            //make a new command
            NpgsqlCommand cmd = new NpgsqlCommand(strSQL, _connection);
            //try to execute our command and return the results in a reader.
            try
            {
                NpgsqlDataReader reader = cmd.ExecuteReader();
                return reader;
            }catch(Exception ex)
            {
                return null;
            }
        }
    }
}
