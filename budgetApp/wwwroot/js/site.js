// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function checkUserSession() {
    var x;
    $.get("/Home/readUserCookie", function (data) {
        x = data;
    });
    if (x == null) {
        return false;
    }
    var y;
    $.get("/Home/readSessionCookie",  function (data) {
        y = data;
    });
    var url = "/Home/validHash?strNormal=" + x
    $.get(url, null, function (data) {
        if (y == data) {
            return true;
        }
        return false;
    });
    return false;
    
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
    alert("in session cookie method")
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
        alert("user was null")
        return false;
    }

   var 
   
    return false;
}