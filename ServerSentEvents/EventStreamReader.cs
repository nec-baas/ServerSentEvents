// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ServerSentEvents
{
    class NewLineReceivedEventArgs : EventArgs
    {
        private readonly string line;

        public string Line { get { return line; } }

        public NewLineReceivedEventArgs(string line)
        {
            this.line = line;
        }
    }

    class EventStreamReader
    {
        public delegate void StateChangeNotifier(EventSourceState newState);

        public event EventHandler<NewLineReceivedEventArgs> NewLineReceived;
        public int ReconnectionTime { get; set; }
        public string LastEventId { get; set; }

        private readonly Uri uri;
        private readonly StateChangeNotifier stateChangeNotifier;

        public EventStreamReader(Uri uri, StateChangeNotifier stateChangeNotifier)
        {
            this.uri = uri;
            this.stateChangeNotifier = stateChangeNotifier;

            const int DefaultReconnectionTime = 3000; // in milliseconds
            ReconnectionTime = DefaultReconnectionTime;
        }

        private async Task<HttpWebResponse> Request()
        {
            var webRequest = WebRequest.Create(uri) as HttpWebRequest;

            webRequest.Accept = "text/event-stream";
            webRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            webRequest.KeepAlive = true;
            webRequest.Method = "GET";
            if (!string.IsNullOrEmpty(LastEventId))
                webRequest.Headers["Last-Event-ID"] = LastEventId;

            stateChangeNotifier(EventSourceState.CONNECTING);

            var webResponse = await webRequest.GetResponseAsync() as HttpWebResponse;
            return webResponse;
        }

        private async Task Read(HttpWebResponse webResponse, CancellationToken token)
        {
            using (var reader = new StreamReader(webResponse.GetResponseStream()))
            {
                string line;
                while (!token.IsCancellationRequested)
                {
                    line = await reader.ReadLineAsync();
                    OnNewLineReceived(line);
                    if (line == null)
                        break;
                }
            }
        }

        public async Task StartAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var webResponse = await Request();
                    if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.ContentType == "text/event-stream")
                    {
                        stateChangeNotifier(EventSourceState.OPEN);
                        await Read(webResponse, token);
                    }
                    else
                    {
                        // FIXME: Handle status code according to
                        // http://www.w3.org/TR/eventsource/#processing-model
                        return;
                    }
                }
                catch (WebException)
                {
                    await Task.Delay(ReconnectionTime, token);
                }
                finally
                {
                    stateChangeNotifier(EventSourceState.CLOSED);
                }
            }
        }

        private void OnNewLineReceived(string line)
        {
            if (NewLineReceived != null)
                NewLineReceived(this, new NewLineReceivedEventArgs(line));
        }
    }
}
