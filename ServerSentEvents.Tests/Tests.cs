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
using System.Text;
using System.Threading;

namespace ServerSentEvents.Tests
{
    [TestFixture]
    public class Tests
    {
        private readonly Uri baseUri = new Uri("http://localhost:8080");
        private TestWebServer ws;
        private string authString;
        private bool isConnectingCalled;
        private bool isOpenCalled;
        private bool isClosedCalled;
        private int try503Times;
        private const int DefaultRetryTime = 3000; //milli second

        [SetUp]
        public void ServerSetUp()
        {
            isConnectingCalled = false;
            isOpenCalled = false;
            isClosedCalled = false;
            try503Times = 0;

            // Basic認証ヘッダ
            authString = "Basic " + Convert.ToBase64String(Encoding.Default.GetBytes("Username:Password"));

            ws = new TestWebServer(baseUri);
            ws.Start();
            ws.AddRoute("/simple", SimpleEventStream);
            ws.AddRoute("/multiLineData", MultiLineDataEventStream);
            ws.AddRoute("/comments", EventStreamWithComments);
            ws.AddRoute("/500error", SimpleEventStreamException500);
            ws.AddRoute("/503error", SimpleEventStreamException503);
        }

        [TearDown]
        public void ServerTearDown()
        {
            ws.Stop();
        }

        private async Task SimpleEventStreamException500(HttpListenerRequest request, HttpListenerResponse response)
        {
            response.StatusCode = (int)HttpStatusCode.InternalServerError;
        }

        private async Task SimpleEventStreamException503(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (try503Times == 0)
            {
                response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                response.Headers["Retry-After"] = DefaultRetryTime.ToString();
                try503Times++;
            }
            else
            {
                // 2回目以降は接続成功とする
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
        }

        private async Task SimpleEventStream(HttpListenerRequest request, HttpListenerResponse response)
        {
            bool isUnAuthorized = false;

            // Basic認証ヘッダ確認
            if (request.Headers["Authorization"] != null)
            {
                if (!request.Headers["Authorization"].Equals(authString))
                {
                    isUnAuthorized = true;
                }
            }

            if (isUnAuthorized)
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
            }
            else
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
        }

        private async Task MultiLineDataEventStream(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.Headers["Authorization"] != null)
            {
                Assert.AreEqual(request.Headers["Authorization"], authString);
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.AutoFlush = true;
                await writer.WriteAsync("data: 1\n");
                await writer.WriteAsync("data: 2\n\n");

                await writer.WriteAsync("data: 3\n");
                await writer.WriteAsync("data: 4\n\n");

                await writer.WriteAsync("data: 5\n");
                await writer.WriteAsync("data: 6\n\n");
            }
        }

        private async Task EventStreamWithComments(HttpListenerRequest request, HttpListenerResponse response)
        {
            if (request.Headers["Authorization"] != null)
            {
                Assert.AreEqual(request.Headers["Authorization"], authString);
            }

            response.StatusCode = (int)HttpStatusCode.OK;
            response.ContentType = "text/event-stream";

            using (var writer = new StreamWriter(response.OutputStream))
            {
                writer.AutoFlush = true;
                await writer.WriteAsync(": This is a comment!\n");
                await writer.WriteAsync("data: 1\n\n");
                await writer.WriteAsync("data: 2\n\n");
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
            //var test = Notification.CreateOnCompleted<string>().ToString();
            var testObs = sseObs.TakeUntil(sseObs.Where(sse => sse.Data.Equals("2")));
//            var testObs = sseObs.TakeUntil(closeObs);

            return testObs;
        }

        private IObservable<ServerSentEvent> GetEventObservableMulti(EventSource es)
        {
            var sseObs = Observable.FromEventPattern<EventHandler<ServerSentEventReceivedEventArgs>, ServerSentEventReceivedEventArgs>(
                h => es.EventReceived += h, h => es.EventReceived -= h)
                .Select(p => p.EventArgs.Message);

            var testObs = sseObs.TakeUntil(sseObs.Where(sse => sse.Data.Equals("5\n6")));

            return testObs;
        }

        private IObservable<ServerSentEvent> GetEventObservableComments(EventSource es)
        {
            var sseObs = Observable.FromEventPattern<EventHandler<ServerSentEventReceivedEventArgs>, ServerSentEventReceivedEventArgs>(
                h => es.EventReceived += h, h => es.EventReceived -= h)
                .Select(p => p.EventArgs.Message);

            var testObs = sseObs.TakeUntil(sseObs.Where(sse => sse.Data.Equals("2")));

            return testObs;
        }

        private IObservable<EventSourceState> GetStateObservable(EventSource es)
        {
            var stateObs = Observable.FromEventPattern<EventHandler<StateChangedEventArgs>, StateChangedEventArgs>(
                h => es.StateChanged += h, h => es.StateChanged -= h)
                .Select(p => p.EventArgs.State);
                
            var testObs = stateObs.TakeUntil(stateObs.Where(state => state == EventSourceState.CLOSED));

            return testObs;
        }

        /// <summary>
        /// イベント受信ハンドラを登録
        /// 指定したハンドラを登録できること
        /// </summary>
        // TestEventSource()内、GetEventObservable()でテスト済

        /// <summary>
        /// 状態変更通知イベントハンドラを登録
        /// 指定したハンドラを登録できること
        /// Connecting, Open, Close イベントを取得できること
        /// </summary>
        [Test]
        public void TestStateChenged()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<EventSourceState>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                // イベントハンドラ登録
                //var testObs = GetStateObservable(es);
                // testObsに通知があった場合に、testObserverに通知する
                //testObs.Subscribe(testObserver.AsObserver());

                es.StateChanged += (sender, e) =>
                {
                    if (e.State == EventSourceState.CONNECTING)
                    {
                        isConnectingCalled = true;
                    }
                    if (e.State == EventSourceState.OPEN)
                    {
                        isOpenCalled = true;
                    }
                    if (e.State == EventSourceState.CLOSED)
                    {
                        isClosedCalled = true;
                    }
                };

                es.Start(null, null);
                Thread.Sleep(500);
                es.Stop();
                Thread.Sleep(100);

                //testObs.Wait();
            }
            Assert.IsTrue(isConnectingCalled);
            Assert.IsTrue(isOpenCalled);
            Assert.IsTrue(isClosedCalled);

