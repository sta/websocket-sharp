<!-- # websocket-sharp # -->
![Logo](websocket-sharp.png)

**websocket-sharp** is a C# implementation of the **WebSocket** protocol client and server.

## Build ##

**websocket-sharp** is built as a single assembly, **websocket-sharp.dll**.

websocket-sharp is developed with **[MonoDevelop]**. So the simple way to build is to open **websocket-sharp.sln** and run build for the websocket-sharp project with any of the build configurations (e.g. Debug) in the **MonoDevelop**.

## Install ##

### Self Build ###

You should add **websocket-sharp.dll** (e.g. /path/to/websocket-sharp/bin/Debug/websocket-sharp.dll) that you build it yourself to the library references of your project.

### NuGet Gallery ###

**websocket-sharp** has now been displayed on the **[NuGet Gallery]**, as still a prerelease version.

- **[NuGet Gallery: websocket-sharp]**

You can add websocket-sharp to your project using the **NuGet Package Manager**, like the follwing command in the **Package Manager Console**.

```
PM> Install-Package WebSocketSharp -Pre
```

### Unity Asset Store ###

**websocket-sharp** has now been displayed on the **Unity Asset Store**.

- **[websocket-sharp for Unity]**

That's priced at **US$15**. I think that your $15 makes this project more better and accelerated, Thank you!

## Supported .NET framework ##

**websocket-sharp** supports .NET **3.5** (includes compatible) or later.

## Usage ##

### WebSocket Client ###

```cs
using System;
using WebSocketSharp;

namespace Example
{
  public class Program
  {
    public static void Main (string [] args)
    {
      using (var ws = new WebSocket ("ws://dragonsnest.far/Laputa"))
      {
        ws.OnMessage += (sender, e) =>
        {
          Console.WriteLine ("Laputa says: " + e.Data);
        };

        ws.Connect ();
        ws.Send ("BALUS");
        Console.ReadKey (true);
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

Creating an instance of the `WebSocket` class with the specified WebSocket URL to connect.

```cs
using (var ws = new WebSocket ("ws://example.com"))
{
  ...
}
```

The `WebSocket` class inherits the `IDisposable` interface, so you can use the `using` statement.

#### Step 3 ####

Setting the `WebSocket` events.

##### WebSocket.OnOpen Event #####

A `WebSocket.OnOpen` event occurs when the WebSocket connection has been established.

```cs
ws.OnOpen += (sender, e) =>
{
  ...
};
```

`e` has passed as `EventArgs.Empty`, so you don't use `e`.

##### WebSocket.OnMessage Event #####

A `WebSocket.OnMessage` event occurs when the `WebSocket` receives a WebSocket data frame.

```cs
ws.OnMessage += (sender, e) =>
{
  ...
};
```

`e.Type` (`WebSocketSharp.MessageEventArgs.Type`, its type is `WebSocketSharp.Opcode`) indicates the type of a received data. So by checking it, you determine which item you should use.

If `e.Type` equals `Opcode.TEXT`, you use `e.Data` (`WebSocketSharp.MessageEventArgs.Data`, its type is `string`) that contains a received **Text** data.

If `e.Type` equals `Opcode.BINARY`, you use `e.RawData` (`WebSocketSharp.MessageEventArgs.RawData`, its type is `byte []`) that contains a received **Binary** data.

```cs
if (e.Type == Opcode.TEXT)
{
  // Do something with e.Data
  return;
}

