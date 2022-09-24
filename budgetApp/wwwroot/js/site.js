// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


function checkUserNotSignedIn() {
    /**
     * This is a copy pase function from CheckUserSession, except we flip all the returns,
     * and do not alert or redirect when a user is not signed in. The function ensures that a user does not go to the signUp page while signed in.
     * */
    var x;
    var arr = document.cookie.split(';');
    for (var i = 0; i < arr.length; i++) {
        var temp = arr[i].split("=");
        if (temp[0] == "user") {
            x = temp[1];
            break;
        }
    }
    if (x == null || x == "") {
        
        return true;
    }

  
}
function checkUserSession() {
    var x;
    var arr = document.cookie.split(';');
    for (var i = 0; i < arr.length; i++) {
        var temp = arr[i].split("=");
        if (temp[0] == "user") {
            x = temp[1];
            break;
        }
    }
    if (x == null || x == "") {


        window.location = "/Home/SignIn";
    }

}
function getUsername() {
    var x;
    var arr = document.cookie.split(';');
    for (var i = 0; i < arr.length; i++) {
        var temp = arr[i].split("=");
        if (temp[0] == "user") {
            x = temp[1];
            break;
        }
    }
    if (x == null || x == "") {
        return "";
    }
    return x;
}
function generateUserCookie(username, boo) {
    if (boo) {
        const d = new Date();
        d.setDate(d.getDate() + 30);
        document.cookie = "user=" + username + ";expires=" + d.toUTCString() + ";path=/";
        alert("You will stay signed in for the next 30 days.")
    } else {
        document.cookie = "user=" + username + ";path=/";
    }
}

function checkPwdSetting(){
    var strNew, strConfirm;
    strNew = document.getElementById("txtNewPwd").value;
    strConfirm = document.getElementById("txtConfirmPwd").value;
    if (strNew == "" || strNew == null) {
        alert("Please enter a new password. ")
        return false
    }
    if (strNew != strConfirm) {
        alert("New passwords must match. ")
        return false
    }
    return true;
}