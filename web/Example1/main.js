$(document).ready(function () {
    console.log("go");

    const connection = new WebSocket("ws://127.0.0.1:55555/Laputa");
    connection.onopen = () => {
        console.log("opened");
    }

    connection.onerror = error => {
        console.log("WebSocket error");
        console.log(error);
    }
});