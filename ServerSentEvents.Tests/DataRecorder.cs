// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;

namespace ServerSentEvents.Tests
{
    public class DataRecorder
    {
        private readonly string[] expected;
        private readonly CountdownEvent countdown;

        private readonly IList<string> history = new List<string>();

        public string LastData { get; private set; }

        public DataRecorder(params string[] expected)
        {
            this.expected = expected;
            countdown = new CountdownEvent(expected.Length);
        }

        public void RecordData(string newData)
        {
            history.Add(newData);
            LastData = newData;
        }

        public void EventReceived(object _, ServerSentEventReceivedEventArgs e)
        {
            RecordData(e.Message.Data);
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

