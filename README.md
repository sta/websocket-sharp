# websocket-sharp #

**websocket-sharp** is a C# implementation of a WebSocket protocol client.

## Usage ##

### Step 1 ###

Required namespaces.

    using WebSocketSharp;
    using WebSocketSharp.Frame;

In `WebSocketSharp` namespace `WebSocket` class exists, in `WebSocketSharp.Frame` namespace WebSocket data frame resources (e.g. `WsFrame` class) exist.

### Step 2 ###

Creating instance of `WebSocket` class.

    using (WebSocket ws = new WebSocket("ws://example.com"))
    {
      ...
    }

So `WebSocket` class inherits `IDisposable` interface, you can use `using` statement.

### Step 3 ###

Setting `WebSocket` event handlers.

#### WebSocket.OnOpen event ####

`WebSocket.OnOpen` event is emitted immediately after WebSocket connection has been established.

    ws.OnOpen += (sender, e) =>
    {
      ...
    };

So `e` has come across as `EventArgs.Empty`, there is no operation on `e`.

#### WebSocket.OnMessage event ####

`WebSocket.OnMessage` event is emitted each time WebSocket data frame is received.

    ws.OnMessage += (sender, e) =>
    {
      ...
    };

So **type** of received WebSocket data frame is stored in `e.Type` (`WebSocketSharp.MessageEventArgs.Type`, its type is `WebSocketSharp.Frame.Opcode`), you check it out and you determine which item you should operate.

    switch (e.Type)
    {
      case Opcode.TEXT:
        ...
        break;
      case Opcode.BINARY:
        ...
        break;
      default:
        break;
    }

If `e.Type` is `Opcode.TEXT`, you operate `e.Data` (`WebSocketSharp.MessageEventArgs.Data`, its type is `string`).

If `e.Type` is `Opcode.BINARY`, you operate `e.RawData` (`WebSocketSharp.MessageEventArgs.RawData`, its type is `byte[]`).

#### WebSocket.OnError event ####

`WebSocket.OnError` event is emitted when some error is occurred.

    ws.OnError += (sender, e) =>
    {
      ...
    };

So error message is stored in `e.Data` (`WebSocketSharp.MessageEventArgs.Data`, its type is `string`), you operate it.

#### WebSocket.OnClose event ####

`WebSocket.OnClose` event is emitted when WebSocket connection is closed.

    ws.OnClose += (sender, e) =>
    {
      ...
    };

So close status code is stored in `e.Code` (`WebSocketSharp.CloseEventArgs.Code`, its type is `WebSocketSharp.Frame.CloseStatusCode`) and reason of close is stored in `e.Reason` (`WebSocketSharp.CloseEventArgs.Reason`, its type is `string`), you operate them.

### Step 4 ###

Connecting to server using WebSocket.

    ws.Connect();

### Step 5 ###

Sending data.

    ws.Send(data);

`WebSocket.Send` method is overloaded.

`data` types are `string`, `byte[]` and `FileInfo` class.

### Step 6 ###

Closing WebSocket connection.

    ws.Close(code, reason);

If you want to close WebSocket connection explicitly, you can use `Close` method.

Type of `code` is `WebSocketSharp.Frame.CloseStatusCode`, type of `reason` is `string`.

`WebSocket.Close` method is overloaded (In addition `Close()` and `Close(code)` exist).

## Examples ##

Examples of using **websocket-sharp**.

### Example ###

[Example] connects to the [Echo server] using the WebSocket.

### Example1 ###

[Example1] connects to the [Audio Data delivery server] using the WebSocket ([Example1] is only implemented a chat feature, still unfinished).

[Example1] uses [Json.NET].

## Supported WebSocket Protocol ##

**websocket-sharp** supports **[RFC 6455]**.

- @**[branch: hybi-00]** supports older draft-ietf-hybi-thewebsocketprotocol-00 (**[hybi-00]**).
- @**[branch: draft75]** supports even more old draft-hixie-thewebsocketprotocol-75 (**[hixie-75]**).

## Reference ##

- **[The WebSocket Protocol]**
- **[The WebSocket API]**

Thx for translating to japanese.

- **[The WebSocket Protocol 日本語訳]**
- **[The WebSocket API 日本語訳]**

## License ##

Copyright &copy; 2010 - 2012 sta.blockhead

Licensed under the **[MIT License]**.


[Audio Data delivery server]: http://agektmr.node-ninja.com:3000/
[branch: draft75]: https://github.com/sta/websocket-sharp/tree/draft75
[branch: hybi-00]: https://github.com/sta/websocket-sharp/tree/hybi-00
[Echo server]: http://www.websocket.org/echo.html
[Example]: https://github.com/sta/websocket-sharp/tree/master/Example
[Example1]: https://github.com/sta/websocket-sharp/tree/master/Example1
[hixie-75]: http://tools.ietf.org/html/draft-hixie-thewebsocketprotocol-75
[hybi-00]: http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00
[Json.NET]: http://james.newtonking.com/projects/json-net.aspx
[MIT License]: http://www.opensource.org/licenses/mit-license.php
[RFC 6455]: http://tools.ietf.org/html/rfc6455
[The WebSocket API]: http://dev.w3.org/html5/websockets
[The WebSocket API 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/WebSocket-ja.html
[The WebSocket Protocol]: http://tools.ietf.org/html/rfc6455
[The WebSocket Protocol 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/RFC6455-ja.html
