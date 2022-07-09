using Microsoft.AspNetCore.Mvc.Rendering;

namespace budgetApp.Models
{
    public class ReportModel
    {
        public string strHTML { get; set; }
        public string category { get; set; }
        public string period { get; set; }
        public List<SelectListItem> categories { get; set; }
        public List<SelectListItem> periods { get; set; }
        public ReportModel()
        {
            strHTML = "";
            categories = new List<SelectListItem>();
            categories.Add(new SelectListItem
            {
                Text = "Need",
                Value = "Need"
            });
            categories.Add(new SelectListItem
            {
                Text = "Want",
                Value = "Want"
            });
            categories.Add(new SelectListItem
            {
                Text = "Saving/Invest",
                Value = "Saving/Invest"
            });
            categories.Add(new SelectListItem
            {
                Text = "Income",
                Value = "Income"
            });
            categories.Add(new SelectListItem
            {
                Text = "All Expenses",
                Value = "All",
                Selected = true
            });
            /* for periods we will make the values of the select items the amount of days */
            periods = new List<SelectListItem>();
            periods.Add(new SelectListItem
            {
                Text = "1 week",
                Value = "7",
                Selected = true
            });
            periods.Add(new SelectListItem
            {
                Text = "1 month",
                Value = "30"
            });
            periods.Add(new SelectListItem
            {
                Text = "3 months",
                Value = "92"
            });
            periods.Add(new SelectListItem
            {
                Text = "1 year",
                Value = "365"
            });
            // YTD and all will be a little weird to do, so lets just worry about them in the controller :)
            periods.Add(new SelectListItem
            {
                Text = "YTD",
                Value = "YTD"
            });
            periods.Add(new SelectListItem
            {
                Text = "All",
                Value = "All"
            });
        }
    }
}
