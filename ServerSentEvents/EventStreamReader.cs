// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;

namespace ServerSentEvents
{
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

        private IObservable<string> Read(HttpWebResponse webResponse)
        {
            return Observable.Create<string>(async (observer, token) =>
            {
                using (var reader = new StreamReader(webResponse.GetResponseStream()))
                {
                    string line;
                    while (!token.IsCancellationRequested)
                    {
                        line = await reader.ReadLineAsync();
                        observer.OnNext(line);
                        if (line == null)
                            break;
                    }
                }
            });
        }

        public IObservable<string> ReadLines()
        {
            return Observable.Create<string>(async (observer, token) =>
            {
                while (!token.IsCancellationRequested)
                {
                    try
                    {
                        var webResponse = await Request();
                        if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.GetContentTypeIgnoringMimeType() == "text/event-stream")
                        {
                            stateSubject.OnNext(EventSourceState.OPEN);
                            Read(webResponse).Subscribe(observer);
                        }
                        else
                        {
                            // FIXME: Handle status code according to
                            // http://www.w3.org/TR/eventsource/#processing-model
                            break;
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
            });
        }
    }
}
