using System;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example2 {

  public class Echo : WebSocketService
  {
    protected override void OnMessage(MessageEventArgs e)
    {
      var msg = QueryString.Exists("name")
              ? String.Format("'{0}' returns to {1}", e.Data, QueryString["name"])
              : e.Data;
      Send(msg);
    }

    protected override bool ProcessCookies(CookieCollection request, CookieCollection response)
    {
      foreach (Cookie cookie in request)
      {
        cookie.Expired = true;
        response.Add(cookie);
      }

      return true;
    }
  }
}
