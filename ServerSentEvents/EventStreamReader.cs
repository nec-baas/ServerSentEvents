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

        private IObservable<HttpWebResponse> Request()
        {
            return Observable.Defer(() =>
            {
                var webRequest = WebRequest.Create(uri) as HttpWebRequest;

                webRequest.Accept = "text/event-stream";
                webRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
                webRequest.KeepAlive = true;
                webRequest.Method = "GET";
                if (!string.IsNullOrEmpty(LastEventId))
                    webRequest.Headers["Last-Event-ID"] = LastEventId;

                stateSubject.OnNext(EventSourceState.CONNECTING);

                return Observable.FromAsync(webRequest.GetResponseAsync).Cast<HttpWebResponse>();
            });
        }

        private IObservable<string> Read(HttpWebResponse webResponse)
        {
            return Observable.Using(
                () => new StreamReader(webResponse.GetResponseStream()),
                reader => Observable.FromAsync(reader.ReadLineAsync).Repeat().TakeWhile(line => line != null));
        }

        private static bool IsEventStream(HttpWebResponse response)
        {
            // FIXME: Handle status code according to
            // http://www.w3.org/TR/eventsource/#processing-model
            return response.StatusCode == HttpStatusCode.OK
                && response.GetContentTypeIgnoringMimeType() == "text/event-stream";
        }

        public IObservable<string> ReadLines()
        {
            var delay = Observable.Empty<string>().Delay(TimeSpan.FromMilliseconds(ReconnectionTime));
            return Request()
                .Where(IsEventStream)
                .SelectMany(webResponse =>
                {
                    stateSubject.OnNext(EventSourceState.OPEN);
                    return Read(webResponse);
                }).Catch(new Func<Exception, IObservable<string>>(e =>
                {
                    // FIXME: Use a logger instead of Console.Error.
                    // Log unexpected error.
                    Console.Error.WriteLine(e);
                    return Observable.Empty<string>();
                })).Finally(() =>
                    stateSubject.OnNext(EventSourceState.CLOSED)
                ).Concat(delay).Repeat();
        }
    }
}
