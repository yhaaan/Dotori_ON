using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DOTORION.Identity;
using UnityEngine;

namespace DOTORION.Supabase
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

        /// <summary>
        /// Signs in as the account the name itself designates, creating it on first
        /// use. The same name resolves to the same Auth user on every machine, so a
        /// member is no longer tied to the PC that first claimed the name.
        /// </summary>
        public async Task<SupabaseIdentityBootstrap> InitializeForNameAsync(
            DisplayNameValidationResult validation,
            CancellationToken cancellationToken)
        {
            if (validation == null || !validation.IsValid)
            {
                throw new ArgumentException("A valid display name is required.", nameof(validation));
            }

            var createdUser = false;
            if (_sessionStore.TryLoad(out var storedSession))
            {
                _session = storedSession;
                if (ShouldRefresh(_session))
                {
                    try
                    {
                        _session = await RefreshStoredSessionAsync(_session, cancellationToken);
                    }
                    catch (SupabaseIdentityRecoveryException)
                    {
                        // A dead refresh token used to be unrecoverable because the
                        // anonymous account had no other way in. The credentials are
                        // derived from the name now, so signing in again is safe and
                        // lands on the very same account - or, if that account has
                        // been deleted since, makes it again.
                        var recovered = await SignInOrSignUpWithNameAsync(validation, cancellationToken);
                        _session = recovered.Session;
                        createdUser = recovered.CreatedUser;
                    }
                }
            }
            else
            {
                var signIn = await SignInOrSignUpWithNameAsync(validation, cancellationToken);
                _session = signIn.Session;
                createdUser = signIn.CreatedUser;
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

            return new SupabaseIdentityBootstrap(member, createdUser);
        }

        private async Task<NameSignInResult> SignInOrSignUpWithNameAsync(
            DisplayNameValidationResult validation,
            CancellationToken cancellationToken)
        {
            try
            {
                return new NameSignInResult(
                    await SignInWithNameAsync(validation, cancellationToken),
                    false);
            }
            catch (SupabaseApiException exception) when (IsUnknownAccount(exception))
            {
                // Nobody has used this name yet, so this launch is the one that
                // creates the account for it.
                return new NameSignInResult(
                    await SignUpWithNameAsync(validation, cancellationToken),
                    true);
            }
        }

        private async Task<SupabaseAuthSession> SignInWithNameAsync(
            DisplayNameValidationResult validation,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new PasswordGrantRequest
            {
                email = DerivedTeamCredentials.EmailFor(validation),
                password = DerivedTeamCredentials.PasswordFor(validation)
            });
            var response = await _transport.SendAsync(
                CreatePublicRequest(
                    "POST",
                    _projectUrl + "/auth/v1/token?grant_type=password",
                    body),
                cancellationToken);
            EnsureSuccess(response);

            var session = ParseAuthResponse(response.Body, Guid.Empty);
            _sessionStore.Save(session);
            return session;
        }

        private async Task<SupabaseAuthSession> SignUpWithNameAsync(
            DisplayNameValidationResult validation,
            CancellationToken cancellationToken)
        {
            var body = JsonUtility.ToJson(new PasswordGrantRequest
            {
                email = DerivedTeamCredentials.EmailFor(validation),
                password = DerivedTeamCredentials.PasswordFor(validation)
            });
            var response = await _transport.SendAsync(
                CreatePublicRequest(
                    "POST",
                    _projectUrl + "/auth/v1/signup",
                    body),
                cancellationToken);
            EnsureSuccess(response);

            SupabaseAuthSession session;
            try
            {
                session = ParseAuthResponse(response.Body, Guid.Empty);
            }
            catch (InvalidOperationException exception)
            {
                // Signup returns a user without a session while "Confirm email" is
                // on, which no derived address can ever receive.
                throw new SupabaseIdentityRecoveryException(
                    "Supabase 프로젝트에서 이메일 확인이 켜져 있어 이름으로 계정을 만들 수 없습니다. " +
                    "Authentication 설정에서 Confirm email을 꺼주세요.",
                    exception);
            }

            _sessionStore.Save(session);
            return session;
        }

        /// <summary>
        /// Whether the failure says the Auth user behind the token is gone.
        ///
        /// The only foreign key a name claim can break is members.id, which
        /// points at auth.users - and it can only break when the token names a
        /// user that has been deleted. claim_member_name raises 23503 itself for
        /// a missing team, with a stable machine-readable message, so that one
        /// case is excluded by name rather than guessed at from prose.
        /// </summary>
        private static bool IsDeletedAccount(SupabaseApiException exception)
        {
            return exception.ErrorCode == "23503"
                   && exception.ServerMessage != "team_not_found";
        }

        private static bool IsUnknownAccount(SupabaseApiException exception)
        {
            return exception.StatusCode == 400
                   && (exception.ErrorCode == "invalid_credentials"
                       || exception.ServerMessage == "Invalid login credentials");
        }

        private sealed class NameSignInResult
        {
            public NameSignInResult(SupabaseAuthSession session, bool createdUser)
            {
                Session = session;
                CreatedUser = createdUser;
            }

            public SupabaseAuthSession Session { get; }

            public bool CreatedUser { get; }
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
            var url = _projectUrl + "/rest/v1/rpc/claim_member_name";
            var response = await _transport.SendAsync(
                CreateAuthorizedRequest("POST", url, requestBody, session),
                cancellationToken);

            try
            {
                EnsureSuccess(response);
            }
            catch (SupabaseApiException exception) when (IsDeletedAccount(exception))
            {
                // Nothing rejected the token on the way here: it is signed and
                // still in date, so PostgREST took it and auth.uid() named a user
                // row that is no longer there. Only the insert notices, as a
                // foreign key on members.id.
                //
                // A session pointing at a deleted account cannot be refreshed
                // back to life, so it is thrown away and the name claimed from
                // scratch - signing in if somebody has already remade the
                // account for it, signing up if not.
                _sessionStore.Delete();
                var restarted = await SignInOrSignUpWithNameAsync(validation, cancellationToken);
                _session = restarted.Session;
                response = await _transport.SendAsync(
                    CreateAuthorizedRequest("POST", url, requestBody, _session),
                    cancellationToken);
                EnsureSuccess(response);
            }

            return ParseMember(response.Body);
        }

        private bool ShouldRefresh(SupabaseAuthSession session)
        {
            return session.ExpiresAtUtc <= _clock.UtcNow.Add(RefreshWindow);
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
                      "&select=id,team_id,display_name,sort_order&limit=1";
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
        private sealed class PasswordGrantRequest
        {
            public string email;
            public string password;
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
            public int sort_order;
        }
    }
}
