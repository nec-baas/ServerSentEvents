// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using System;
using System.Net;
using System.IO;
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
            ws.AddRoute("/multiLineData", MultiLineDataEventStream);
            ws.AddRoute("/comments", EventStreamWithComments);
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

        private async Task MultiLineDataEventStream(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.AutoFlush = true;
                await writer.WriteAsync("data: 1\n");
                await writer.WriteAsync("data: 2\n\n");

                await writer.WriteAsync("data: 3\n");
                await writer.WriteAsync("data: 4\n\n");
            }
        }

        private async Task EventStreamWithComments(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.AutoFlush = true;
                await writer.WriteAsync(": This is a comment!\n");
                await writer.WriteAsync("data: 1\n\n");
                await writer.WriteAsync(": This is another comment!\n");
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

        [Test]
        public void TestMultiLineData()
        {
            DataRecorder dataRecorder = new DataRecorder("1\n2", "3\n4");

            using (var es = new EventSource(new Uri(baseUri, "/multiLineData")))
            {
                es.EventReceived += dataRecorder.EventReceived;
                es.Start();
                dataRecorder.Wait(5000);
            }

            dataRecorder.Assert();
        }

        [Test]
        public void TestComments()
        {
            DataRecorder dataRecorder = new DataRecorder("1");

            using (var es = new EventSource(new Uri(baseUri, "/comments")))
            {
                es.EventReceived += dataRecorder.EventReceived;
                es.Start();
                dataRecorder.Wait(5000);
            }

            dataRecorder.Assert();
        }
    }
}