            //Assert.AreEqual(3, testObserver.Messages.Count);
            //Assert.AreEqual(Notification.CreateOnNext(EventSourceState.CONNECTING), testObserver.Messages[0].Value);
            //Assert.AreEqual(Notification.CreateOnNext(EventSourceState.OPEN), testObserver.Messages[1].Value);
            //Assert.AreEqual(Notification.CreateOnCompleted<EventSourceState>(), testObserver.Messages[2].Value);
        }

        /// <summary>
        /// 接続開始(Basic認証なし)(成功)
        /// </summary>
        [Test]
        public void TestEventSourceNoBasicAuth()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                // testObsに通知があった場合に、testObserverに通知する
                testObs.Subscribe(testObserver.AsObserver());

                es.Start(null, null);
                //testObs.Wait.Timeout(TimeSpan.FromSeconds(4000));
                //await testObs.TimeoutException( (TimeSpan.FromSeconds(1));
                testObs.Wait();

            }

            //Assert.AreEqual(4, testObserver.Messages.Count);
            Assert.AreEqual(3, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("0"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[1].Value);
            //Assert.AreEqual(Notification.CreateOnNext("2"), testObserver.Messages[2].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[2].Value);
        }

        /// <summary>
        /// 接続開始(Basic認証あり)(成功)
        /// </summary>
        [Test]
        public void TestEventSourceWithBasicAuth()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start("Username", "Password");
                testObs.Wait();

            }

            Assert.AreEqual(3, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("0"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[1].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[2].Value);
        }

        /// <summary>
        /// 接続開始(Basic認証あり)(失敗)
        /// 401エラーが返り、サーバと切断すること
        /// </summary>
        [Test]
        public void TestEventSourceUnAuthorizedException()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<EventSourceState>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                // イベントハンドラ登録
                //var testObs = GetStateObservable(es);
                // testObsに通知があった場合に、testObserverに通知する
                //testObs.Subscribe(testObserver.AsObserver());

                // エラーコールバック登録
                OnErrorCallback errorCallback = new OnErrorCallback();
                es.RegisterOnError(errorCallback);

                // 接続状態変更通知登録
                es.StateChanged += (sender, e) =>
                {
                    if (e.State == EventSourceState.CLOSED)
                    {
                        isClosedCalled = true;
                    }
                };

                es.Start("InvalidUsername", "InvalidPassword");
                //testObs.Wait();

                // コールバック実行を待つ
                Thread.Sleep(100);
                /*
                try
                {
                    testObs.Wait();
                }
                catch (InvalidOperationException e)
                {
                    //ok
                }
                */
                Assert.IsTrue(errorCallback.isOnErrorCalled);
                Assert.IsTrue(isClosedCalled);
                Assert.AreEqual(errorCallback.stCode, HttpStatusCode.Unauthorized);
            }
        }

        /// <summary>
        /// 接続開始(異常)
        /// 500エラーが返り、自動再接続すること
        /// </summary>
        [Test]
        public void TestEventSourceAutoConnect()
        {
            // 自動再接続の最大回数(注：大きいとテストに時間がかかる)
            var reConnectMaxTimes = 3;
            bool isFinished = false;

            using (var es = new EventSource(new Uri(baseUri, "/500error")))
            {
                OnErrorCallback errorCallback = new OnErrorCallback();
                es.RegisterOnError(errorCallback);

                var i = 0;
                DateTime OldConnectingTime = DateTime.Now;
                DateTime ConnectingTime = OldConnectingTime;
                es.StateChanged += (sender, e) =>
                {
                    if (e.State == EventSourceState.CONNECTING)
                    {
                        if (i == 0)
                        {
                            // 現在時刻
                            ConnectingTime = DateTime.Now;
                            i++;
                        }
                        else if (i == reConnectMaxTimes)
                        {
                            isFinished = true;
                        }
                        else
                        {
                            OldConnectingTime = ConnectingTime;
                            ConnectingTime = DateTime.Now;
                            var timeSpan = ConnectingTime - OldConnectingTime;
                            // CONNECTINGの間隔を検証(Exponential Backoff)
                            Assert.IsTrue(TimeSpan.FromSeconds(Math.Pow(i, 2)) < timeSpan 
                                && timeSpan < TimeSpan.FromSeconds(Math.Pow(++i, 2)));
                        }
                    }
                };

                es.Start(null, null);
                while (!isFinished) { };
            }
        }

        /// <summary>
        /// 接続開始(異常)
        /// 503エラーが返り、レスポンスヘッダに含まれるリトライ値で待機後、接続すること
        /// </summary>
        [Test]
        public void TestEventSourceRetryWithHeaderValue()
        {
            //  TestWebServerからヘッダを返し、
            //  EventSourceState.CLOSEとEventSourceState.CONNECTINGを検知してその間の時間が何秒以上何秒未満かで確認する？

            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();
            TimeSpan timeSpan = TimeSpan.FromSeconds(0);

            using (var es = new EventSource(new Uri(baseUri, "/503error")))
            {
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                var i = 0;
                DateTime OldConnectingTime = DateTime.Now;
                DateTime ConnectingTime = OldConnectingTime;
                es.StateChanged += (sender, e) =>
                {
                    if (e.State == EventSourceState.CONNECTING)
                    {
                        if (i == 0)
                        {
                            // 接続1回目

                            // 現在時刻
                            ConnectingTime = DateTime.Now;
                            i++;
                        }
                        else if (i == 1)
                        {
                            // 接続2回目(接続OK)
                            OldConnectingTime = ConnectingTime;
                            ConnectingTime = DateTime.Now;
                            timeSpan = ConnectingTime - OldConnectingTime;
                        }
                        else
                        {
                            //do nothing
                        }
                    }
                };

                es.Start(null, null);
                testObs.Wait();
            }
            // DefaultRetryTime 以上 DefaultRetryTime+2秒　未満を検証
            Assert.IsTrue(TimeSpan.FromMilliseconds(DefaultRetryTime) < timeSpan
                                && timeSpan < (TimeSpan.FromMilliseconds(DefaultRetryTime) + TimeSpan.FromMilliseconds(2000)));
            Assert.AreEqual(3, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("0"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[1].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[2].Value);
        }

        /// <summary>
        /// 接続開始(複数行受信)(成功)
        /// </summary>
        [Test]
        public void TestMultiLineData()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/multiLineData")))
            {
                var testObs = GetEventObservableMulti(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start(null, null);
                testObs.Wait();
            }

            Assert.AreEqual(3, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("1\n2"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnNext("3\n4"), testObserver.Messages[1].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[2].Value);
        }

        /// <summary>
        /// 接続開始(コメント行受信)(成功)
        /// </summary>
        [Test]
        public void TestComments()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/comments")))
            {
                var testObs = GetEventObservableComments(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start(null,null);
                testObs.Wait();
            }

            Assert.AreEqual(2, testObserver.Messages.Count);
            Assert.AreEqual(Notification.CreateOnNext("1"), testObserver.Messages[0].Value);
            Assert.AreEqual(Notification.CreateOnCompleted<string>(), testObserver.Messages[1].Value);
        }

        /// <summary>
        /// SSE Pushサーバとの接続を切断する(成功)
        /// 受信が終了すること
        /// </summary>
        [Test]
        public void TestDisConnect()
        {
            var scheduler = new TestScheduler();
            var testObserver = scheduler.CreateObserver<string>();

            using (var es = new EventSource(new Uri(baseUri, "/simple")))
            {
                // コールバック登録
                es.StateChanged += (sender, e) =>
                {
                    if (e.State == EventSourceState.CLOSED)
                    {
                        isClosedCalled = true;
                    }
                };

                // 接続
                var testObs = GetEventObservable(es).Select(sse => sse.Data);
                testObs.Subscribe(testObserver.AsObserver());

                es.Start(null, null);
                // 受信完了まで待つ
                testObs.Wait();

                // 切断
                es.Stop();
                Thread.Sleep(500);

                // Check
                Assert.IsNull(es.readSubscription);
                Assert.IsTrue(isClosedCalled);
            }
        }

        // エラー受信用コールバック
        private class OnErrorCallback : OnErrorReceived
        {
            internal bool isOnErrorCalled = false;
            internal HttpStatusCode stCode;

            public void OnError(HttpStatusCode statusCode, HttpWebResponse response)
            {
                // コールバックが呼ばれたらtrue
                isOnErrorCalled = true;
                stCode = statusCode;
            }
        }
    }
}
