// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
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

    public sealed class ServerSentEvent
    {
        private readonly string lastEventId;
        private readonly string eventType;
        private readonly string data;
        private readonly int? retry;

        public string LastEventId { get { return lastEventId; } }
        public string EventType { get { return eventType; } }
        public string Data { get { return data; } }
        public int? Retry { get { return retry; } }

        public ServerSentEvent(string lastEventId, string eventType, string data, int? retry)
        {
            this.lastEventId = lastEventId;
            this.eventType = eventType;
            this.data = data;
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

    public class EventSource
    {
        public event EventHandler<StateChangedEventArgs> StateChanged;
        public event EventHandler<ServerSentEventReceivedEventArgs> EventReceived;

        public EventSource(Uri uri)
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        private void OnEventReceived(ServerSentEvent sse)
        {
            if (EventReceived != null)
                EventReceived(this, new ServerSentEventReceivedEventArgs(sse));
        }

        private void OnStateChanged(EventSourceState newState)
        {
            if (StateChanged != null)
                StateChanged(this, new StateChangedEventArgs(newState));
        }
    }
}
