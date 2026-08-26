using System;
using UnityEngine;

namespace TeamOverlay.Supabase
{
    /// <summary>
    /// Turns a PostgREST or GoTrue error body into a typed exception. The RPCs
    /// raise stable machine-readable messages, so callers switch on
    /// <see cref="SupabaseApiException.ServerMessage"/> rather than on prose.
    /// </summary>
    internal static class SupabaseErrors
    {
        public static void EnsureSuccess(SupabaseHttpResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            if (response.IsSuccess)
            {
                return;
            }

            ErrorDocument error = null;
            try
            {
                error = JsonUtility.FromJson<ErrorDocument>(response.Body);
            }
            catch (ArgumentException)
            {
                // A non-JSON body (a proxy error page, for example) still has to
                // surface as an API exception rather than a parse failure.
            }

            var code = !string.IsNullOrWhiteSpace(error?.code)
                ? error.code
                : error?.error_code;
            var message = !string.IsNullOrWhiteSpace(error?.message)
                ? error.message
                : error?.msg;
            throw new SupabaseApiException(response.StatusCode, code, message);
        }

        [Serializable]
        private sealed class ErrorDocument
        {
            public string code;
            public string error_code;
            public string message;
            public string msg;
        }
    }
}
