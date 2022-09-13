# budgetApp
# Author :  loesel19
# Platform : C# ASP.NET MVC 5 .NET6, HEROKU NPGSQL Database
# Purpose : this application was developed so that I could sharpen both my web developement skills and get to know ASP developement. This is a simple application that
# allows for budget tracking, and report creation. Below the program flow is laid out.
# ACCOUNTS - first a user will need to create an account. Passwords are encrypted before being sent to the database. Users must sign in before accessing any other page.
# HOME/INDEX - The home page is where users can create entrys for expenses (need, want, savings) or income. Users can make as many entries as they would like.
# REPORT - The report page allows the user to generate reports for whichever categories and time period they would like. From this page a user may click on a table row
#          which will prompt an edit(yellow) and delete(red) button to appear. When the red button is pressed the user is asked if they would like to delete the entry
#          and the user either confirms or cancels this action. If deleted the page is refreshed. When the yellow button is pressed all the values for the given row will
#          be placed in textboxes or a select(for limited field category), and the user may make any changes they would like. After changes are made the user will press
#          the green button which used to be yellow, and must either confirm or cancel the update action. If confirmed the new data is sent to the backend, and an 
#          attempt to update the database is made.
# SETTINGS - When settings is clicked on (it is next to the sign out button) the user will be taken to a page where they can change their password.
# SIGN OUT - Signing out an account is handled by a sign out button, which will delete all browser stored credentials and redirect the user to the sign in page.
