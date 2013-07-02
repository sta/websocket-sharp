<!-- # websocket-sharp # -->
![Logo](websocket-sharp.png)

**websocket-sharp** is a C# implementation of the **WebSocket** protocol client & server.

## Usage ##

### WebSocket Client ###

```cs
using System;
using WebSocketSharp;

namespace Example {

  public class Program {

    public static void Main(string[] args)
    {
      using (var ws = new WebSocket("ws://dragonsnest.far/Laputa"))
      {
        ws.OnMessage += (sender, e) =>
        {
          Console.WriteLine("Laputa says: {0}", e.Data);
        };

        ws.Connect();
        ws.Send("BALUS");
        Console.ReadKey(true);
      }
    }
  }
}
```

#### Step 1 ####

Required namespace.

```cs
using WebSocketSharp;
```

The `WebSocket` class exists in the `WebSocketSharp` namespace.

#### Step 2 ####

Creating a instance of the `WebSocket` class with the specified WebSocket URL.

```cs
using (var ws = new WebSocket("ws://example.com"))
{
  ...
}
```

The `WebSocket` class inherits the `IDisposable` interface, so you can use the `using` statement.

#### Step 3 ####

Setting the `WebSocket` events.

##### WebSocket.OnOpen event #####

The `WebSocket.OnOpen` event occurs when the WebSocket connection has been established.

```cs
ws.OnOpen += (sender, e) =>
{
  ...
};
```

The `e` has come across as the `EventArgs.Empty`, so there is no operation on the `e`.

##### WebSocket.OnMessage event #####

The `WebSocket.OnMessage` event occurs when the `WebSocket` receives a data frame.

```cs
ws.OnMessage += (sender, e) =>
{
  ...
};
```

The `e.Type` (`WebSocketSharp.MessageEventArgs.Type`, its type is `WebSocketSharp.Opcode`) indicates the **Frame type** of the WebSocket frame, so you check it out and you determine which item you should operate.

```cs
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
```

If the `e.Type` is `Opcode.TEXT`, you operate the `e.Data` (`WebSocketSharp.MessageEventArgs.Data`, its type is `string`).

If the `e.Type` is `Opcode.BINARY`, you operate the `e.RawData` (`WebSocketSharp.MessageEventArgs.RawData`, its type is `byte[]`).

##### WebSocket.OnError event #####

The `WebSocket.OnError` event occurs when the `WebSocket` gets an error.

```cs
ws.OnError += (sender, e) =>
{
  ...
};
```
The `e.Message` (`WebSocketSharp.ErrorEventArgs.Message`, its type is `string`) contains the error message, so you operate it.

##### WebSocket.OnClose event #####

The `WebSocket.OnClose` event occurs when the `WebSocket` connection has been closed.

```cs
ws.OnClose += (sender, e) =>
{
  ...
};
```

The `e.Code` (`WebSocketSharp.CloseEventArgs.Code`, its type is `ushort`) contains a status code indicating a reason for closure and the `e.Reason` (`WebSocketSharp.CloseEventArgs.Reason`, its type is `string`) contains a reason for closure, so you operate them.

#### Step 4 ####

Connecting to the WebSocket server.

```cs
ws.Connect();
```

#### Step 5 ####

Sending a data.

```cs
ws.Send(data);
```

The `Send` method is overloaded.

The types of `data` are `string`, `byte[]` and `FileInfo` class.

#### Step 6 ####

Closing the WebSocket connection.

```cs
ws.Close(code, reason);
```

If you want to close the WebSocket connection explicitly, you can use the `Close` method.

The `Close` method is overloaded.

The types of `code` are `WebSocketSharp.CloseStatusCode` and `ushort`, the type of `reason` is `string`.

In addition, the `Close()` and `Close(code)` methods exist.

### WebSocket Server ###

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example {

  public class Laputa : WebSocketService
  {
    protected override void OnMessage(MessageEventArgs e)
    {
      var msg = e.Data.Equals("balus", StringComparison.InvariantCultureIgnoreCase)
              ? "I've been balused already..."
              : "I'm not available now.";
      Send(msg);
    }
  }

  public class Program {

    public static void Main(string[] args)
    {
      var wssv = new WebSocketServiceHost<Laputa>("ws://dragonsnest.far/Laputa");
      wssv.Start();
      Console.ReadKey(true);
      wssv.Stop();
    }
  }
}
```

#### Step 1 ####

Required namespace.

```cs
using WebSocketSharp.Server;
```

The `WebSocketServer`, `WebSocketServiceHost<T>` and `WebSocketService` classes exist in the `WebSocketSharp.Server` namespace.

#### Step 2 ####

Creating a class that inherits the `WebSocketService` class.

For example, if you want to provide the echo service,

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

public class Echo : WebSocketService
{
  protected override void OnMessage(MessageEventArgs e)
  {
    Send(e.Data);
  }
}
```

Or if you want to provide the chat service,

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

