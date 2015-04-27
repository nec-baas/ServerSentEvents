// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace ServerSentEvents.Tests
{
    public class TestWebServer
    {
        private readonly Dictionary<string, Func<HttpListenerRequest, HttpListenerResponse, Task>> routeDict;
        private readonly HttpListener listener;
        private readonly CancellationTokenSource cts;

        public TestWebServer(params Uri[] baseUris)
        {
            if (baseUris.Length == 0)
                throw new ArgumentException("baseUris");

            listener = new HttpListener();
            cts = new CancellationTokenSource();

            routeDict = new Dictionary<string, Func<HttpListenerRequest, HttpListenerResponse, Task>>();

            foreach (var baseUri in baseUris)
                listener.Prefixes.Add(baseUri.ToString().Replace("localhost", "+"));
        }

        public void AddRoute(string path, Func<HttpListenerRequest, HttpListenerResponse, Task> route)
        {
            routeDict.Add(path, route);
        }

        private async void ProcessAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();

                string url = ctx.Request.RawUrl;
                Func<HttpListenerRequest, HttpListenerResponse, Task> route;

                try
                {
                    if (routeDict.TryGetValue(url, out route))
                        await route(ctx.Request, ctx.Response);
                    else
                        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                finally
                {
                    ctx.Response.OutputStream.Close();
                }
            }
        }

        public void Start()
        {
            listener.Start();

            try
            {
                CancellationToken token = cts.Token;
                Task.Run(() => ProcessAsync(token));
            }
            catch (HttpListenerException)
            {
                // This will be thrown when listener is closed while waiting for a request
                return;
            }
        }

        public void Stop()
        {
            listener.Stop();
            cts.Cancel();
        }
    }
}
