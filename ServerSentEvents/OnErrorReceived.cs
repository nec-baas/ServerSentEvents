﻿using System.Net;

namespace ServerSentEvents
{
    /// <summary>
    /// エラー検知用コールバック
    /// </summary>
    public interface OnErrorReceived
    {
        /// <summary>
        /// エラー発生時に呼び出される。
        /// </summary>
        /// <param name="StatusCode">HTTPステータスコード</param>
        /// <param name="Response">エラー詳細情報</param>
        void OnError(HttpStatusCode StatusCode, HttpWebResponse Response);
    }
}
