$(document).ready(function () {
    console.log("go");

    const connection = new WebSocket("ws://127.0.0.1:55555/Laputa");
    connection.onopen = () => {
        console.log("opened");
        connection.send('BALUS');
    }

    connection.onerror = error => {
        console.log("WebSocket error");
        console.log(error);
    }

    connection.onclose = e => {
        console.log("connection closes");
        console.log(e);
    }

    connection.onmessage = (e) => {
        console.log(e.data);
    }
});