if (e.Type == Opcode.BINARY)
{
  // Do something with e.RawData
  return;
}
```

##### WebSocket.OnError Event #####

A `WebSocket.OnError` event occurs when the `WebSocket` gets an error.

```cs
ws.OnError += (sender, e) =>
{
  ...
};
```
`e.Message` (`WebSocketSharp.ErrorEventArgs.Message`, its type is `string`) contains an error message, so you use it.

##### WebSocket.OnClose Event #####

A `WebSocket.OnClose` event occurs when the WebSocket connection has been closed.

```cs
ws.OnClose += (sender, e) =>
{
  ...
};
```

`e.Code` (`WebSocketSharp.CloseEventArgs.Code`, its type is `ushort`) contains a status code indicating the reason for closure and `e.Reason` (`WebSocketSharp.CloseEventArgs.Reason`, its type is `string`) contains the reason for closure, so you use them.

#### Step 4 ####

Connecting to the WebSocket server.

```cs
ws.Connect ();
```

#### Step 5 ####

Sending a data.

```cs
ws.Send (data);
```

The `Send` method is overloaded.

The types of `data` are `string`, `byte []` and `System.IO.FileInfo` class.

In addition, the `Send (stream, length)` method exists, too.

These methods don't wait for the send to be complete. This means that these methods behave asynchronously.

If you want to do something when the send is complete, you use any of some `Send (data, completed)` methods.

#### Step 6 ####

Closing the WebSocket connection.

```cs
ws.Close (code, reason);
```

If you want to close the WebSocket connection explicitly, you use the `Close` method.

The `Close` method is overloaded.

The types of `code` are `WebSocketSharp.CloseStatusCode` and `ushort`, and the type of `reason` is `string`.

In addition, the `Close ()` and `Close (code)` methods exist, too.

### WebSocket Server ###

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example
{
  public class Laputa : WebSocketService
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      var msg = e.Data == "BALUS"
              ? "I've been balused already..."
              : "I'm not available now.";

      Send (msg);
    }
  }

  public class Program
  {
    public static void Main (string [] args)
    {
      var wssv = new WebSocketServer ("ws://dragonsnest.far");
      wssv.AddWebSocketService<Laputa> ("/Laputa");
      wssv.Start ();
      Console.ReadKey (true);
      wssv.Stop ();
    }
  }
}
```

#### Step 1 ####

Required namespace.

```cs
using WebSocketSharp.Server;
```

The `WebSocketService` and `WebSocketServer` classes exist in the `WebSocketSharp.Server` namespace.

#### Step 2 ####

Creating the class that inherits the `WebSocketService` class.

For example, if you want to provide an echo service,

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

public class Echo : WebSocketService
{
  protected override void OnMessage (MessageEventArgs e)
  {
    Send (e.Data);
  }
}
```

And if you want to provide a chat service,

```cs
using System;
using WebSocketSharp;
using WebSocketSharp.Server;

public class Chat : WebSocketService
{
  private string _suffix;

  public Chat ()
    : this (String.Empty)
  {
  }

  public Chat (string suffix)
  {
    _suffix = suffix;
  }