public class Chat : WebSocketService
{
  protected override void OnMessage(MessageEventArgs e)
  {
    Broadcast(e.Data);
  }
}
```

If you override the `OnMessage` method, it is bound to the server side `WebSocket.OnMessage` event.

In addition, if you override the `OnOpen`, `OnError` and `OnClose` methods, each of them is bound to the `WebSocket.OnOpen`, `WebSocket.OnError` and `WebSocket.OnClose` events.

#### Step 3 ####

Creating a instance of the `WebSocketServiceHost<T>` class if you want the single WebSocket service server.

```cs
var wssv = new WebSocketServiceHost<Echo>("ws://example.com:4649");
```

Creating a instance of the `WebSocketServer` class if you want the multi WebSocket service server.

```cs
var wssv = new WebSocketServer(4649);
wssv.AddWebSocketService<Echo>("/Echo");
wssv.AddWebSocketService<Chat>("/Chat");
```

You can add to your `WebSocketServer` any WebSocket service and a matching path to that service by using the `WebSocketServer.AddWebSocketService<T>` method.

The type of `T` inherits `WebSocketService` class, so you can use a class that was created in **Step 2**.

If you create a instance of the `WebSocketServer` class without port number, `WebSocketServer` set **80** to port number automatically.  
So it is necessary to run with root permission.

    $ sudo mono example2.exe

#### Step 4 ####

Setting the event.

##### WebSocketServiceHost&lt;T>.OnError event #####

The `WebSocketServiceHost<T>.OnError` event occurs when the `WebSocketServiceHost<T>` gets an error.

```cs
wssv.OnError += (sender, e) =>
{
  ...
};
```

The `e.Message` (`WebSocketSharp.ErrorEventArgs.Message`, its type is `string`) contains the error message, so you operate it.

##### WebSocketServer.OnError event #####

Same as the `WebSocketServiceHost<T>.OnError` event.

#### Step 5 ####

Starting the server.

```cs
wssv.Start();
```

#### Step 6 ####

Stopping the server.

```cs
wssv.Stop();
```

### HTTP Server with the WebSocket ###

I modified the `System.Net.HttpListener`, `System.Net.HttpListenerContext` and some other classes of [Mono] to create the HTTP server that can upgrade the connection to the WebSocket connection when receives a WebSocket request.

You can add to your `HttpServer` any WebSocket service and a matching path to that service by using the `HttpServer.AddWebSocketService<T>` method.

```cs
var httpsv = new HttpServer(4649);
httpsv.AddWebSocketService<Echo>("/");
```

For more information, could you see **[Example3]**?

## Examples ##

Examples of using **websocket-sharp**.

### Example ###

[Example] connects to the [Echo server] using the WebSocket.

### Example1 ###

[Example1] connects to the [Audio Data delivery server] using the WebSocket ([Example1] is only implemented a chat feature, still unfinished).

[Example1] uses [Json.NET].

### Example2 ###

[Example2] starts the WebSocket server.

### Example3 ###

[Example3] starts the HTTP server that can upgrade the connection to the WebSocket connection.

Please access [http://localhost:4649](http://localhost:4649) to do WebSocket Echo Test with your web browser after [Example3] running.

## Required Environment ##

C# **3.0**, .NET **3.5** compatible or later.

## websocket-sharp for Unity ##

**websocket-sharp** has now been displayed on the **Unity Asset Store**!

- **[websocket-sharp for Unity]**

## Supported WebSocket Specifications ##

**websocket-sharp** supports **[RFC 6455]**.

- **[branch: hybi-00]** supports older draft-ietf-hybi-thewebsocketprotocol-00 ( **[hybi-00]** ).
- **[branch: draft75]** supports even more old draft-hixie-thewebsocketprotocol-75 ( **[hixie-75]** ).

## Supported WebSocket Extensions ##

### Per-message Compression ###

**websocket-sharp** supports **[Per-message Compression][compression]** extension. (But, does not support with [extension parameters].)

If you want to enable this extension as a WebSocket client, you should do like the following.

```cs
ws.Compression = CompressionMethod.DEFLATE;
```

And then your client sends the following header in the opening handshake to a WebSocket server.

```
Sec-WebSocket-Extensions: permessage-deflate
```

If the server supports this extension, responds the same header. And when your client receives the header, enables this extension.

## WebSocket References ##

- **[The WebSocket Protocol]**
- **[The WebSocket API]**
- **[Compression Extensions for WebSocket][compression]**

Thanks for translating to japanese.

- **[The WebSocket Protocol 日本語訳]**
- **[The WebSocket API 日本語訳]**

## License ##

Copyright &copy; 2010 - 2013 sta.blockhead

Licensed under the **[MIT License]**.


[Audio Data delivery server]: http://agektmr.node-ninja.com:3000/
[branch: draft75]: https://github.com/sta/websocket-sharp/tree/draft75
[branch: hybi-00]: https://github.com/sta/websocket-sharp/tree/hybi-00
[compression]: http://tools.ietf.org/html/draft-ietf-hybi-permessage-compression-09
[Echo server]: http://www.websocket.org/echo.html
[Example]: https://github.com/sta/websocket-sharp/tree/master/Example
[Example1]: https://github.com/sta/websocket-sharp/tree/master/Example1
[Example2]: https://github.com/sta/websocket-sharp/tree/master/Example2
[Example3]: https://github.com/sta/websocket-sharp/tree/master/Example3
[extension parameters]: http://tools.ietf.org/html/draft-ietf-hybi-permessage-compression-09#section-8.1
[hixie-75]: http://tools.ietf.org/html/draft-hixie-thewebsocketprotocol-75
[hybi-00]: http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00
[Json.NET]: http://james.newtonking.com/projects/json-net.aspx
[MIT License]: http://www.opensource.org/licenses/mit-license.php
[Mono]: http://www.mono-project.com/
[RFC 6455]: http://tools.ietf.org/html/rfc6455
[The WebSocket API]: http://www.w3.org/TR/websockets/
[The WebSocket API 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/WebSocket-ja.html
[The WebSocket Protocol]: http://tools.ietf.org/html/rfc6455
[The WebSocket Protocol 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/RFC6455-ja.html
[websocket-sharp for Unity]: http://u3d.as/content/sta-blockhead/websocket-sharp-for-unity
