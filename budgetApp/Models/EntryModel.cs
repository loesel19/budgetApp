namespace budgetApp.Models
{
    public class EntryModel
    {
        public string category { get; set; }
        public string subCategory { get; set; }
        public decimal amount { get; set; }
        public string description { get; set; }
        public DateTime date { get; set; }
    }
}