  protected override void OnMessage (MessageEventArgs e)
  {
    Sessions.Broadcast (e.Data + _suffix);
  }
}
```

If you override the `OnMessage` method, it is bound to the server side `WebSocket.OnMessage` event.

In addition, if you override the `OnOpen`, `OnError` and `OnClose` methods, each of them is bound to each server side event of `WebSocket.OnOpen`, `WebSocket.OnError` and `WebSocket.OnClose`.

#### Step 3 ####

Creating an instance of the `WebSocketServer` class.

```cs
var wssv = new WebSocketServer (4649);
wssv.AddWebSocketService<Echo> ("/Echo");
wssv.AddWebSocketService<Chat> ("/Chat");
wssv.AddWebSocketService<Chat> ("/ChatWithNiceBoat", () => new Chat (" Nice boat."));
```

You can add any WebSocket service with the specified path to the service to your `WebSocketServer` by using the `WebSocketServer.AddWebSocketService<TWithNew>` or `WebSocketServer.AddWebSocketService<T>` method.

The type of `TWithNew` must inherit the `WebSocketService` class and must have a public parameterless constructor.

The type of `T` must inherit `WebSocketService` class.

So you can use the classes created in **Step 2**.

If you create an instance of the `WebSocketServer` class without the port number, the `WebSocketServer` set the port number to **80** automatically. So it is necessary to run with root permission.

    $ sudo mono example2.exe

#### Step 4 ####

Starting the server.

```cs
wssv.Start ();
```

#### Step 5 ####

Stopping the server.

```cs
wssv.Stop ();
```

### HTTP Server with the WebSocket ###

I modified the `System.Net.HttpListener`, `System.Net.HttpListenerContext` and some other classes of [Mono] to create the HTTP server that can upgrade the connection to the WebSocket connection when receives a WebSocket connection request.

You can add any WebSocket service with the specified path to the service to your `HttpServer` by using the `HttpServer.AddWebSocketService<TWithNew>` or `HttpServer.AddWebSocketService<T>` method.

```cs
var httpsv = new HttpServer (4649);
httpsv.AddWebSocketService<Echo> ("/Echo");
httpsv.AddWebSocketService<Chat> ("/Chat");
httpsv.AddWebSocketService<Chat> ("/ChatWithNiceBoat", () => new Chat (" Nice boat."));
```

For more information, could you see **[Example3]**?

### WebSocket Extensions ###

#### Per-message Compression ####

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

### Secure Connection ###

As a **WebSocket Client**, creating an instance of the `WebSocket` class with the WebSocket URL with **wss** scheme.

```cs
using (var ws = new WebSocket ("wss://example.com"))
{
  ...
}
```

If you want to set the custom validation for the server certificate, you use the `WebSocket.ServerCertificateValidationCallback` property.

```cs
ws.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
{
  // Do something to validate the server certificate.
  return true; // The server certificate is valid.
};
```

If you set this property to nothing, the validation does nothing with the server certificate, always returns valid.

As a **WebSocket Server**, creating an instance of the `WebSocketServer` or `HttpServer` class with some settings for the secure connection.

```cs
var wssv = new WebSocketServer (4649, true);
wssv.Certificate = new X509Certificate2 ("/path/to/cert.pfx", "password for cert.pfx");
```

### Logging ###

The `WebSocket` class includes own logging functions.

The `WebSocket.Log` property provides the logging functions.

If you want to change the current logging level (the default is `LogLevel.ERROR`), you use the `WebSocket.Log.Level` property.

```cs
ws.Log.Level = LogLevel.DEBUG;
```

The above means that the logging outputs with a less than `LogLevel.DEBUG` are not outputted.

And if you want to output a log, you use any of some output methods. The following outputs a log with `LogLevel.DEBUG`.

```cs
ws.Log.Debug ("This is a debug message.");
```

The `WebSocketServer` and `HttpServer` classes include the same logging functions.

## Examples ##

Examples using **websocket-sharp**.

### Example ###

[Example] connects to the [Echo server] using the WebSocket.

### Example1 ###

[Example1] connects to the [Audio Data delivery server] using the WebSocket ([Example1] is only implemented the chat feature, still unfinished).

And [Example1] uses [Json.NET].

### Example2 ###

[Example2] starts a WebSocket server.

### Example3 ###

[Example3] starts an HTTP server that can upgrade the connection to the WebSocket connection.

Could you access to [http://localhost:4649](http://localhost:4649) to do **WebSocket Echo Test** with your web browser after [Example3] running?

## Supported WebSocket Specifications ##

**websocket-sharp** supports **[RFC 6455]**.

- **[branch: hybi-00]** supports older draft-ietf-hybi-thewebsocketprotocol-00 ( **[hybi-00]** ).
- **[branch: draft75]** supports even more old draft-hixie-thewebsocketprotocol-75 ( **[hixie-75]** ).

**websocket-sharp** is based on the following WebSocket references.

- **[The WebSocket Protocol]**
- **[The WebSocket API]**
- **[Compression Extensions for WebSocket][compression]**

Thanks for translating to japanese.

- **[The WebSocket Protocol 日本語訳]**
- **[The WebSocket API 日本語訳]**

## License ##

**websocket-sharp** is provided under **[The MIT License](LICENSE.txt)**.


[Audio Data delivery server]: http://agektmr.node-ninja.com:3000
[Echo server]: http://www.websocket.org/echo.html
[Example]: https://github.com/sta/websocket-sharp/tree/master/Example
[Example1]: https://github.com/sta/websocket-sharp/tree/master/Example1
[Example2]: https://github.com/sta/websocket-sharp/tree/master/Example2
[Example3]: https://github.com/sta/websocket-sharp/tree/master/Example3
[Json.NET]: http://james.newtonking.com/projects/json-net.aspx
[Mono]: http://www.mono-project.com
[MonoDevelop]: http://monodevelop.com
[NuGet Gallery]: http://www.nuget.org
[NuGet Gallery: websocket-sharp]: http://www.nuget.org/packages/WebSocketSharp
[RFC 6455]: http://tools.ietf.org/html/rfc6455
[The WebSocket API]: http://www.w3.org/TR/websockets
[The WebSocket API 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/WebSocket-ja.html
[The WebSocket Protocol]: http://tools.ietf.org/html/rfc6455
[The WebSocket Protocol 日本語訳]: http://www.hcn.zaq.ne.jp/___/WEB/RFC6455-ja.html
[branch: draft75]: https://github.com/sta/websocket-sharp/tree/draft75
[branch: hybi-00]: https://github.com/sta/websocket-sharp/tree/hybi-00
[compression]: http://tools.ietf.org/html/draft-ietf-hybi-permessage-compression-09
[extension parameters]: http://tools.ietf.org/html/draft-ietf-hybi-permessage-compression-09#section-8.1
[hixie-75]: http://tools.ietf.org/html/draft-hixie-thewebsocketprotocol-75
[hybi-00]: http://tools.ietf.org/html/draft-ietf-hybi-thewebsocketprotocol-00
[websocket-sharp for Unity]: http://u3d.as/content/sta-blockhead/websocket-sharp-for-unity
