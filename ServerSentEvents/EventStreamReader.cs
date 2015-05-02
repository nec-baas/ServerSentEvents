// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;

namespace ServerSentEvents
{
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
            return Observable.Using(
                () => new StreamReader(webResponse.GetResponseStream()),
                reader => Observable.FromAsync(reader.ReadLineAsync).Repeat().TakeWhile(line => line != null));
        }

        public IObservable<string> ReadLines()
        {
            return Observable.Create(
                new Func<IObserver<string>, CancellationToken, Task<IDisposable>>(async (observer, token) =>
                {
                    try
                    {
                        var webResponse = await Request();

                        // FIXME: Handle status code according to
                        // http://www.w3.org/TR/eventsource/#processing-model
                        if (webResponse.StatusCode == HttpStatusCode.OK && webResponse.GetContentTypeIgnoringMimeType() == "text/event-stream")
                        {
                            stateSubject.OnNext(EventSourceState.OPEN);
                            return Read(webResponse).Subscribe(observer);
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
                        await Task.Delay(ReconnectionTime, token);
                    }

                    return Disposable.Empty;
                })).Repeat();
        }
    }
}
