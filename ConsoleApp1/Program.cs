using System.Text;
using WebSocketSharp;

var sharpws = new WebSocketSharp.WebSocket(
    // "wss://demo.piesocket.com/v3/channel_1?api_key=VCXCEuvhGcBDP7XhiJJUDvR1e1D3eiVjgZ9VRiaV&notify_self");
    "wss://fstream.binance.com/stream");

var txt =
        "{\"id\":1,\"method\":\"SUBSCRIBE\",\"params\":[\"dotusdt@depth@0ms\",\"dotusdt@trade\",\"compusdt@depth@0ms\",\"compusdt@trade\",\"sushiusdt@depth@0ms\",\"sushiusdt@trade\",\"renusdt@depth@0ms\",\"renusdt@trade\",\"sfpusdt@depth@0ms\",\"sfpusdt@trade\",\"bandusdt@depth@0ms\",\"bandusdt@trade\",\"aliceusdt@depth@0ms\",\"aliceusdt@trade\",\"dentusdt@depth@0ms\",\"dentusdt@trade\",\"icxusdt@depth@0ms\",\"icxusdt@trade\",\"oneusdt@depth@0ms\",\"oneusdt@trade\",\"stmxusdt@depth@0ms\",\"stmxusdt@trade\",\"kncusdt@depth@0ms\",\"kncusdt@trade\",\"xemusdt@depth@0ms\",\"xemusdt@trade\",\"ksmusdt@depth@0ms\",\"ksmusdt@trade\",\"rsrusdt@depth@0ms\",\"rsrusdt@trade\",\"avaxusdt@depth@0ms\",\"avaxusdt@trade\",\"hntusdt@depth@0ms\",\"hntusdt@trade\",\"mkrusdt@depth@0ms\",\"mkrusdt@trade\",\"srmusdt@depth@0ms\",\"srmusdt@trade\",\"chrusdt@depth@0ms\",\"chrusdt@trade\",\"zrxusdt@depth@0ms\",\"zrxusdt@trade\",\"snxusdt@depth@0ms\",\"snxusdt@trade\",\"balusdt@depth@0ms\",\"balusdt@trade\",\"hbarusdt@depth@0ms\",\"hbarusdt@trade\",\"yfiusdt@depth@0ms\",\"yfiusdt@trade\",\"manausdt@depth@0ms\",\"manausdt@trade\"]}";

sharpws.OnMessage += SharpwsOnOnMessage;
sharpws.Connect(txt);
// sharpws.Send(txt);

Console.ReadKey();


void SharpwsOnOnMessage(object? sender, MessageEventArgs e)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] sharps {e.Data}");
}    