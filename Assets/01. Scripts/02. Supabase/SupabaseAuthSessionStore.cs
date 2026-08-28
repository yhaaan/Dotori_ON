using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace DOTORION.Supabase
{
    public interface ISupabaseAuthSessionStore
    {
        bool TryLoad(out SupabaseAuthSession session);

        void Save(SupabaseAuthSession session);

        void Delete();
    }

    public sealed class WindowsCredentialSupabaseAuthSessionStore : ISupabaseAuthSessionStore
    {
        private const uint GenericCredential = 1;
        private const uint LocalMachinePersistence = 2;
        private readonly string _targetName;

        public WindowsCredentialSupabaseAuthSessionStore(string targetName)
        {
            _targetName = !string.IsNullOrWhiteSpace(targetName)
                ? targetName
                : throw new ArgumentException("A credential target is required.", nameof(targetName));
        }

        public bool TryLoad(out SupabaseAuthSession session)
        {
            session = null;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!CredRead(_targetName, GenericCredential, 0, out var credentialPointer))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 1168)
                {
                    return false;
                }

                throw new InvalidOperationException(
                    "Windows 자격 증명 관리자에서 로그인 세션을 읽지 못했습니다. 오류 " + error + ".");
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                {
                    throw new InvalidOperationException("저장된 Supabase 로그인 세션이 비어 있습니다.");
                }

                var serialized = Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    checked((int)credential.CredentialBlobSize / 2));
                if (!SessionDocument.TryDeserialize(serialized, out session))
                {
                    throw new InvalidOperationException("저장된 Supabase 로그인 세션이 손상되었습니다.");
                }

                return true;
            }
            finally
            {
                CredFree(credentialPointer);
            }
#else
            throw new PlatformNotSupportedException("DOTORI ON secure Auth storage currently requires Windows.");
#endif
        }

        public void Save(SupabaseAuthSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var serialized = SessionDocument.Serialize(session);
            var bytes = Encoding.Unicode.GetBytes(serialized);
            var blob = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blob, bytes.Length);
                var credential = new NativeCredential
                {
                    Type = GenericCredential,
                    TargetName = _targetName,
                    CredentialBlobSize = checked((uint)bytes.Length),
                    CredentialBlob = blob,
                    Persist = LocalMachinePersistence,
                    UserName = "Supabase anonymous Auth session"
                };

                if (!CredWrite(ref credential, 0))
                {
                    throw new InvalidOperationException(
                        "Windows 자격 증명 관리자에 로그인 세션을 저장하지 못했습니다. 오류 " +
                        Marshal.GetLastWin32Error() + ".");
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(blob);
            }
#else
            throw new PlatformNotSupportedException("DOTORI ON secure Auth storage currently requires Windows.");
#endif
        }

        public void Delete()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!CredDelete(_targetName, GenericCredential, 0))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 1168)
                {
                    throw new InvalidOperationException(
                        "Windows 자격 증명 관리자에서 로그인 세션을 삭제하지 못했습니다. 오류 " + error + ".");
                }
            }
#else
            throw new PlatformNotSupportedException("DOTORI ON secure Auth storage currently requires Windows.");
#endif
        }

        [Serializable]
        private sealed class SessionDocument
        {
            public int schemaVersion;
            public string userId;
            public string accessToken;
            public string refreshToken;
            public string expiresAtUtc;

            public static string Serialize(SupabaseAuthSession session)
            {
                return JsonUtility.ToJson(new SessionDocument
                {
                    schemaVersion = 1,
                    userId = session.UserId.ToString("D"),
                    accessToken = session.AccessToken,
                    refreshToken = session.RefreshToken,
                    expiresAtUtc = session.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture)
                });
            }

            public static bool TryDeserialize(string serialized, out SupabaseAuthSession session)
            {
                session = null;
                SessionDocument document;
                try
                {
                    document = JsonUtility.FromJson<SessionDocument>(serialized);
                }
                catch (ArgumentException)
                {
                    return false;
                }

                if (document == null || document.schemaVersion != 1
                    || !Guid.TryParse(document.userId, out var userId)
                    || userId == Guid.Empty
                    || string.IsNullOrWhiteSpace(document.accessToken)
                    || string.IsNullOrWhiteSpace(document.refreshToken)
                    || !DateTimeOffset.TryParseExact(
                        document.expiresAtUtc,
                        "O",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var expiresAtUtc))
                {
                    return false;
                }

                session = new SupabaseAuthSession(
                    userId,
                    document.accessToken,
                    document.refreshToken,
                    expiresAtUtc);
                return true;
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }

        [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(ref NativeCredential credential, uint flags);

        [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("advapi32.dll", SetLastError = false)]
        private static extern void CredFree(IntPtr buffer);
#endif
    }
}
