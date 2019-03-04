using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Brisk.Web
{
    public class WebServer
    {
        public delegate void WebHandler(HttpListenerRequest req, HttpListenerResponse res);
        
        public int Port { get; private set; }
        
        private readonly HttpListener listener = new HttpListener();
        private readonly IDictionary<string, WebHandler> paths = new ConcurrentDictionary<string, WebHandler>();
 
        public WebServer(int port)
        {
            if (!HttpListener.IsSupported)
            {
                Debug.Log("HTTP Listener is not supported");
                return;
            }
 
            listener.Prefixes.Add($"http://*:{port}/");
            Port = port;
            listener.Start();
        }

        public void AddPath(string path, WebHandler handler)
        {
            paths.Add(path, handler);
        }
 
        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                while (listener.IsListening)
                {
                    ThreadPool.QueueUserWorkItem(c =>
                    {
                        var ctx = (HttpListenerContext) c;
                        if (paths.ContainsKey(ctx.Request.Url.AbsolutePath))
                            paths[ctx.Request.Url.AbsolutePath](ctx.Request, ctx.Response);
                        else
                            ctx.Response.StatusCode = (int) HttpStatusCode.NotFound;
                        ctx.Response.OutputStream.Close();
                    }, listener.GetContext());
                }
            });
        }
 
        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }
    }
}