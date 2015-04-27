// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;

namespace ServerSentEvents.Tests
{
    public class StateTransitionRecorder
    {
        private readonly EventSourceState[] expected;
        private readonly CountdownEvent countdown;

        private readonly IList<EventSourceState> history = new List<EventSourceState>();

        public EventSourceState LastState { get; private set; }

        public StateTransitionRecorder(params EventSourceState[] expected)
        {
            this.expected = expected;
            countdown = new CountdownEvent(expected.Length);
        }

        public void RecordState(EventSourceState newState)
        {
            history.Add(newState);
            LastState = newState;
        }

        public void StateChanged(object _, StateChangedEventArgs e)
        {
            RecordState(e.State);
            countdown.Signal();
        }

        public void Assert()
        {
            CollectionAssert.AreEqual(expected, history);
        }

        public void Wait(int millisecondsTimeout)
        {
            countdown.Wait(millisecondsTimeout);
        }
    }
}
