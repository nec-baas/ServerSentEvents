﻿// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Net;
using System.Net.Cache;
using System.IO;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace ServerSentEvents
{
    sealed class EventStreamReader
    {
        // 再接続時間(milli second)
        public int ReconnectionTime { get; set; }
        // 再接続時間のデフォルト値(milli second)
        public int DefaultReconnectionTime { get; private set; }
        public string LastEventId { get; set; }
        // エラー検知用コールバック
        private OnErrorReceived OnErrorCallback;

        private readonly Uri uri;
        private readonly Subject<EventSourceState> stateSubject = new Subject<EventSourceState>();
        
        // 切断用に使うHttpWebResponse
        private HttpWebResponse WebResponse;

        public IObservable<EventSourceState> StateObservable { get { return stateSubject; } }

        public EventStreamReader(Uri uri)
        {
            this.uri = uri;

            this.DefaultReconnectionTime = 3000; // in milliseconds
            ReconnectionTime = this.DefaultReconnectionTime;
        }

        // Basic認証ヘッダをセットする
        public void SetBasicAuthHeader(WebRequest request, string userName, string userPassword)
        {
            string authInfo = userName + ":" + userPassword;
            authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));
            request.Headers["Authorization"] = "Basic " + authInfo;
        }

        private IObservable<HttpWebResponse> Request(string Username, string Password)
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
                // Username, Passwordが設定された時はBasic認証を行う
                if (!string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password))
                {
                    SetBasicAuthHeader(webRequest, Username, Password);
                }
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

        private bool IsEventStream(HttpWebResponse response)
        {
            // HttpStatusCode==OK 以外はエラーコールバックを実行する
            if (response.StatusCode != HttpStatusCode.OK)
            {
                // エラーをコールバックする
                if (this.OnErrorCallback != null)
                {
                    this.OnErrorCallback.OnError(response.StatusCode, response);
                }
            }
            return response.StatusCode == HttpStatusCode.OK
                && response.GetContentTypeIgnoringMimeType() == "text/event-stream";
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
        public IObservable<string> ReadLines(string Username, string Password, OnErrorReceived OnErrorCallback)
        {
            this.OnErrorCallback = OnErrorCallback;
            Func<int, TimeSpan> strategy = ExponentialBackoff;

            var delay = Observable.Defer(() =>
            {
                // 再接続時間が初期値の場合はExponential Backoffで再接続
                if (this.ReconnectionTime == this.DefaultReconnectionTime)
                {
                    return Observable.Empty<string>().DelaySubscription(strategy(++attempt));
                }
                // サーバからリトライ値が設定された場合は接続成功するまでその値で再接続
                else
                {
                    return Observable.Empty<string>().Delay(TimeSpan.FromMilliseconds(this.ReconnectionTime));
                }

            });

            return Request(Username, Password)
                .Where(IsEventStream)
                .SelectMany(webResponse =>
                {
                    stateSubject.OnNext(EventSourceState.OPEN);
                    // 切断用にHttpWebResponseを保持
                    this.WebResponse = webResponse;
                    // 再接続時間を初期化
                    this.ReconnectionTime = this.DefaultReconnectionTime;
                    // 接続施行回数を初期化
                    this.attempt = 0;
                    return Read(webResponse);
                }).Finally(() =>
                    stateSubject.OnNext(EventSourceState.CLOSED)
                ).OnErrorResumeNext(delay).Repeat();
        }

    }
}
