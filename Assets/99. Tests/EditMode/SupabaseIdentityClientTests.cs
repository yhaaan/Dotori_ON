using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamOverlay.Supabase;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class SupabaseIdentityClientTests
    {
        private static readonly Guid UserId =
            new Guid("11111111-2222-4333-8444-555555555555");
        private static readonly Guid TeamId =
            new Guid("00000000-0000-4000-8000-000000000001");
        private static readonly DateTimeOffset Now =
            new DateTimeOffset(2026, 8, 26, 2, 0, 0, TimeSpan.Zero);

        [Test]
        public async Task InitializeWithoutStoredSession_CreatesAndPersistsAnonymousIdentity()
        {
            var transport = new QueueTransport(
                Ok(AuthJson("access-a", "refresh-a", Now.AddHours(1))),
                Ok("[]"));
            var store = new MemorySessionStore();
            var client = CreateClient(transport, store);

            var result = await client.InitializeAsync(CancellationToken.None);

            Assert.That(result.CreatedAnonymousUser, Is.True);
            Assert.That(result.Session.UserId, Is.EqualTo(UserId));
            Assert.That(result.Member, Is.Null);
            Assert.That(store.Saved.UserId, Is.EqualTo(UserId));
            Assert.That(transport.Requests[0].Url, Does.EndWith("/auth/v1/signup"));
            Assert.That(transport.Requests[0].Headers.ContainsKey("Authorization"), Is.False);
            Assert.That(transport.Requests[1].Headers["Authorization"], Is.EqualTo("Bearer access-a"));
        }

        [Test]
        public async Task InitializeWithExpiredSession_RefreshesRotatedTokenBeforeMemberQuery()
        {
            var store = new MemorySessionStore
            {
                Saved = new SupabaseAuthSession(
                    UserId,
                    "expired-access",
                    "old-refresh",
                    Now.AddMinutes(-1))
            };
            var transport = new QueueTransport(
                Ok(AuthJson("new-access", "new-refresh", Now.AddHours(1))),
                Ok("[" + MemberJson("하늘") + "]"));
            var client = CreateClient(transport, store);

            var result = await client.InitializeAsync(CancellationToken.None);

            Assert.That(result.CreatedAnonymousUser, Is.False);
            Assert.That(result.Member.DisplayName, Is.EqualTo("하늘"));
            Assert.That(store.Saved.AccessToken, Is.EqualTo("new-access"));
            Assert.That(store.Saved.RefreshToken, Is.EqualTo("new-refresh"));
            Assert.That(transport.Requests[0].Url, Does.Contain("grant_type=refresh_token"));
            Assert.That(transport.Requests[1].Headers["Authorization"], Is.EqualTo("Bearer new-access"));
        }

        [Test]
        public void InvalidRefreshToken_NeverCreatesReplacementAnonymousUser()
        {
            var original = new SupabaseAuthSession(
                UserId,
                "expired-access",
                "invalid-refresh",
                Now.AddMinutes(-1));
            var store = new MemorySessionStore { Saved = original };
            var transport = new QueueTransport(
                new SupabaseHttpResponse(
                    400,
                    "{\"error_code\":\"refresh_token_not_found\",\"msg\":\"Invalid Refresh Token\"}"));
            var client = CreateClient(transport, store);

            Assert.ThrowsAsync<SupabaseIdentityRecoveryException>(async () =>
                await client.InitializeAsync(CancellationToken.None));

            Assert.That(transport.Requests, Has.Count.EqualTo(1));
            Assert.That(transport.Requests[0].Url, Does.Contain("grant_type=refresh_token"));
            Assert.That(store.Saved, Is.SameAs(original));
            Assert.That(store.DeleteCount, Is.Zero);
        }

        [Test]
        public async Task ClaimMemberName_UsesAuthenticatedRpcAndParsesSingleMemberObject()
        {
            var store = new MemorySessionStore
            {
                Saved = new SupabaseAuthSession(
                    UserId,
                    "valid-access",
                    "valid-refresh",
                    Now.AddHours(1))
            };
            var transport = new QueueTransport(
                Ok("[]"),
                Ok(MemberJson("김 하늘")));
            var client = CreateClient(transport, store);
            await client.InitializeAsync(CancellationToken.None);

            var member = await client.ClaimMemberNameAsync("  김   하늘  ", CancellationToken.None);

            Assert.That(member.Id, Is.EqualTo(UserId));
            Assert.That(member.TeamId, Is.EqualTo(TeamId));
            Assert.That(member.DisplayName, Is.EqualTo("김 하늘"));
            Assert.That(member.SortOrder, Is.Zero);
            Assert.That(transport.Requests[1].Url, Does.EndWith("/rest/v1/rpc/claim_member_name"));
            Assert.That(transport.Requests[1].Headers["Authorization"], Is.EqualTo("Bearer valid-access"));
            Assert.That(transport.Requests[1].Body, Does.Contain("김 하늘"));
        }

        [Test]
        public async Task GetTeamCapacity_ReadsSlotUsageWithoutASession()
        {
            var transport = new QueueTransport(Ok("[{\"occupied\":4,\"capacity\":4}]"));
            var client = CreateClient(transport, new MemorySessionStore());

            var capacity = await client.GetTeamCapacityAsync(CancellationToken.None);

            Assert.That(capacity.Occupied, Is.EqualTo(4));
            Assert.That(capacity.Capacity, Is.EqualTo(4));
            Assert.That(capacity.HasRoom, Is.False);
            Assert.That(transport.Requests[0].Url, Does.EndWith("/rest/v1/rpc/team_capacity"));
            Assert.That(transport.Requests[0].Headers.ContainsKey("Authorization"), Is.False);
        }

        [Test]
        public async Task GetTeamCapacity_ReportsRoomWhenSlotsRemain()
        {
            var transport = new QueueTransport(Ok("[{\"occupied\":1,\"capacity\":4}]"));
            var client = CreateClient(transport, new MemorySessionStore());

            var capacity = await client.GetTeamCapacityAsync(CancellationToken.None);

            Assert.That(capacity.HasRoom, Is.True);
        }

        private static SupabaseIdentityClient CreateClient(
            QueueTransport transport,
            MemorySessionStore store)
        {
            return new SupabaseIdentityClient(
                "https://project.supabase.co",
                "sb_publishable_test",
                transport,
                store,
                new FixedClock());
        }

        private static SupabaseHttpResponse Ok(string body)
        {
            return new SupabaseHttpResponse(200, body);
        }

        private static string AuthJson(
            string accessToken,
            string refreshToken,
            DateTimeOffset expiresAt)
        {
            return "{\"access_token\":\"" + accessToken +
                   "\",\"refresh_token\":\"" + refreshToken +
                   "\",\"expires_in\":3600,\"expires_at\":" + expiresAt.ToUnixTimeSeconds() +
                   ",\"user\":{\"id\":\"" + UserId.ToString("D") + "\"}}";
        }

        private static string MemberJson(string displayName)
        {
            return "{\"id\":\"" + UserId.ToString("D") +
                   "\",\"team_id\":\"" + TeamId.ToString("D") +
                   "\",\"display_name\":\"" + displayName +
                   "\",\"normalized_name\":\"" + displayName.ToLowerInvariant() +
                   "\",\"sort_order\":0}";
        }

        private sealed class FixedClock : ISupabaseClock
        {
            public DateTimeOffset UtcNow => Now;
        }

        private sealed class MemorySessionStore : ISupabaseAuthSessionStore
        {
            public SupabaseAuthSession Saved { get; set; }

            public int DeleteCount { get; private set; }

            public bool TryLoad(out SupabaseAuthSession session)
            {
                session = Saved;
                return session != null;
            }

            public void Save(SupabaseAuthSession session)
            {
                Saved = session;
            }

            public void Delete()
            {
                DeleteCount++;
                Saved = null;
            }
        }

        private sealed class QueueTransport : ISupabaseHttpTransport
        {
            private readonly Queue<SupabaseHttpResponse> _responses;

            public QueueTransport(params SupabaseHttpResponse[] responses)
            {
                _responses = new Queue<SupabaseHttpResponse>(responses);
            }

            public List<SupabaseHttpRequest> Requests { get; } =
                new List<SupabaseHttpRequest>();

            public Task<SupabaseHttpResponse> SendAsync(
                SupabaseHttpRequest request,
                CancellationToken cancellationToken)
            {
                Requests.Add(request);
                if (_responses.Count == 0)
                {
                    throw new InvalidOperationException("No fake response was queued.");
                }

                return Task.FromResult(_responses.Dequeue());
            }
        }
    }
}
