// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System;
using System.Text;

namespace ServerSentEvents
{
    class ServerSentEventBuilder
    {
        private bool isDone;

        private string lastEventId;
        private string eventType;
        private StringBuilder dataBuilder;
        private int? retry;

        public ServerSentEventBuilder()
        {
            Reset();
        }

        public bool IsDataEmpty() { return dataBuilder.Length == 0; }

        public bool IsDone() { return isDone; }

        public void Reset()
        {
            lastEventId = string.Empty;
            eventType = string.Empty;
            dataBuilder = new StringBuilder();
            retry = null;
            isDone = false;
        }

        public ServerSentEvent ToServerSentEvent()
        {
            return new ServerSentEvent(lastEventId, eventType, dataBuilder.ToString(), retry);
        }

        private static Tuple<string, string> ParseFieldAndValue(string line)
        {
            int indexOfColon = line.IndexOf(":");
            var field = line.Substring(0, indexOfColon);
            var value = line.Substring(indexOfColon + 1, line.Length - indexOfColon - 1);
            if (value.StartsWith(" "))
                value = value.Substring(1);
            return Tuple.Create(field, value);
        }

        public void AddLine(string line)
        {
            if (string.IsNullOrEmpty(line))
            {
                isDone = true;
                return;
            }

            // Comment
            if (line.StartsWith(":"))
                return;

            string field;
            string value;

            if (line.Contains(":"))
            {
                var fieldAndValue = ParseFieldAndValue(line);
                field = fieldAndValue.Item1;
                value = fieldAndValue.Item2;
            }
            else
            {
                field = line;
                value = string.Empty;
            }

            switch (field)
            {
                case "event":
                    eventType = value;
                    break;
                case "data":
                    dataBuilder.Append(value);
                    dataBuilder.Append("\n"); // U+000A LINE FEED (LF) character
                    break;
                case "id":
                    lastEventId = value;
                    break;
                case "retry":
                    int intValue;
                    if (Int32.TryParse(value, out intValue))
                        retry = intValue;
                    break;
            }
        }
    }
}
