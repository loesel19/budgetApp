using budgetApp.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace budgetApp.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
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
            string sqlCommand = String.Format("INSERT INTO Entrys columns(amount, category, description, userID) values({0}, {1}, {2}, {3})",
                "'" + model.amount + "'", "'" + model.category + "'", "'" + model.description + "'", "'" + GlobalVariables.UserID + "'");
            if (clsDatabase.ExecuteSQLNonQuery(sqlCommand))
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
        [HttpPost]
        public IActionResult SignIn(string username, string password)
        {
            /* TODO: check database that username passowrd combo is correct */
            //if correct
            GlobalVariables.GlobalUsername = username;
            return RedirectToAction("Index");
            //if wrong
            //return View(false);
        }
        public IActionResult SignOut()
        {
            GlobalVariables.GlobalUsername = null;
            return RedirectToAction("Index");
        }

    }
}