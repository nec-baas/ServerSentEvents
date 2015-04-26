// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
        private readonly ServerSentEventBuilder builder;
        private readonly CancellationTokenSource cts;

        public EventSource(Uri uri)
        {
            reader = new EventStreamReader(uri, OnStateChanged);
            reader.NewLineReceived += NewLineReceived;

            builder = new ServerSentEventBuilder();

            cts = new CancellationTokenSource();
        }

        private void NewLineReceived(object sender, NewLineReceivedEventArgs e)
        {
            builder.AddLine(e.Line);
            if (builder.IsDone())
            {
                var sse = builder.ToServerSentEvent();
                DispatchEvent(sse);
                builder.Reset();
            }
        }

        private void DispatchEvent(ServerSentEvent sse)
        {
            if (sse.IsEmptyData)
                return;

            if (!string.IsNullOrEmpty(sse.LastEventId))
                reader.LastEventId = sse.LastEventId;

            if (sse.Retry.HasValue)
                reader.ReconnectionTime = sse.Retry.Value;

            OnEventReceived(sse);
        }

        public void Start()
        {
            var token = cts.Token;
            Task.Run(() => reader.StartAsync(token));
        }

        public void Stop()
        {
            cts.Cancel();
        }

        public void Dispose()
        {
            this.Stop();
        }

        private void OnEventReceived(ServerSentEvent sse)
        {
            if (EventReceived != null)
                EventReceived(this, new ServerSentEventReceivedEventArgs(sse));
        }

        private void OnStateChanged(EventSourceState newState)
        {
            builder.Reset();

            if (StateChanged != null)
                StateChanged(this, new StateChangedEventArgs(newState));
        }
    }
}
