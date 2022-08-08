// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

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
        alert("Please sign in. ")
        return false;
    }

    var url = "/Home/checkSession";
    
    $.get(url, function (data) {
        if (data == false) {
            window.location = "/Home/SignIn";
            alert("Not a valid session. Please sign in. ")
            return false;
        }
        return true;

    });
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

function generateSessionCookie() {
    
    var x;
    alert("in generate session cookie")
    var arr = document.cookie.split(';');
    for (var i = 0; i < arr.length; i++) {
        var temp = arr[i].split("=");
        if (temp[0] == "user") {
            x = temp[1];
            break;
        }
    }
    if (x == null || x == "") {
        alert("user was null")
        return false;
    }
    $.post("/Home/generateSessionCookie", function (data) {
        if (data == null || data == false) {
            alert("Could not start sesstion. ");
        } else {
            return true;
        }
    })
   
    return false;
}
function checkPwdSetting(){
    var strNew, strConfirm;
    strNew = document.getElementById("txtNewPwd").value;
    strConfirm = document.getElementById("txtConfirmPwd").value;
    if (!strNew == strConfirm || strNew == "" || strNew == null) {
        return false
    }
    return true;
}