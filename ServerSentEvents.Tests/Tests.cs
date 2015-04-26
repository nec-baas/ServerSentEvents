// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using NUnit.Framework;
using System;

namespace ServerSentEvents.Tests
{
    [TestFixture]
    public class Tests
    {
        private TestWebServer ws;

        [TestFixtureSetUp]
        public void ServerSetUp()
        {
            ws = new TestWebServer(new Uri("http://localhost:8080"));
            ws.Start();
        }

        [TestFixtureTearDown]
        public void ServerTearDown()
        {
            ws.Stop();
        }

        public Tests()
        {
        }
    }
}

