// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace ServerSentEvents
{
    sealed class EventStreamReader
    {
        public int ReconnectionTime { get; set; }
        public string LastEventId { get; set; }

        private readonly Uri uri;
        private readonly Subject<EventSourceState> stateSubject = new Subject<EventSourceState>();
        
        // 切断用に使うHttpWebResponse
        private HttpWebResponse WebResponse;

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

        /// <summary>
        /// SSE Pushサーバと切断する
        /// </summary>
        public void Close()
        {
            if (this.WebResponse != null)
            {
                this.WebResponse.Close();
            }
        }

        /// <summary>
        /// Exponential Backoff 実行メソッド
        /// </summary>
        public static readonly Func<int, TimeSpan> ExponentialBackoff = n => TimeSpan.FromSeconds(Math.Pow(n, 2));
        
        /// <summary>
        /// 接続施行回数
        /// </summary>
        public int attempt = 0;
       
        /// <summary>
        /// SSE Pushサーバと接続し、メッセージを受信する
        /// </summary>
        /// <returns></returns>
        public IObservable<string> ReadLines()
        {
            Func<int, TimeSpan> strategy = ExponentialBackoff;
            var delay = Observable.Defer(() => Observable.Empty<string>().DelaySubscription(strategy(++attempt)));

            return Request()
                .Where(IsEventStream)
                .SelectMany(webResponse =>
                {
                    stateSubject.OnNext(EventSourceState.OPEN);
                    // 切断用にHttpWebResponseを保持
                    this.WebResponse = webResponse;
                    // 接続施行回数を初期化
                    this.attempt = 0;
                    return Read(webResponse);
                }).Finally(() =>
                    stateSubject.OnNext(EventSourceState.CLOSED)
                ).OnErrorResumeNext(delay).Repeat();
        }
    }
}
