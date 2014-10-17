/*
 * echotest.js
 *
 * Derived from Echo Test of WebSocket.org (http://www.websocket.org/echo.html).
 *
 * Copyright (c) 2012 Kaazing Corporation.
 */

var url = "ws://localhost:4649/Echo";
//var url = "wss://localhost:4649/Echo";
var output;

function init () {
  output = document.getElementById ("output");
  doWebSocket ();
}

function doWebSocket () {
  websocket = new WebSocket (url);

  websocket.onopen = function (evt) {
    onOpen (evt)
  };

  websocket.onclose = function (evt) {
    onClose (evt)
  };

  websocket.onmessage = function (evt) {
    onMessage (evt)
  };

  websocket.onerror = function (evt) {
    onError (evt)
  };
}

function onOpen (evt) {
  writeToScreen ("CONNECTED");
  send ("WebSocket rocks");
}

function onClose (evt) {
  writeToScreen ("DISCONNECTED");
}

function onMessage (evt) {
  writeToScreen ('<span style="color: blue;">RESPONSE: ' + evt.data + '</span>');
  websocket.close ();
}

function onError (evt) {
  writeToScreen('<span style="color: red;">ERROR: ' + evt.data + '</span>');
}

function send (message) {
  writeToScreen ("SENT: " + message);
  websocket.send (message);
}

function writeToScreen (message) {
  var pre = document.createElement ("p");
  pre.style.wordWrap = "break-word";
  pre.innerHTML = message;
  output.appendChild (pre);
}

window.addEventListener ("load", init, false);