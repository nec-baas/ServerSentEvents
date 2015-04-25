// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using ServerSentEvents;
using System;

namespace ServerSentEvents.Sample
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            var uri = new Uri("http://127.0.0.1:8000/events");
            var es = new EventSource(uri);
            es.EventReceived += (sender, e) => Console.WriteLine("Event: {0}", e.Message);
            es.StateChanged += (sender, e) => Console.WriteLine("State: {0}", e.State);
            es.Start();

            Console.WriteLine("Press any key to stop EventSource");
            Console.ReadKey();

            es.Stop();

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}
