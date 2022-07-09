using budgetApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;
using System.Text;

namespace budgetApp.Controllers
{
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
            /* check if the username is populated to ensure that user is signed in */
            if (String.IsNullOrEmpty(GlobalVariables.GlobalUsername))
            {
                /* no user signed in so we want to redirect user to signin page*/
                return RedirectToAction("SignIn");
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
            string sqlCommand = String.Format("INSERT INTO entrys (amount, category, subcategory, description, userID, createdtime) values({0}, {1}, {4}, {2}, {3}, {5});",
                "'" + model.amount + "'", "'" + model.category + "'", "'" + model.description + "'", "'" + GlobalVariables.UserID + "'", "'" + model.subCategory + "'", 
                "'" + DateTime.Now + "'");
            if (clsDatabase.ExecuteSQLNonQuery(sqlCommand, config.GetValue<string>("DBConnString")))
            {
                /* we were able to add the entry to our database */
                ViewBag.Message = "Successfully added entry.";
            }
            else
            {
                ViewBag.Message = "Something went wrong, entry not posted.";

            }
            /* after posting we just return the index view */
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
            StringBuilder sbSQL = new StringBuilder("SELECT * From entrys WHERE Userid = '" + GlobalVariables.UserID + "'");
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
                    break;
                case "All":
                    break;
                default:
                    sbSQL.Append(" AND CreatedTime < '" + DateTime.Now.ToString() + "-" + model.period + "'");
                    break;
            }
            sbSQL.Append(";");
            NpgsqlDataReader sdr = clsDatabase.ExecuteDataReader(sbSQL.ToString(), config.GetValue<string>("DBConnString"));
            /* lets start building our table, put in the headers first. Space them nicely as well */
            StringBuilder strTable = new StringBuilder("<table>");
            strTable.AppendLine("   <tr>");
            strTable.AppendLine("       <th>Amount</th>");
            strTable.AppendLine("       <th>Category</th>");
            strTable.AppendLine("       <th>SubCategory</th>");
            strTable.AppendLine("       <th>Description</th>");
            strTable.AppendLine("       <th>Date</th>");
            strTable.AppendLine("   </tr>");

            /* now we need to loop through all the results returned by our datareader, and add them as a row to the table */
            if (sdr == null)
            {
                ViewBag.Msg = "Something went wrong trying to read the data";
                model.strHTML = "";
                return View(model);
            }
            while (sdr.Read())
            {
                strTable.AppendLine("   <tr>");
                strTable.AppendLine("       <td>" + sdr["Amount"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Category"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Subcategory"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Description"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Createdtime"] + "</td>");
                strTable.AppendLine("   </tr>");
            }
            //dont forget to close the table
            strTable.AppendLine("</table>");
            model.strHTML = strTable.ToString();
            return View(model);
        }
        [HttpPost]
        public IActionResult SignIn(UserModel model)
        {
            string strSQL = "SELECT Id, username FROM users WHERE username = '" + model.username + "' AND passward = '" + model.password + "';";
            NpgsqlDataReader reader = clsDatabase.ExecuteDataReader(strSQL, config.GetValue<string>("DBConnString"));
            if(!(reader == null))
            {
                if (reader.Read())
                {
                    //correct signin credentials
                    try
                    {
                        GlobalVariables.GlobalUsername = model.username;
                        GlobalVariables.UserID = int.Parse(reader["Id"].ToString());
                    }
                    catch (Exception ex)
                    {
                        //something wrong with the data
                        ViewBag.Msg = "Account error.";
                        return View();
                    }
                    return RedirectToAction("Index");
                }
                //incorrect signin values
                ViewBag.Msg = "Username password incorrect.";
                return View();
            }
            else
            {
                //incorrect signin values
                ViewBag.Msg = "Username password incorrect.";
                return View();
            }
            /* TODO: check database that username passowrd combo is correct */
            //if correct
            
            //if wrong
            //return View(false);
        }
        public IActionResult SignOut()
        {
            GlobalVariables.GlobalUsername = null;
            GlobalVariables.UserID = -1;
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        public IActionResult signUp()
        {
            return View();
        }
        [HttpPost]
        public IActionResult signUp(UserModel model)
        {
            /* data validated in view, but need to make sure the username is unique */
            string strSQL = "SELECT * FROM users WHERE username = '" + model.username + "';";
            NpgsqlDataReader sdr = clsDatabase.ExecuteDataReader(strSQL, config.GetValue<string>("DBConnString"));
            if (sdr == null)
            {
                ViewBag.Msg = "There was an error with the database connection.";
                return View();
            }
            if (sdr.Read())
            {
                ViewBag.Msg = "Username already taken.";
                return View();
            }
            else
            {
                /* we can sign up the new user */
                strSQL = "INSERT INTO users (username, passward) values ('" + model.username + "', '" + model.password + "');";
                if(clsDatabase.ExecuteSQLNonQuery(strSQL, config.GetValue<string>("DBConnString"))){
                    /* user was signed up successfully */
                    ViewBag.Msg = "Account created succesfully please signin.";
                    return RedirectToAction("SignIn");
                }
                else
                {
                    ViewBag.Msg = "Something went wrong creating your account, please try again.";
                    return View();
                }
            }
        }
    }
}