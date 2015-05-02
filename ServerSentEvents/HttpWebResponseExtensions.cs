// Copyright (c) Kwang Yul Seo. All rights reserved.
// Licensed under the Apache 2.0 license. See LICENSE file in the project root for full license information.

using System.Net;

namespace ServerSentEvents
{
    static class HttpWebResponseExtensions
    {
        public static string GetContentTypeIgnoringMimeType(this HttpWebResponse webResponse)
        {
            string contentType = webResponse.ContentType;
            if (contentType == null)
                return null;

            int indexOfSemicolon = contentType.IndexOf(";");
            return indexOfSemicolon == -1 ? contentType : contentType.Substring(0, indexOfSemicolon);
        }
    }

}

