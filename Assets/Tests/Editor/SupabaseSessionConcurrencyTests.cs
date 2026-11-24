using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Supabase;
using NUnit.Framework;
using UnityEngine;

namespace DOTORION.Tests
{
    public sealed class SupabaseSessionConcurrencyTests
    {
        [Test]
        public async Task ConcurrentRestAndRealtimeCallersRotateOnlyOnceAndCancelledWaiterDoesNotUnlock()
        {
            var clock = new Clock();
            var store = new Store();
            var transport = new Transport();
            var identity = new SupabaseIdentityClient("https://example.invalid", "test-publishable", transport, store, clock);
            var user = Guid.Parse("22222222-2222-4222-8222-222222222222");
            // No sign-in or platform credential store is involved in this race test.
            typeof(SupabaseIdentityClient).GetField("_session", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(identity, new SupabaseAuthSession(user, "test-old-access", "test-old-refresh", clock.UtcNow));

            var first = identity.GetValidSessionAsync(CancellationToken.None);
            var second = identity.GetValidSessionAsync(CancellationToken.None);
            using (var cancellation = new CancellationTokenSource())
            {
                var waiter = identity.GetValidSessionAsync(cancellation.Token);
                cancellation.Cancel();
                try { await waiter; Assert.Fail("The cancelled waiter must not get a session."); }
                catch (OperationCanceledException) { }
            }
            Assert.That(transport.Calls, Is.EqualTo(1));
            transport.Response.SetResult(new SupabaseHttpResponse(200, JsonUtility.ToJson(new AuthReply
            {
                access_token = "test-new-access", refresh_token = "test-new-refresh", expires_in = 3600
            })));
            var sessions = await Task.WhenAll(first, second);
            Assert.That(sessions[0], Is.SameAs(sessions[1]));
            Assert.That(sessions[0].UserId, Is.EqualTo(user));
            Assert.That(transport.Calls, Is.EqualTo(1));
            Assert.That(store.Saved.Count, Is.EqualTo(1));
            Assert.That(store.Saved[0], Is.SameAs(sessions[0]));
        }

        private sealed class Clock : ISupabaseClock
        {
            public DateTimeOffset UtcNow => new DateTimeOffset(2026, 11, 24, 0, 0, 0, TimeSpan.Zero);
        }
        private sealed class Store : ISupabaseAuthSessionStore
        {
            public readonly List<SupabaseAuthSession> Saved = new List<SupabaseAuthSession>();
            public bool TryLoad(out SupabaseAuthSession session) { session = null; return false; }
            public void Save(SupabaseAuthSession session) => Saved.Add(session);
            public void Delete() { }
        }
        private sealed class Transport : ISupabaseHttpTransport
        {
            public int Calls;
            public readonly TaskCompletionSource<SupabaseHttpResponse> Response =
                new TaskCompletionSource<SupabaseHttpResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Task<SupabaseHttpResponse> SendAsync(SupabaseHttpRequest request, CancellationToken token)
            {
                Interlocked.Increment(ref Calls);
                return Response.Task;
            }
        }
        [Serializable] private sealed class AuthReply
        {
            public string access_token, refresh_token;
            public long expires_in;
        }
    }
}
