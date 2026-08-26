using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamOverlay.Identity;
using UnityEngine;

namespace TeamOverlay.Supabase
{
    public sealed class SupabaseIdentityClient : ISupabaseSessionProvider
    {
        private static readonly TimeSpan RefreshWindow = TimeSpan.FromMinutes(2);
        private readonly string _projectUrl;
        private readonly string _publishableKey;
        private readonly ISupabaseHttpTransport _transport;
        private readonly ISupabaseAuthSessionStore _sessionStore;
        private readonly ISupabaseClock _clock;
        private SupabaseAuthSession _session;

        public SupabaseIdentityClient(
            string projectUrl,
            string publishableKey,
            ISupabaseHttpTransport transport,
            ISupabaseAuthSessionStore sessionStore)
            : this(projectUrl, publishableKey, transport, sessionStore, new SystemSupabaseClock())
        {
        }

        public SupabaseIdentityClient(
            string projectUrl,
            string publishableKey,
            ISupabaseHttpTransport transport,
            ISupabaseAuthSessionStore sessionStore,
            ISupabaseClock clock)
        {
            _projectUrl = (projectUrl ?? string.Empty).TrimEnd('/');
            _publishableKey = !string.IsNullOrWhiteSpace(publishableKey)
                ? publishableKey
                : throw new ArgumentException("A Supabase publishable key is required.", nameof(publishableKey));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _sessionStore = sessionStore ?? throw new ArgumentNullException(nameof(sessionStore));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));

            if (!Uri.TryCreate(_projectUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException("A valid Supabase project URL is required.", nameof(projectUrl));
            }
        }

        public SupabaseAuthSession CurrentSession => _session;

        public async Task<SupabaseIdentityBootstrap> InitializeAsync(
            CancellationToken cancellationToken)
        {
            var createdAnonymousUser = false;
            if (_sessionStore.TryLoad(out var storedSession))
            {
                _session = storedSession;
                if (ShouldRefresh(_session))
                {
                    _session = await RefreshStoredSessionAsync(_session, cancellationToken);
                }
            }
            else
            {
                _session = await SignInAnonymouslyAsync(cancellationToken);
                createdAnonymousUser = true;
            }

            SupabaseMemberRecord member;
            try
            {
                member = await GetCurrentMemberAsync(_session, cancellationToken);
            }
            catch (SupabaseApiException exception) when (exception.StatusCode == 401)
            {
                _session = await RefreshStoredSessionAsync(_session, cancellationToken);
                member = await GetCurrentMemberAsync(_session, cancellationToken);
            }

            return new SupabaseIdentityBootstrap(_session, member, createdAnonymousUser);
        }

        /// <summary>
        /// Reads the team's slot usage without a session. Callable before signup so
        /// a full team can be reported without leaving an anonymous Auth user
        /// behind, which the client has no permission to delete afterwards.
        /// </summary>
        public async Task<SupabaseTeamCapacity> GetTeamCapacityAsync(
            CancellationToken cancellationToken)
        {
            var response = await _transport.SendAsync(
                CreatePublicRequest(
                    "POST",
                    _projectUrl + "/rest/v1/rpc/team_capacity",
                    "{}"),
                cancellationToken);
            EnsureSuccess(response);

            TeamCapacityArrayDocument document;
            try
            {
                document = JsonUtility.FromJson<TeamCapacityArrayDocument>(
                    "{\"items\":" + response.Body + "}");
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase 팀 정원 응답을 읽지 못했습니다.", exception);
            }

            if (document?.items == null || document.items.Length == 0)
            {
                throw new InvalidOperationException("Supabase 팀 정원 응답이 비어 있습니다.");
            }

            return new SupabaseTeamCapacity(document.items[0].occupied, document.items[0].capacity);
        }

