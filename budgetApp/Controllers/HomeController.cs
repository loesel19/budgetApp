using budgetApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace budgetApp.Controllers
{
    /* AUTHOR : Andrew A. Loesel
     * Organization : None. This is a personal project :)
     * Purpose : This controller will service the home (*at this point only*) directory for my budgetapp ?budgetIO?
     *           The purpose of this application is to replace spreadsheets (even though I will still probably use them)
     *           as a means to monitor my income/ spending. This application allows for account creation/ signing, creating
     *           expense/ income entrys which can be sorted by categories, and customizable report viewing, ***and at some point
     *           I would like to implement a feature for downloading a csv of the reports***.
     * Connected Services : This app uses github for VCS. It is pipelined to Azure for deployment. Lastly, a Heroku postgres sql server
     *                      is used for cloud hosted data persistence. */
    public class HomeController : Controller
    {
        //add our config
        private readonly IConfiguration config;
        public HomeController(IConfiguration config)
        {
            this.config = config;
        }
        [HttpGet]
        public IActionResult Index()
        {
            /* This is the HTTPGet method for our index page. The way I implemented this
            /* check if the username is populated to ensure that user is signed in */
            if (String.IsNullOrEmpty(GlobalVariables.GlobalUsername))
            {
                try
                {
                    string username = Request.Cookies["user"].ToString();
                    /* user was previously signed in and wanted to be remembered, so we can sign them back in */
                    GlobalVariables.GlobalUsername = username;
                    /* we also want to get the userID for this user from the pgsql table. */
                    string strSQL = "SELECT * FROM users Where Username = '" + username + "';";
                    /******* since we use postgres sql here we have to have an air of caution around our transactions. Postgres
                     * can be fussy about what transactions it allows, so for some instances (like deletion) we will need to encapsulate those
                     * transactions with a npgsqlTransaction object. For reading and inserting it will be fine to just make an instance of our database class
                     * and execute our sql... hopefully. */
                    //make an instance of our database object
                    clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
                    //try to open the connection
                    if (!objDB.openConnection())
                    {
                        //connection failed. here we should throw an exception since we let down the user this early no point in trying to front about it
                        objDB.Dispose(); //dispose of our disappointment of an object in case the user is gracious enough to give the application another try
                        throw new NpgsqlException("connection with database failed");
                    }
                    NpgsqlDataReader reader = objDB.ExecuteDataReader(strSQL);
                    try
                    {
                        if (reader.Read())
                        {
                            GlobalVariables.UserID = int.Parse(reader["Id"].ToString());
                        }
                    } catch (Exception ex)
                    {
                        //note we did not check if reader was null, that will be caught here
                        //we want to close any open objects like the reader and connection
                        reader.Close();
                        objDB.closeConnection();
                        //now we can dispose of objDB as well
                        objDB.Dispose();
                        return RedirectToAction("SignIn");
                    }
                    //clean up objects and dispose of objDB
                    reader.Close();
                    objDB.closeConnection();
                    objDB.Dispose();
                    return View();
                }catch(Exception ex)
                {
                    /* no user signed in so we want to redirect user to signin page*/
                    return RedirectToAction("SignIn");
                }
                
            }
            else
            {
                /* user is signed in so we will return the index page */
                return View();
            }
        }

        [HttpPost]
        public IActionResult Index([FromForm] EntryModel model)
        {
            /* we want to try to post the entry to our database */
            //first make a sql string
            if (String.IsNullOrEmpty(model.subCategory))
            {
                model.subCategory = "";
            }
            if (String.IsNullOrEmpty(model.description))
            {
                model.description = "";
            }
            //since the user will be able to choose if they want to use a date that is not today we need to check for that
            /* make a variable for date, do it as a datetime value since that is the type we use in our model */
            DateTime insertDate = DateTime.Now;
            //we set it to now first, if user wants another date we change it
            if (model.otherDate)
            {
                insertDate = model.date;
            }
            string sqlCommand = String.Format("INSERT INTO entrys (amount, category, subcategory, description, userID, createdtime) values({0}, {1}, {4}, {2}, {3}, {5});",
                "'" + model.amount + "'", "'" + model.category + "'", "'" + model.description + "'", "'" + GlobalVariables.UserID + "'", "'" + model.subCategory + "'", 
                "'" + insertDate + "'");

            //make an instance of our db class
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
            //try to open the connection
            if (!objDB.openConnection())
            {
                //send a message to the GUI, and dispose of the dud object
                ViewBag.Message = "Could not open database connection.";
                objDB.Dispose();
                return View();
            }
            //now try to insert the new row
            if (objDB.ExecuteSQLNonQuery(sqlCommand))
            {
                /* we were able to add the entry to our database */
                ViewBag.Message = "Successfully added entry.";
            }
            else
            {
                ViewBag.Message = "Something went wrong, entry not posted.";

            }
            //now cleanup open connection and dispose of our database object
            objDB.closeConnection();
            objDB.Dispose();
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
        [HttpGet]
        public IActionResult SignIn()
        {
            /* if global user name value is not populated we return the signing page
             * otherwise we redirect to the index page. */
           
            if(String.IsNullOrEmpty(GlobalVariables.GlobalUsername))
                return View();
            else
                return RedirectToAction("Index");
        }
        [HttpGet]
        public IActionResult Report()
        {
            ReportModel model = new ReportModel();
            return View(model);
        }
        [HttpPost]
        public IActionResult Report(ReportModel model)
        {
            /* we need to make a report, lets do it as a table to make it look nice. We will want to use a string builder 
             * and a couple switch statements since there are a lot of differenct reports we can generate. */
            StringBuilder sbSQL = new StringBuilder("SELECT * From entrys WHERE userID = '" + GlobalVariables.UserID + "'");
            switch (model.category)
            {
                case "All":

                    break;
                default:
                    sbSQL.Append(" AND Category = '" + model.category + "'");
                    break;
            }
            switch (model.period)
            {
                case "YTD":
                    sbSQL.Append(" AND date_part('year', CreatedTime) = date_part('year', now())");
                    break;
                case "All":
                    break;
                default:
                    sbSQL.Append(" AND CreatedTime >= '" + DateTime.Now.AddDays(-1 * int.Parse(model.period)) + "'");
                    break;
            }
            sbSQL.Append(" ORDER BY CreatedTime DESC;");
            //make an instance of our db class.
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
            //see if we can open the connection
            if (!objDB.openConnection())
            {
                //rats. notify the user that something went wrong opening the connection through a GUI message.
                ViewBag.Message = "Unable to open Database connection.";
                objDB.Dispose(); //cleanup the DB object so that no funny business happens if a new one is made.
                //return the view
                return View();
            }
            //create a data reader based off the query we built.
            NpgsqlDataReader sdr = objDB.ExecuteDataReader(sbSQL.ToString());
            /* lets start building our table, put in the headers first. Space them nicely as well */
            StringBuilder strTable = new StringBuilder("<table class=\"table\" id=\"reportTable\" style=\"table-layout: fixed; border-collapes: collapse;\">");
            strTable.AppendLine("   <tr>");
            strTable.AppendLine("       <th class=\"th-md\">entryID</th>");
            strTable.AppendLine("       <th class=\"th-md\">Amount</th>");
            strTable.AppendLine("       <th class=\"th-md\">Category</th>");
            strTable.AppendLine("       <th class=\"th-md\">SubCategory</th>");
            strTable.AppendLine("       <th class=\"th-md\">Description</th>");
            strTable.AppendLine("       <th class=\"th-md\">Date</th>");
            strTable.AppendLine("       <th class=\"th-md\" id=\"editStateHead\"><img src=\"/lib/images/lock.jpg\" class=\"img-header\" onclick=\"checkEditState()\"/></th>");
            strTable.AppendLine("   </tr>");

            /* now we need to loop through all the results returned by our datareader, and add them as a row to the table */
            if (sdr == null)
            {
                ViewBag.Msg = "Something went wrong trying to read the data";
                model.strHTMLTable = "";
                return View(model);
            }
            /* change Msg to nothing here in case something went wrong prior */
            ViewBag.Msg = "";
            double spent = 0;
            double income = 0;
            int count = 0; //counter for assigning ids
            while (sdr.Read())
            {
                /* we can check the category to both set the background color, and to keep total gross and total net spending */
                if(sdr["Category"].ToString() == "Income")
                {
                    strTable.AppendLine("<tr style=\"background-color:#00FF7F;\">");
                    try
                    {
                        income += double.Parse(sdr["Amount"].ToString());
                    } catch { 
                    //nothing to do if amount is null. it shouldn't happen since we validate before entering into the database
                    }
                }
                else
                {
                    strTable.AppendLine("   <tr style=\"background-color:#CD5C5C;\">");
                    try
                    {
                        spent += double.Parse(sdr["Amount"].ToString());
                    }
                    catch
                    {
                        //nothing to do if amount is null. it shouldn't happen since we validate before entering into the database
                    }
                }
                strTable.AppendLine("       <td class=\"td-md\" id=\"entryID" + count + "\">" + sdr["entryID"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"amt" + count + "\">$" + sdr["Amount"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"cat" + count + "\">" + sdr["Category"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"sub" + count + "\">" + sdr["Subcategory"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"des" + count + "\">" + sdr["Description"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"crt" + count + "\">" + sdr["Createdtime"] + "</td>");
                strTable.AppendLine("       <td class=\"td-md\" id=\"colEdit" + count + "\"><input type=\"button\" class=\"btn btn-secondary btn-sm btn-row\" onclick=\"editEntry(event, this.id)\" id=\"edt" + count + "\"/>" +
                    " &nbsp <input type=\"button\" class=\"btn btn-secondary btn-sm btn-row\" onclick=\"deleteEntry(event, this.id)\" id=\"del" + count + "\"/>");
                strTable.AppendLine("   </tr>");
                //increment counter
                count++;
            }
            //dont forget to close the table tag
            strTable.AppendLine("</table>");
            //now we need to set the totals as well
            model.strHTML = "<table><tr><th class=\"th-md\">Total Spent</th>" +
                "<td>&nbsp$" + spent + "</td>" + "<th class=\"th-md\">Total Net</th>" +
                "<td>&nbsp$" + (income - spent) + "</td></tr></table>";
            model.strHTMLTable = strTable.ToString();
            //clean up loose ends with the datareader, connection and lastly the object.
            sdr.Close();
            objDB.closeConnection();
            objDB.Dispose();
            return View(model);
        }
        [HttpPost]
        public IActionResult SignIn(UserModel model)
        {
           
            string strSQL = "SELECT userID, username FROM users WHERE username = '" + model.username + "' AND password = '" + validHash(model.password) + "';";
            //make an instance of our db class.
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
            //try to open the connection
            if (!objDB.openConnection())
            {
                //mission failed. let the user know what a disgrace this application is with a GUI message
                ViewBag.Msg = "Could not open database Connection.";
                //dispose of our failure, just to so that no one will see it ever again
                objDB.Dispose();
                //give the user something to look at
                return View();
            }
            NpgsqlDataReader reader = objDB.ExecuteDataReader(strSQL);
            if(!(reader == null))
            {
                if (reader.Read())
                {
                    //correct signin credentials
                    try
                    {
                        GlobalVariables.GlobalUsername = model.username;
                        GlobalVariables.UserID = int.Parse(reader["userID"].ToString());
                    }
                    catch (Exception ex)
                    {
                        //something wrong with the data
                        ViewBag.Msg = "Account error.";
                        //close the loose connection and reader
                        reader.Close();
                        objDB.closeConnection();
                        //dispose of the objDB instance
                        objDB.Dispose();
                        return View();
                    }
                    //before we serve the webpage we need to close our reader, connection, and dispose of the objDB instance
                    reader.Close();
                    objDB.closeConnection();
                    objDB.Dispose();
                    return RedirectToAction("Index");
                }
                //theoretically this point shouldn't be reached, but better safe than sorry.
                //incorrect signin values
                ViewBag.Msg = "Username/password incorrect.";
                //before we serve the webpage we need to close our reader, connection, and dispose of the objDB instance
                reader.Close();
                objDB.closeConnection();
                objDB.Dispose(); 
                return View();
            }
            else
            {
                //incorrect signin values
                ViewBag.Msg = "Username/password incorrect.";
                //we need to close our reader, connection, and dispose of the objDB instance
                reader.Close();
                objDB.closeConnection();
                objDB.Dispose();
                return View();
            }
            
        }
        public IActionResult SignOut()
        {
            /** This method 'signs the user out' right now we just delete the cookie that is stored in the browser,
             * eventually we will have to deal with whatever authentication method is used. */
            GlobalVariables.GlobalUsername = null;
            GlobalVariables.UserID = -1;
            /* we will also want to 'delete' the username cookie */
            try
            {
                Response.Cookies.Delete("user");
            } catch (Exception ex) { throw ex; }

            return RedirectToAction("Index");
        }
        
        [HttpGet]
        public IActionResult signUp()
        {
            /**
             * This is the HTTPGet method for the signup page. We just return the page to the client */
            return View();
        }
        [HttpPost]
        public IActionResult signUp(UserModel model)
        {
            /* data validated in view, but need to make sure the username is unique */
            string strSQL = "SELECT * FROM users WHERE username = '" + model.username + "';";
            // make an instance of our db class
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
            //try to open up a connection
            if (!objDB.openConnection())
            {
                //I am running out of things to say for these. just dispose of the database object and get it out of my face
                objDB.Dispose();
                //oh and letting the user know what happened would be smart. Then we can return the page to them as well
                ViewBag.Msg = "Could not connect to the database.";
                return View();
            }
            NpgsqlDataReader sdr = objDB.ExecuteDataReader(strSQL);
            if (sdr == null)
            {
                /* Note that we handle the null case here specifacally. If there are no users with this username
                * already in the database we would get an empty dataReader, so something else happened either in
                * the code or with the database. */ 
                ViewBag.Msg = "An error occured with the database.";
                //close down the reader, connection and dispose of the database object
                sdr.Close();
                objDB.closeConnection();
                objDB.Dispose();
                return View();
            }
            if (sdr.Read())
            {
                ViewBag.Msg = "Username already taken.";
                //close down the reader, connection and dispose of the database object
                sdr.Close();
                objDB.closeConnection();
                objDB.Dispose();
                return View();
            }
            else
            {
                //close our datreader
                sdr.Close();
                //lets hash the password before storing it in the database
                
                /* we can sign up the new user */
                strSQL = "INSERT INTO users (username, password) values ('" + model.username + "', '" + validHash(model.password) + "');";
                if(objDB.ExecuteSQLNonQuery(strSQL)){
                    /* user was signed up successfully */
                    ViewBag.Msg = "Account created succesfully please signin.";
                    //close down the reader, connection and dispose of the database object
                    sdr.Close();
                    objDB.closeConnection();
                    objDB.Dispose();
                    return RedirectToAction("SignIn");
                }
                else
                {
                    ViewBag.Msg = "Something went wrong creating your account, please try again.";
                    //close down the reader, connection and dispose of the database object
                    sdr.Close();
                    objDB.closeConnection();
                    objDB.Dispose();
                    return View();
                }
            }
        }
        public bool deleteEntry([FromQuery] int entryID)
        {
            /* this method will be accessed through an ajax call in the report page. The user wants to delete a row from the database 
             * here we will have to encapsulate our sql commands in a npgsqlTransaction. */
            //make an instance of our db object
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));

            //lets get a connection from our database class
            NpgsqlConnection conn = objDB.getConnection();
            //and try to open that connection
            try
            {
                conn.Open();
            }catch (Exception ex)
            {
                ViewBag.Msg = "could not open database connection. Deletion has been aborted.";
                return false;
            }
            try
            {
                /* npgsql is actually quite fussy about what you can do. so we need to
                 * delete from a transaction clause instead of through a straight up query, or
                 * else it will seem like our changes were made, but the database will not commit that deletion. */
                using (NpgsqlTransaction tr = conn.BeginTransaction())
                {
                    string stringSql = "DELETE From entrys WHERE entryID = " + entryID + ";";
                    //execute the sql statement
                    try
                    {
                        NpgsqlCommand cmd = new NpgsqlCommand(stringSql, conn);
                        cmd.Transaction = tr;
                        cmd.ExecuteNonQuery();
                        //commit the changes to the db
                        tr.Commit();
                        //close connection
                        conn.Close();
                        //dispose of the database object
                        objDB.Dispose();
                        //close the transaction as well, just to be safe. I do not trust it :\
                        tr.Dispose();
                        //return true to the ajax function this is called from
                        return true;
                    }catch (Exception ex)
                    {
                        //the delete did not work, rollback any changes made to our db
                        tr.Rollback();
                        //close conn, dispose db object and return false
                        conn.Close();
                        
                        objDB.Dispose();
                        return false;
                    }
                    
                }
            }catch (Exception ex)
            {
                //just return false here
                return false;
            }
        }
        public bool editEntry([FromQuery] string date, [FromQuery] string description, [FromQuery] string amount, [FromQuery] string category, [FromQuery] string subcategory)
        {
            return false;
        }
        private string validHash([FromQuery] string strNormal)
        {
            /**
             * @name : validHash
             * @author : Andrew A. Loesel
             * @params : strHash - the hashed string that we need to make sure is valid
             * @returns : strValid - a valid hashed string
             * @purpose : The purpose of this method is to hash the passowrd and then make sure the hashed version of the password we send to the database does not contain any characters
             *            that could mess up our database, for example if the password we try to send is x23's4 the ' will make the database think that s4 is some random
             *            garbage and will not accept any transactions with that string.
             *            */
            string strValid = "";
            
            byte[] hashPassword;
            using (HashAlgorithm hash = SHA256.Create())
            {
                hashPassword = hash.ComputeHash(Encoding.UTF8.GetBytes(strNormal));
            }
            var strHashed = System.Text.Encoding.Default.GetString(hashPassword);
            strValid = strHashed.Replace('\'', '!');
            strValid = strValid.Replace('\"', '?');
            strValid = strValid.Replace(';', '*');

            return strValid;
        }
        public string readUserCookie()
        {
            string x = Request.Cookies["user"];
            return x;
        }
        public string readSessionCookie()
        {
            string x = Request.Cookies["validSession"];
            return x;
        }
    } 
}