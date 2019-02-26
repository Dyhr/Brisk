using System;
using System.Net;
using System.Text;
using System.Threading;

namespace Brisk.Web
{
    public class WebServer
    {
        public int Port { get; private set; }
        
        private readonly HttpListener listener = new HttpListener();
        private readonly Func<HttpListenerRequest, string> responderMethod;
 
        public WebServer(int port, Func<HttpListenerRequest, string> method)
        {
            if (!HttpListener.IsSupported)
                throw new NotSupportedException("HTTP Listener is not supported");
            
            // A responder method is required
            if (method == null)
                throw new ArgumentException("method");
 
            listener.Prefixes.Add($"http://*:{port}/");
 
            responderMethod = method;
            Port = port;
            
            listener.Start();
        }
 
        public void Run()
        {
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (listener.IsListening)
                    {
                        ThreadPool.QueueUserWorkItem((c) =>
                        {
                            var ctx = c as HttpListenerContext;
                            try
                            {
                                var rstr = responderMethod(ctx.Request);
                                var buf = Encoding.UTF8.GetBytes(rstr);
                                ctx.Response.ContentLength64 = buf.Length;
                                ctx.Response.OutputStream.Write(buf, 0, buf.Length);
                            }
                            catch { } // suppress any exceptions
                            finally
                            {
                                // always close the stream
                                ctx.Response.OutputStream.Close();
                            }
                        }, listener.GetContext());
                    }
                }
                catch { } // suppress any exceptions
            });
        }
 
        public void Stop()
        {
            listener.Stop();
            listener.Close();
        }
    }
}