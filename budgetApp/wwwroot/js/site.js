// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function checkUserSession(){
    if (document.cookie["user"] == null) {
        return false;
    }
    var url = "/Home/validHash?strNormal=" + document.cookie["user"]
    $.get(url, null, new function (data) {
        if (document.cookie["validSession"] == data) {
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
    if (document.cookie["user"] == null) {
        return false;
    }
    var url = "/Home/validHash?strNormal=" + document.cookie["user"]
    $.get(url, null, new function (data) {
        document.cookie = "validSession=" + data + ";path=/";
        return true;
    });
    return false;
}