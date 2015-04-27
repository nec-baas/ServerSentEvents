﻿// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using System;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerSentEvents.Tests
{
    [TestFixture]
    public class Tests
    {
        private readonly Uri baseUri = new Uri("http://localhost:8080");
        private TestWebServer ws;

        [TestFixtureSetUp]
        public void ServerSetUp()
        {
            ws = new TestWebServer(baseUri);
            ws.Start();
            ws.AddRoute("/simple", SimpleEventStream);
        }

        [TestFixtureTearDown]
        public void ServerTearDown()
        {
            ws.Stop();
        }

        private async Task SimpleEventStream(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.AutoFlush = true;

                for (int i = 0; i < 3; i++)
                {
                    await writer.WriteAsync("data: " + i + "\n\n");
                    await Task.Delay(1000);
                }
            }
        }

        [Test]
        public void TestEventSource()
        {
            DataRecorder dataRecorder = new DataRecorder("0", "1", "2");
            StateTransitionRecorder stateRecorder = new StateTransitionRecorder(
                EventSourceState.CONNECTING, EventSourceState.OPEN, EventSourceState.CLOSED);

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                es.StateChanged += stateRecorder.StateChanged;
                es.EventReceived += dataRecorder.EventReceived;
                es.Start();
                dataRecorder.Wait(5000);
                stateRecorder.Wait(5000);
            }

            stateRecorder.Assert();
            dataRecorder.Assert();
        }
    }
}