        /// <summary>
        /// Returns a session whose access token is still valid, rotating it first
        /// when it is inside the refresh window. Every authorized caller goes
        /// through here so only one component ever spends a refresh token.
        /// </summary>
        public async Task<SupabaseAuthSession> GetValidSessionAsync(
            CancellationToken cancellationToken)
        {
            if (_session == null)
            {
                throw new InvalidOperationException("Supabase identity has not been initialized.");
            }

            if (ShouldRefresh(_session))
            {
                _session = await RefreshStoredSessionAsync(_session, cancellationToken);
            }

            return _session;
        }

        public async Task<SupabaseMemberRecord> ClaimMemberNameAsync(
            string rawDisplayName,
            CancellationToken cancellationToken)
        {
            var validation = DisplayNamePolicy.Validate(rawDisplayName);
            if (!validation.IsValid)
            {
                throw new ArgumentException(
                    "The display name is invalid: " + validation.Error + ".",
                    nameof(rawDisplayName));
            }

            var session = await GetValidSessionAsync(cancellationToken);
            var requestBody = JsonUtility.ToJson(new ClaimMemberRequest
            {
                p_display_name = validation.DisplayName,
                p_avatar_key = "default"
            });
            var response = await _transport.SendAsync(
                CreateAuthorizedRequest(
                    "POST",
                    _projectUrl + "/rest/v1/rpc/claim_member_name",
                    requestBody,
                    session),
                cancellationToken);

            EnsureSuccess(response);
            return ParseMember(response.Body);
        }

        private bool ShouldRefresh(SupabaseAuthSession session)
        {
            return session.ExpiresAtUtc <= _clock.UtcNow.Add(RefreshWindow);
        }

        private async Task<SupabaseAuthSession> SignInAnonymouslyAsync(
            CancellationToken cancellationToken)
        {
            var response = await _transport.SendAsync(
                CreatePublicRequest(
                    "POST",
                    _projectUrl + "/auth/v1/signup",
                    "{}"),
                cancellationToken);
            EnsureSuccess(response);

            var auth = ParseAuthResponse(response.Body, Guid.Empty);
            _sessionStore.Save(auth);
            return auth;
        }

        private async Task<SupabaseAuthSession> RefreshStoredSessionAsync(
            SupabaseAuthSession session,
            CancellationToken cancellationToken)
        {
            try
            {
                var body = JsonUtility.ToJson(new RefreshTokenRequest
                {
                    refresh_token = session.RefreshToken
                });
                var response = await _transport.SendAsync(
                    CreatePublicRequest(
                        "POST",
                        _projectUrl + "/auth/v1/token?grant_type=refresh_token",
                        body),
                    cancellationToken);
                EnsureSuccess(response);

                var refreshed = ParseAuthResponse(response.Body, session.UserId);
                if (refreshed.UserId != session.UserId)
                {
                    throw new InvalidOperationException("Refreshed Auth user did not match the stored identity.");
                }

                // Supabase refresh tokens rotate. Persist the replacement before
                // any later request so an app crash cannot strand the identity.
                _sessionStore.Save(refreshed);
                return refreshed;
            }
            catch (SupabaseApiException exception) when (
                exception.StatusCode == 400 || exception.StatusCode == 401)
            {
                throw new SupabaseIdentityRecoveryException(
                    "저장된 로그인 세션을 갱신하지 못했습니다. 새 익명 ID를 자동 생성하지 않습니다.",
                    exception);
            }
        }

        private async Task<SupabaseMemberRecord> GetCurrentMemberAsync(
            SupabaseAuthSession session,
            CancellationToken cancellationToken)
        {
            var url = _projectUrl + "/rest/v1/members?id=eq." +
                      Uri.EscapeDataString(session.UserId.ToString("D")) +
                      "&select=id,team_id,display_name,normalized_name,sort_order&limit=1";
            var response = await _transport.SendAsync(
                CreateAuthorizedRequest("GET", url, null, session),
                cancellationToken);
            EnsureSuccess(response);

            var wrapped = "{\"items\":" + response.Body + "}";
            var document = JsonUtility.FromJson<MemberArrayDocument>(wrapped);
            if (document?.items == null || document.items.Length == 0)
            {
                return null;
            }

            return ConvertMember(document.items[0]);
        }

