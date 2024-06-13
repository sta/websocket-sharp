using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine;

using WebSocketSharp;
using WebSocketSharp.Server;

public static class GlobalVariables
{
    public static float azimuth = 0;
    public static float elevation = 0;
    public static float zoom = 1;
}

public class Example1 : WebSocketBehavior
{
    // this is the data that we send using websockets (as JSON)
    [Serializable]
    public class WSData
    {
        public float az;
        public float el;
        public float z;
    }

    protected override void OnMessage(MessageEventArgs e)
    {
        WSData data = JsonUtility.FromJson<WSData>(e.Data);
        GlobalVariables.azimuth = data.az / (float)Math.PI * 180;
        GlobalVariables.elevation = data.el / (float)Math.PI * 180;
        GlobalVariables.zoom = data.z;
    }

    protected override void OnOpen()
    {
        Debug.Log("connection opened");
    }
}

public class Controller : MonoBehaviour
{
    public string m_IP = "127.0.0.1";
    public int m_port = 55555;
    private WebSocketServer m_WSServer = null;

    void Start()
    {
        string addr = "ws://" + m_IP + ":" + m_port.ToString();
        Debug.Log("Run WS Server at: " + addr);
        m_WSServer = new WebSocketServer(addr);
        m_WSServer.AddWebSocketService<Example1>("/Example1");
        m_WSServer.Start();
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
        gameObject.transform.eulerAngles = new Vector3(GlobalVariables.elevation, GlobalVariables.azimuth, gameObject.transform.eulerAngles.z);
        gameObject.transform.localScale = new Vector3(GlobalVariables.zoom, GlobalVariables.zoom, GlobalVariables.zoom);
    }
}
