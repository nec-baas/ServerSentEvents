// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Reactive.Subjects;
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

    static class HttpWebResponseExtensions
    {
        public static string GetContentTypeIgnoringMimeType(this HttpWebResponse webResponse)
        {
            string contentType = webResponse.ContentType;
            if (contentType == null)
                return null;

            int indexOfSemicolon = contentType.IndexOf(";");
            return indexOfSemicolon == -1 ? contentType : contentType.Substring(0, indexOfSemicolon);
        }
    }

    sealed class EventStreamReader
    {

        public event EventHandler<NewLineReceivedEventArgs> NewLineReceived;
        public int ReconnectionTime { get; set; }
        public string LastEventId { get; set; }

        private readonly Uri uri;
        private readonly Subject<EventSourceState> stateSubject = new Subject<EventSourceState>();

        public IObservable<EventSourceState> StateObservable { get { return stateSubject; } }

        public EventStreamReader(Uri uri)
        {
            this.uri = uri;

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

            stateSubject.OnNext(EventSourceState.CONNECTING);

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
                    if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.GetContentTypeIgnoringMimeType() == "text/event-stream")
                    {
                        stateSubject.OnNext(EventSourceState.OPEN);
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
                    // Ignore exception and retry.
                }
                catch (Exception e)
                {
                    // FIXME: Use a logger instead of Console.Error.
                    // Log unexpected error.
                    Console.Error.WriteLine(e);
                }
                finally
                {
                    stateSubject.OnNext(EventSourceState.CLOSED);
                }

                await Task.Delay(ReconnectionTime, token);
            }
        }

        private void OnNewLineReceived(string line)
        {
            if (NewLineReceived != null)
                NewLineReceived(this, new NewLineReceivedEventArgs(line));
        }
    }
}
