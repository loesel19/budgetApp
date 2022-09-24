namespace budgetApp
{
    public class GlobalVariables
    {
        private string GlobalUsername;
        private int UserID;
        public string getGlobalUsername()
        {
            return this.GlobalUsername;
        }
        public void setGlobalUsername(string GlobalUsername)
        {
            this.GlobalUsername = GlobalUsername;
        }
        public int getUserID()
        {
            return this.UserID;
        }
        public void setUserID(int UserID)
        {
            this.UserID = UserID;
        }
    }
    
}