        private SupabaseHttpRequest CreatePublicRequest(string method, string url, string body)
        {
            return new SupabaseHttpRequest(
                method,
                url,
                body,
                new Dictionary<string, string>
                {
                    { "apikey", _publishableKey }
                });
        }

        private SupabaseHttpRequest CreateAuthorizedRequest(
            string method,
            string url,
            string body,
            SupabaseAuthSession session)
        {
            return new SupabaseHttpRequest(
                method,
                url,
                body,
                new Dictionary<string, string>
                {
                    { "apikey", _publishableKey },
                    { "Authorization", "Bearer " + session.AccessToken }
                });
        }

        private SupabaseAuthSession ParseAuthResponse(string body, Guid fallbackUserId)
        {
            AuthResponse document;
            try
            {
                document = JsonUtility.FromJson<AuthResponse>(body);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase Auth 응답을 읽지 못했습니다.", exception);
            }

            var userIdText = document?.user?.id;
            var userId = Guid.TryParse(userIdText, out var parsedUserId)
                ? parsedUserId
                : fallbackUserId;
            if (document == null || userId == Guid.Empty
                || string.IsNullOrWhiteSpace(document.access_token)
                || string.IsNullOrWhiteSpace(document.refresh_token))
            {
                throw new InvalidOperationException("Supabase Auth 응답에 필수 세션 정보가 없습니다.");
            }

            var expiresAt = document.expires_at > 0
                ? DateTimeOffset.FromUnixTimeSeconds(document.expires_at)
                : _clock.UtcNow.AddSeconds(Math.Max(1, document.expires_in));
            return new SupabaseAuthSession(
                userId,
                document.access_token,
                document.refresh_token,
                expiresAt);
        }

        private static SupabaseMemberRecord ParseMember(string body)
        {
            MemberDocument document;
            try
            {
                document = JsonUtility.FromJson<MemberDocument>(body);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidOperationException("Supabase 회원 응답을 읽지 못했습니다.", exception);
            }

            return ConvertMember(document);
        }

        private static SupabaseMemberRecord ConvertMember(MemberDocument document)
        {
            if (document == null
                || !Guid.TryParse(document.id, out var id)
                || !Guid.TryParse(document.team_id, out var teamId)
                || string.IsNullOrWhiteSpace(document.display_name))
            {
                throw new InvalidOperationException("Supabase 회원 응답에 필수 정보가 없습니다.");
            }

            return new SupabaseMemberRecord(
                id,
                teamId,
                document.display_name,
                document.normalized_name,
                document.sort_order);
        }

        private static void EnsureSuccess(SupabaseHttpResponse response)
        {
            SupabaseErrors.EnsureSuccess(response);
        }

        [Serializable]
        private sealed class AuthResponse
        {
            public string access_token;
            public string refresh_token;
            public long expires_in;
            public long expires_at;
            public AuthUser user;
        }

        [Serializable]
        private sealed class AuthUser
        {
            public string id;
        }

        [Serializable]
        private sealed class RefreshTokenRequest
        {
            public string refresh_token;
        }

        [Serializable]
        private sealed class ClaimMemberRequest
        {
            public string p_display_name;
            public string p_avatar_key;
        }

        [Serializable]
        private sealed class MemberArrayDocument
        {
            public MemberDocument[] items;
        }

        [Serializable]
        private sealed class TeamCapacityArrayDocument
        {
            public TeamCapacityDocument[] items;
        }

        [Serializable]
        private sealed class TeamCapacityDocument
        {
            public int occupied;
            public int capacity;
        }

        [Serializable]
        private sealed class MemberDocument
        {
            public string id;
            public string team_id;
            public string display_name;
            public string normalized_name;
            public int sort_order;
        }
    }
}
