﻿// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Net;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;

namespace ServerSentEvents
{
    public enum EventSourceState
    {
        CONNECTING,
        OPEN,
        CLOSED
    }

    public class StateChangedEventArgs : EventArgs
    {
        private readonly EventSourceState state;

        public EventSourceState State { get { return state; } }

        public StateChangedEventArgs(EventSourceState state)
        {
            this.state = state;
        }
    }

    static class StringExtensions
    {
        public static string RemoveLastLineFeedCharacter(this string str)
        {
            return str.EndsWith("\n") ? str.Remove(str.Length - 1) : str;
        }
    }

    public sealed class ServerSentEvent
    {
        private readonly string lastEventId;
        private readonly string eventType;
        private readonly string data;
        private readonly int? retry;
        private readonly bool isEmptyData;

        public string LastEventId { get { return lastEventId; } }
        public string EventType { get { return eventType; } }
        public string Data { get { return data; } }
        public int? Retry { get { return retry; } }

        public bool IsEmptyData { get { return isEmptyData; } }

        public ServerSentEvent(string lastEventId, string eventType, string data, int? retry)
        {
            isEmptyData = string.IsNullOrEmpty(data);

            this.lastEventId = lastEventId;
            this.eventType = eventType;
            this.data = data.RemoveLastLineFeedCharacter();
            this.retry = retry;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();

            sb.Append("EventType: ").AppendLine(EventType);
            sb.Append("Data: ").AppendLine(Data);
            sb.Append("LastEventId: ").AppendLine(LastEventId);
            if (Retry.HasValue)
                sb.Append("Retry: ").AppendLine(Retry.Value.ToString());
            return sb.ToString();
        }
    }

    public class ServerSentEventReceivedEventArgs : EventArgs
    {
        private readonly ServerSentEvent message;

        public ServerSentEvent Message { get { return message; } }

        public ServerSentEventReceivedEventArgs(ServerSentEvent message)
        {
            this.message = message;
        }
    }

    public sealed class EventSource : IDisposable
    {
        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<ServerSentEventReceivedEventArgs> EventReceived;

        private readonly EventStreamReader reader;
        private IDisposable readSubscription;
        private IDisposable groupBySubscription;
        public OnErrorReceived OnErrorCallback { get; private set; }

        public EventSourceState ReadyState { get; private set; }


        public EventSource(Uri uri)
        {
            ReadyState = EventSourceState.CONNECTING;

            reader = new EventStreamReader(uri);
            reader.StateObservable.Subscribe(OnStateChanged);
        }

        private void DispatchEvent(ServerSentEvent sse)
        {
            if (sse.IsEmptyData)
                return;

            if (!string.IsNullOrEmpty(sse.LastEventId))
                reader.LastEventId = sse.LastEventId;

            if (sse.Retry.HasValue){
                reader.ReconnectionTime = sse.Retry.Value;
                // リトライフラグセット
                reader.RetryFlag = true;
            }
            OnEventReceived(sse);
        }

        // サーバとの切断(Stop())を呼ぶために、本クラスでOnErrorを一度受け取りparentを設定する
        private class InnerOnErrorCallback : OnErrorReceived
        {
            private EventSource parent;
            public InnerOnErrorCallback()
            {
            }

            public InnerOnErrorCallback(EventSource parent)
            {
                this.parent = parent;
            }

            public void OnError(HttpStatusCode StatusCode, HttpWebResponse Response)
            {
                switch (StatusCode)
                {
                    case HttpStatusCode.InternalServerError:
                        // 何もしない⇒自動再接続処理を行う
                        break;
                    case HttpStatusCode.ServiceUnavailable:
                        // リトライ値をセットして、自動再接続を行う
                        if (!string.IsNullOrEmpty(Response.Headers["Retry-After"]))
                        {
                            parent.reader.ReconnectionTime = int.Parse(Response.Headers["Retry-After"]);
                            // リトライフラグセット
                            parent.reader.RetryFlag = true;
                        }
                        break;
                    default :
                        // サーバと切断する
                        parent.Stop();
                        break;
                }

                // エラーコールバックを実行する
                parent.OnErrorCallback.OnError(StatusCode, Response);
            }
        }

        public void Start(string username, string password)
        {
            Debug.WriteLine("Start() <start> username=" + username);
            var closer = new Subject<Unit>();
            var subject = new Subject<string>();

            readSubscription = reader
                // 認証情報とエラーコールバックを登録
                .ReadLines(username, password, new InnerOnErrorCallback(this))
                .Subscribe(subject);

            // readSubscriptionをDispose()してもGroupByで生成したSubscriptionはDisposeしない(バグ？)ため、
            // readSubscriptionとgroupBySubscriptionに分け、Stop()時に両方Dispose()する。
            groupBySubscription = subject
                .GroupBy(string.IsNullOrEmpty)
                .Subscribe(g =>
                {
                    if (g.Key) // line is empty or null.
                    {
                        g.Subscribe(_ => closer.OnNext(Unit.Default));
                    }
                    else
                    {
                        g.Window(() => closer).Subscribe(window =>
                            window
                                .Where(line => !line.StartsWith(":"))
                                .Aggregate(new ServerSentEventBuilder(), (builder, line) => builder.AppendLine(line))
                                .Select(builder => builder.ToServerSentEvent())
                                .Subscribe(DispatchEvent));
                    }
                });
            Debug.WriteLine("Start() <end>");
        }

        // SSE Pushサーバとの接続を切断する
        public void Stop()
        {
            Debug.WriteLine("Stop() <start>");

            if (readSubscription != null)
            {
                readSubscription.Dispose();
            }
            if (groupBySubscription != null)
            {
                groupBySubscription.Dispose();
            }

            Debug.WriteLine("Stop() <end>");
        }

        public void Dispose()
        {
            this.Stop();
        }

        /// <summary>
        /// エラー発生時の処理
        /// </summary>
        /// <param name="Callback">エラー検知用コールバック</param>
        public void RegisterOnError(OnErrorReceived Callback)
        {
            Debug.WriteLine("RegisterOnError() <start>");
            this.OnErrorCallback = Callback;
            Debug.WriteLine("RegisterOnError() <end>");
        }

        private void OnEventReceived(ServerSentEvent sse)
        {
            if (EventReceived != null)
                EventReceived(this, new ServerSentEventReceivedEventArgs(sse));
        }

        private void OnStateChanged(EventSourceState newState)
        {
            ReadyState = newState;

            if (StateChanged != null)
                StateChanged(this, new StateChangedEventArgs(newState));
        }
    }
}
