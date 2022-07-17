﻿using budgetApp.Models;
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
                try
                {
                    string username = Request.Cookies["username"].ToString();
                    /* user was previously signed in and wanted to be remembered, so we can sign them back in */
                    GlobalVariables.GlobalUsername = username;
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
                    sbSQL.Append(" AND CreatedTime > '" + DateTime.Now.ToString() + " - " + model.period + "'");
                    break;
            }
            sbSQL.Append(";");
            NpgsqlDataReader sdr = clsDatabase.ExecuteDataReader(sbSQL.ToString(), config.GetValue<string>("DBConnString"));
            /* lets start building our table, put in the headers first. Space them nicely as well */
            StringBuilder strTable = new StringBuilder("<table class=\"table\">");
            strTable.AppendLine("   <tr>");
            strTable.AppendLine("       <th class=\"th-md\">Amount</th>");
            strTable.AppendLine("       <th class=\"th-md\">Category</th>");
            strTable.AppendLine("       <th class=\"th-md\">SubCategory</th>");
            strTable.AppendLine("       <th class=\"th-md\">Description</th>");
            strTable.AppendLine("       <th class=\"th-md\">Date</th>");
            strTable.AppendLine("   </tr>");

            /* now we need to loop through all the results returned by our datareader, and add them as a row to the table */
            if (sdr == null)
            {
                ViewBag.Msg = "Something went wrong trying to read the data";
                model.strHTMLTable = "";
                return View(model);
            }
            double spent = 0;
            double income = 0;
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
                strTable.AppendLine("       <td>$" + sdr["Amount"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Category"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Subcategory"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Description"] + "</td>");
                strTable.AppendLine("       <td>" + sdr["Createdtime"] + "</td>");
                strTable.AppendLine("   </tr>");
            }
            //dont forget to close the table
            strTable.AppendLine("</table>");
            //now we need to set the totals as well
            model.strHTML = "<table><tr><th class=\"th-md\">Total Spent</th>" +
                "<td>&nbsp$" + spent + "</td>" + "<th class=\"th-md\">Total Net</th>" +
                "<td>&nbsp$" + (income - spent) + "</td></tr></table>";
            model.strHTMLTable = strTable.ToString();
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
                ViewBag.Msg = "Username/password incorrect.";
                return View();
            }
            else
            {
                //incorrect signin values
                ViewBag.Msg = "Username/password incorrect.";
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
            /* we will also want to 'delete' the username cookie */
            try
            {
                Response.Cookies.Delete("username");
            } catch (Exception ex) { }

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