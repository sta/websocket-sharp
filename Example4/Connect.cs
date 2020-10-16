using System;
using System.Threading.Tasks;
using WebSocketSharp;

namespace Example4
{
    public class Connect : IConnect
    {
        private WebSocket ws;
        private string string_data = null;
        private byte[] bytes_data = null;

        public Connect(){}

        IConnect IConnect.Connection(string Url, string Port)
        {
            ws = new WebSocket("ws://" + Url + ":" + Port);
            return this;
        }

        private void OnMessage(object s, MessageEventArgs e)
        {
            if (e.IsText)
                string_data = e.Data;
            else if (e.IsBinary)
                bytes_data = e.RawData;
        }

        async Task<string> IConnect.SendMessage(string message)
        {
            ws.OnMessage += OnMessage;
            ws.OnOpen += (s, e) => ws.Send(message);
            ws.Connect();
            _ = await ((IConnect)this).WaitAsync();
            return string_data;
        }

        async Task<string> IConnect.SendMessage(byte[] message)
        {
            ws.OnMessage += OnMessage;
            ws.OnOpen += (s, e) => ws.Send(message);
            ws.Connect();
            _ = await ((IConnect)this).WaitAsync();
            return string_data;
        }

        async Task<byte[]> IConnect.GetBytes(string message)
        {
            ws.OnMessage += OnMessage;
            ws.OnOpen += (s, e) => ws.Send(message);
            ws.Connect();
            _ = await ((IConnect)this).WaitAsync();
            return bytes_data;
        }

        async Task<byte[]> IConnect.GetBytes(byte[] message)
        {
            ws.OnMessage += OnMessage;
            ws.OnOpen += (s, e) => ws.Send(message);
            ws.Connect();
            _ = await ((IConnect)this).WaitAsync();
            return bytes_data;
        }

        async Task<bool> IConnect.WaitAsync()
        {
            (string_data, bytes_data) = (null, null);
            bool s = false;
            while (!s)
            {
                if (string_data != null || bytes_data != null)
                {
                    ws.Close();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    return true;
                }
                await Task.Delay(500);
            }
            return false;
        }
    }
}