using budgetApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

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
                    if (!generateSessionCookie())
                    {
                        // we try to generate a session cookie, but can't so there is no cookie for the user
                        return RedirectToAction("SignIn");
                    }
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
                            GlobalVariables.UserID = int.Parse(reader["Userid"].ToString());
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
            //check to see if there is an open session for the user
            if (!checkSession())
            {
                ViewBag.Message = "No open session, please sign out and sign back in. ";
                return View();
            }
            //first make a sql string
            if (String.IsNullOrEmpty(model.subCategory))
            {
                model.subCategory = "";
            }
            if (String.IsNullOrEmpty(model.description))
            {
                model.description = "";
            }
            //create our sql command
            
            string sqlCommand = String.Format("INSERT INTO entrys (amount, category, subcategory, description, userID, createdtime) values({0}, {1}, {4}, {2}, {3}, {5});",
                "'" + model.amount + "'", "'" + model.category + "'", "'" + model.description + "'", "'" + GlobalVariables.UserID + "'", "'" + model.subCategory + "'", 
                "'" + model.date + "'");

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
            //check to see if there is an open session for the user
            if (!checkSession())
            {
                ViewBag.Message = "No open session, please sign out and sign back in. ";
                return View();
            }
            ReportModel model = new ReportModel();
            return View(model);
        }
        private string repoortSQLString(ReportModel model)
        {
            /**
             * Name : reportSQLString
             * Params : model - a model with data pertaining to the report
             * Returns : a string that will be built based off of the data in model.
             * Purpose : the purpose of this method is to create a sql query for the report that the user requests to see.
             *           */
            StringBuilder sbSQL = new StringBuilder("SELECT * From entrys WHERE userID = '" + GlobalVariables.UserID + "'");
            if (model.searchWithText)
            {

                /* we want to search with the textbox, so we should try and match the search string with subcategory and description */
                sbSQL.Append(" AND (subcategory LIKE '%" + model.strSearch + "%' OR description LIKE '%" + model.strSearch + "%')");
            }
            else
            {
                /* searching by the time and category user provided */
                switch (model.category)
                {
                    case "All":

                        break;
                    default:
                        sbSQL.Append(" AND Category = '" + model.category + "'");
                        break;
                }
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
            return sbSQL.ToString();

        }
        private Tuple<string, double, double> reportTbodyString(NpgsqlDataReader sdr)
        {
            
            /* lets start building our table. We will do the headers last since I want to have the total spent and net toward the top. 
             * Space them nicely as well */
            StringBuilder strTbody = new StringBuilder();
            strTbody.AppendLine("   <tbody>");
            /* now we need to loop through all the results returned by our datareader, and add them as a row to the table */
            

            double spent = 0;
            double income = 0;
            int count = 0; //counter for assigning ids
            while (sdr.Read())
            {
                /* we can check the category to both set the background color, and to keep total gross and total net spending */

                if (sdr["Category"].ToString() == "Income")
                {
                    strTbody.AppendLine("       <tr style=\"background-color:#0d6efd;\" id=\"tr" + count + "\" onclick=\"popup(" + count + ")\">");
                    try
                    {
                        income += double.Parse(sdr["Amount"].ToString());
                    }
                    catch
                    {
                        //nothing to do if amount is null. it shouldn't happen since we validate before entering into the database
                    }
                }
                else
                {
                    strTbody.AppendLine("       <tr id=\"tr" + count + "\" onclick=\"popup(" + count + ")\">");
                    try
                    {
                        spent += double.Parse(sdr["Amount"].ToString());
                    }
                    catch
                    {
                        //nothing to do if amount is null. it shouldn't happen since we validate before entering into the database
                    }
                }
                strTbody.AppendLine("           <td class=\"td-md\" id=\"entryID" + count + "\" hidden>" + sdr["entryID"] + "</td>");
                //to formoat our currency properly lets use cultureInfo.CurrentCulture. It should grab the region where the app is running. we will have to parse the value into a double and then a string
                strTbody.AppendLine("           <td class=\"td-md\" id=\"amt" + count + "\">" + double.Parse(sdr["Amount"].ToString()).ToString("C", CultureInfo.CurrentCulture) + "</td>");
                strTbody.AppendLine("           <td class=\"td-md\" id=\"cat" + count + "\">" + sdr["Category"] + "</td>");
                strTbody.AppendLine("           <td class=\"td-md\" id=\"sub" + count + "\">" + sdr["Subcategory"] + "</td>");
                strTbody.AppendLine("           <td class=\"td-md\" id=\"des" + count + "\">" + sdr["Description"] + "</td>");
                strTbody.AppendLine("           <td class=\"td-md\" id=\"crt" + count + "\">" + sdr["Createdtime"] + "</td>");
                strTbody.AppendLine("           <td class=\"td-md\" id=\"colEdit" + count + "\" style=\"display: none;\"><input type=\"button\" class=\"btn btn-warning btn-sm btn-row\" onclick=\"editEntry(event, " + count + ")\" id=\"edt" + count + "\"/>" +
                    " &nbsp <input type=\"button\" class=\"btn btn-danger btn-sm btn-row\" onclick=\"deleteEntry(event, this.id)\" id=\"del" + count + "\"/>");
                strTbody.AppendLine("   </tr>");
                //increment counter
                count++;
            }
            //close the tbody tag
            strTbody.AppendLine("</tbody>");
            //dont forget to close the table tag
            strTbody.AppendLine("</table>");
            return new Tuple<string, double, double>(strTbody.ToString(), spent, income);
        }
        private string reportTableString(Tuple<string, double, double> tup)
        {
            /**
             * Name : reportTableString
             * Params : tup - a tuple containing the table body html string, the spent total and the income total
             * Returns : a string containing the html string for the full report table we are generating.
             * Purpose : the purpose of this method is to generate the full report table. We first create the table head
             *           and then append the head and body and return that string.
             *           */
            //now we do the table head since we can put the total spent and net in our headers
            StringBuilder strThead = new StringBuilder("<table class=\"table table-hover\" id=\"reportTable\" style=\"table-layout: fixed;\">");
            strThead.AppendLine("   <thead>");
            strThead.AppendLine("       <tr>");
            strThead.AppendLine("           <th class=\"th-md\">Spent</th>");

            strThead.AppendLine("           <td>" + tup.Item2.ToString("C", CultureInfo.CurrentCulture) + "</td>");
            strThead.AppendLine("           <th class=\"th-md\">Total net</th>");
            strThead.AppendLine("           <td>" + (tup.Item3 - tup.Item2).ToString("C", CultureInfo.CurrentCulture) + "</td>");
            strThead.AppendLine("       </tr>");
            strThead.AppendLine("       <tr>");
            strThead.AppendLine("           <th class=\"th-md\" hidden>entryID</th>");
            strThead.AppendLine("           <th class=\"th-md\">Amount</th>");
            strThead.AppendLine("           <th class=\"th-md\">Category</th>");
            strThead.AppendLine("           <th class=\"th-md\">SubCategory</th>");
            strThead.AppendLine("           <th class=\"th-md\">Description</th>");
            strThead.AppendLine("           <th class=\"th-md\">Date</th>");
            //strThead.AppendLine("           <th class=\"th-md\" id=\"editStateHead\"><img src=\"/lib/images/lock.jpg\" class=\"img-header\" onclick=\"checkEditState()\"/></th>");
            strThead.AppendLine("       </tr>");
            strThead.AppendLine("   </thead>");
            strThead.AppendLine();
            return strThead.ToString() + tup.Item1.ToString();
        }
        [HttpPost]
        public IActionResult Report(ReportModel model)
        {

            //get the sql query string
            string strSQL = repoortSQLString(model);
            
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
            /* change Msg to nothing here in case something went wrong prior */
            ViewBag.Msg = "";
            //create a data reader based off the query we built.
            NpgsqlDataReader sdr = objDB.ExecuteDataReader(strSQL);

            if (sdr == null)
            {
                ViewBag.Msg = "Something went wrong trying to read the data";
                model.strHTMLTable = "";
                return View(model);
            }
            //generate the html for the table body, along with our total income and spent for this report
            Tuple<string, double, double> tup = reportTbodyString(sdr);
            model.strHTMLTable = reportTableString(tup);
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
            //check to see if there is an open session for the user
            if (!checkSession())
            {
                ViewBag.Message = "No open session, try refreshing the page or closing out the browser. ";
                return RedirectToAction("SignIn");
            }
            GlobalVariables.GlobalUsername = null;
            GlobalVariables.UserID = -1;
            /* we will also want to 'delete' the username and session cookie */
            try
            {
                Response.Cookies.Delete("user");
                Response.Cookies.Delete("validSession");
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
        [HttpGet]
        public IActionResult AccountSettings()
        {
            ChangePasswordModel model = new ChangePasswordModel();
            return View(model);
        }
        [HttpPost]
        public IActionResult AccountSettings(ChangePasswordModel model)
        {
            string strSql = "Select password from users where username = '" + GlobalVariables.GlobalUsername + "';";
            string strHashPwd = validHash(model.OldPassword);
            clsDatabase objDb = new clsDatabase(config["DBConnString"]);
            if (!objDb.openConnection())
            {
                ViewBag.message = "Failed to connect to database";
                return View(model);
            }
            NpgsqlDataReader sdr = objDb.ExecuteDataReader(strSql);
            try
            {
                sdr.Read();
                if(strHashPwd.Equals(sdr["password"].ToString()))
                {
                    sdr.Close();
                    string strNewPwd = validHash(model.NewPassword);
                    if (String.IsNullOrEmpty(strNewPwd))
                    {
                        ViewBag.message = "Something is wrong with the new password. ";
                        return View();
                    }
                    string strUpdate = "UPDATE Users set password = '" + strNewPwd + "';";
                    if (!objDb.ExecuteSQLNonQuery(strUpdate))
                    {
                        ViewBag.message = "Could not update the password. ";
                    }
                    else
                    {
                        ViewBag.message = "Sucessfully changed password.";
                    }

                }
            }catch (Exception ex)
            {
                ViewBag.message = "An unexpected error occurred. ";
            }
            objDb.closeConnection();
            objDb.Dispose();
            return View();
        }
        public bool deleteEntry([FromQuery] int entryID)
        {
            /* this method will be accessed through an ajax call in the report page. The user wants to delete a row from the database 
             * here we will have to encapsulate our sql commands in a npgsqlTransaction. */
            //make an instance of our db object
            //check to see if there is an open session for the user
            if (!checkSession())
            {
                ViewBag.Message = "No open session, please sign out and sign back in. ";
                return false;
            }
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
        public bool isNumber(string strTest)
        {
            /**
             * Name : isNumber
             * Params : strTest - the string that we want to test
             * Returns : bool - true => strTest is a number, false => strTest is not a number
             * Purpose : the purpose of this method is to use the tryParse method on strTest to determine if it is a number or not.
             *           */
            if(!double.TryParse( strTest, out double d))
            {
                //not a double
                return false;
            }
            //is a double
            return true;
        }
        public string replaceSemiColon(string strReplace)
        {
            /**
             * Name : replaceSemiColon
             * Params : strReplace - the string which will need semicolons replaced
             * Returns : a string containing no semicolons
             * Purpose : in this function we will replace any semicolons in strReplace with a colon
             *           because semicolons are used to delimit sql statements and that will mess up our queries.
             *           We will use regex to replace.
             *           */
            string strNoSemi = Regex.Replace(strReplace, "/*;*/", ":");
            return strNoSemi;
        }
        public bool isDateTime(string strDate)
        {
            /**
             * Name : isDateTime
             * Params : strDate - the string we are trying to parse to a dateTime
             * Returns : bool - true => success, string represents a dateTime, false => failure
             * Purpose : the purpose of this method is to try to parse strDate into a datetime, and return
             *           a boolean that represents if this avenue was a sucess or a failure.
             *           */
            try
            {
                DateTime.Parse(strDate);
                return true;
            }catch(Exception ex)
            {
                return false;
            }
        }
        public bool validateCategory(string strCategory) {
            /**
             * Name : validateCategory
             * Params : strCategory - the category we need to validate
             * Returns : bool - true => category is a valid one, false otherwise
             * Purpose : the purpose of this function is to make sure that the category used in the edit
             *           is a valid one, one that is in our database table.
             *           */
            //TODO// Implement this method once category table is created.
            return true;
        }
        public int editEntry([FromQuery] int intEntryID, [FromQuery]string strAmt, [FromQuery]string strCat, [FromQuery]string strSub, [FromQuery]string strDes, [FromQuery]string strCrt)
        {
            /**
             * Name : editEntry
             * Params : strAmt - the amount the user wishes to change to for the current entry
             *          strCat - the category the user wishes the entry to have
             *          strSub - the subCategory the user wishes the entry to have
             *          strDes - the description the user wishes the entry to have
             *          strCrt - the created time the user wishes the entry to have.
             * Returns : strReturn - a return string that first has a code where
             *                       0 = all good, entry was changed sucessfully
             *                       -1 = no session open
             *                       -2 = invalid amount
             *                       -3 = invalid created time
             *                       -4 = invalid category, not in database table
             *                       -5 = unable to open database connection
             *                       -6 = unable to execute statement
             * Purpose : the purpose of this function is to take in all the parameters from an entry on the report page on the front end, and
             *           try to update the corresponding row in the entry table. We will check to make sure that we arent trying to update with
             *           bogus values, and if we are we will use the above mentioned values to tell the frontend what occurred so that the user
             *           can be notified. If everything is all good we will make an instance of our database object and run an update command
             *           against our database. If the query is sucessful we return "0" otherwise we will use one of the aforementioned error codes.
             *           */
            //check to see if there is an open session for the user
            if (!checkSession())
            {
                ViewBag.Message = "No open session, please sign out and sign back in. ";
                return -1;
            }
            //check the amount given
            if (!isNumber(strAmt))
            {
                return -2;
            }
            //check the created time given
            if (!isDateTime(strCrt))
            {
                return -3;
            }
            if (!validateCategory(strCat))
            {
                return -4;
            }
            /*now lets make sure we replace any semicolons in the two unchecked fields : subcategory and description. Semicolons
             in the strings can mess up our sql queries. */

            if (!string.IsNullOrEmpty(strSub))
            {
                strSub = replaceSemiColon(strSub);
            }
            if (!string.IsNullOrEmpty(strDes))
            {
                strDes = replaceSemiColon(strDes);
            }
            

            //now create a database object instance
            clsDatabase objDB = new clsDatabase(config.GetValue<string>("DBConnString"));
            //sql will not like any commas in the numeric value, so lets make sure to replace any with nothing
            string strSQL = "UPDATE entrys SET amount = '" + strAmt.Replace(",", "") + "', category = '" + strCat + "', subcategory = '" + strSub + "', description= '" + strDes + "', createdtime = '" + strCrt + "' " +
                "WHERE entryID = '" + intEntryID + "';";
            NpgsqlConnection conn = objDB.getConnection();
            try{
                conn.Open();
                
            }
            catch
            {
                return -5;
            }
            //open a transaction for the sql changes we are about to make.
            using(NpgsqlTransaction tr = conn.BeginTransaction())
            {
                
                //create a command
                NpgsqlCommand cmd = new NpgsqlCommand(strSQL, conn);
                //now lets try to execute the command and commit the transaction
                try
                {
                    cmd.ExecuteNonQuery();
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    //before return code -6 lets close down all of the objects
                    tr.Rollback();
                    objDB.Dispose();
                    conn.Close();
                    return -6;
                }

            }
            //now we have sucessfully commited the change to our database, lets close the objects
            conn.Close();
            objDB.Dispose();
            return 0;
        }
        private string validHash(string strNormal)
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
            //first make sure strNormal is not null or empty

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
            strValid = strValid.Replace(' ', '.');

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
        public bool generateSessionCookie()
        {
            var x = Request.Cookies["user"];
            if (String.IsNullOrEmpty(x))
            {
                return false;
            }
            Response.Cookies.Append("validSession", validHash(x));
            return true;
        }
        public bool checkSession()
        {
            if (string.IsNullOrEmpty(Request.Cookies["user"]))
            {
                return false;
            }
            var x = validHash(Request.Cookies["user"]);
            if (String.IsNullOrEmpty(x))
            {
                return false;
            }
            if (Request.Cookies["validSession"].Equals(x))
            {
                return true;
            }
            GlobalVariables.UserID = -1;
            GlobalVariables.GlobalUsername = "";
            return false;
        }
    } 
}