// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using Microsoft.Reactive.Testing;
using NUnit.Framework;
using System;
using System.Net;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace ServerSentEvents.Tests
{
    [TestFixture]
    public class Tests
    {
        private readonly Uri baseUri = new Uri("http://localhost:8080");
        private TestWebServer ws;

        [SetUp]
        public void ServerSetUp()
        {
            ws = new TestWebServer(baseUri);
            ws.Start();
            ws.AddRoute("/simple", SimpleEventStream);
            ws.AddRoute("/multiLineData", MultiLineDataEventStream);
            ws.AddRoute("/comments", EventStreamWithComments);
        }

        [TearDown]
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

        private IObservable<ServerSentEvent> GetEventObservable(EventSource es)
        {
            var sseObs = Observable.FromEventPattern<EventHandler<ServerSentEventReceivedEventArgs>, ServerSentEventReceivedEventArgs>(
                h => es.EventReceived += h, h => es.EventReceived -= h)
                .Select(p => p.EventArgs.Message);
            var closeObs = Observable.FromEventPattern<EventHandler<StateChangedEventArgs>, StateChangedEventArgs>(
                h => es.StateChanged += h, h => es.StateChanged -= h)
                .Select(p => p.EventArgs.State)
                .Where(state => state == EventSourceState.CLOSED);
            var testObs = sseObs.TakeUntil(closeObs);

            return testObs;
        }

        [Test]
        public void TestEventSource()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start();
                testObs.Wait();
            }

            Assert.AreEqual(4, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("0"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[1].Value);
            Assert.AreEqual(Notification.CreateOnNext("2"), testObserver.Messages[2].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[3].Value);
        }

        [Test]
        public void TestMultiLineData()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/multiLineData")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start();
                testObs.Wait();
            }

            Assert.AreEqual(3, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("1\n2"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("3\n4"), testObserver.Messages[1].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[2].Value);
        }

        [Test]
        public void TestComments()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/comments")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start();
                testObs.Wait();
            }

            Assert.AreEqual(2, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[1].Value);
        }
    }
}
