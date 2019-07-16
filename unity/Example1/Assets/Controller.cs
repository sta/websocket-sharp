using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using WebSocketSharp;
using WebSocketSharp.Server;

public class Controller : MonoBehaviour
{
    private WebSocketServer m_WSServer = null;

    public class Laputa : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            var msg = e.Data == "BALUS"
                      ? "I've been balused already..."
                      : "I'm not available now.";

            Send(msg);
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        Debug.Log("Start");

        m_WSServer = new WebSocketServer("ws://127.0.0.1:55555");
        m_WSServer.AddWebSocketService<Laputa>("/Laputa");
        m_WSServer.Start();

        Debug.Log("Start ended");
    }

    void OnDestroy()
    {
        Debug.Log("OnDestroy");
        m_WSServer.Stop();
        m_WSServer = null;
    }

    // Update is called once per frame
    void Update()
    {
    }
}
