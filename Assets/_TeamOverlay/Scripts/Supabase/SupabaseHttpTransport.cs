using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TeamOverlay.Supabase
{
    public sealed class SupabaseHttpRequest
    {
        public SupabaseHttpRequest(
            string method,
            string url,
            string body,
            IReadOnlyDictionary<string, string> headers)
        {
            Method = method;
            Url = url;
            Body = body;
            Headers = headers ?? new Dictionary<string, string>();
        }

        public string Method { get; }

        public string Url { get; }

        public string Body { get; }

        public IReadOnlyDictionary<string, string> Headers { get; }
    }

    public sealed class SupabaseHttpResponse
    {
        public SupabaseHttpResponse(int statusCode, string body)
        {
            StatusCode = statusCode;
            Body = body ?? string.Empty;
        }

        public int StatusCode { get; }

        public string Body { get; }

        public bool IsSuccess => StatusCode >= 200 && StatusCode <= 299;
    }

    public interface ISupabaseHttpTransport
    {
        Task<SupabaseHttpResponse> SendAsync(
            SupabaseHttpRequest request,
            CancellationToken cancellationToken);
    }

    public sealed class HttpClientSupabaseTransport : ISupabaseHttpTransport, IDisposable
    {
        private readonly HttpClient _client;

        public HttpClientSupabaseTransport()
        {
            _client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        public async Task<SupabaseHttpResponse> SendAsync(
            SupabaseHttpRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            using (var message = new HttpRequestMessage(new HttpMethod(request.Method), request.Url))
            {
                foreach (var header in request.Headers)
                {
                    message.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (request.Body != null)
                {
                    message.Content = new StringContent(request.Body, Encoding.UTF8, "application/json");
                }

                using (var response = await _client.SendAsync(message, cancellationToken))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new SupabaseHttpResponse((int)response.StatusCode, body);
                }
            }
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
