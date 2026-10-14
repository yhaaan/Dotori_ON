using System;
using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Core
{
    /// <summary>
    /// A profile photo of the member's own, as opposed to a catalog icon. The
    /// picture itself is stored once on the server; the roster only carries
    /// <see cref="MemberState.AvatarKey"/> = <c>photo.&lt;version&gt;</c>, so a
    /// client fetches the bytes only when it meets a version it has not cached.
    /// </summary>
    public interface ITeamAvatarPhotos
    {
        /// <summary>
        /// Stores <paramref name="png"/> as the local member's photo and switches
        /// their avatar to it. The PNG must be exactly
        /// <see cref="AvatarPhotoKey.Size"/> square; the server rejects anything else.
        /// </summary>
        Task UploadAvatarPhotoAsync(byte[] png, CancellationToken cancellationToken);

        /// <summary>The member's current photo, or null when they never uploaded one.</summary>
        Task<AvatarPhoto> GetAvatarPhotoAsync(string memberId, CancellationToken cancellationToken);
    }

    public sealed class AvatarPhoto
    {
        public AvatarPhoto(int version, byte[] png)
        {
            Version = version;
            Png = png ?? throw new ArgumentNullException(nameof(png));
        }

        public int Version { get; }

        public byte[] Png { get; }
    }

    /// <summary>How a photo is named in <see cref="MemberState.AvatarKey"/>.</summary>
    public static class AvatarPhotoKey
    {
        public const string Prefix = "photo.";

        /// <summary>Width and height of the stored PNG. The server checks it too.</summary>
        public const int Size = 64;

        public static string For(int version) => Prefix + version;

        /// <summary>The version a photo key names, or false for a catalog key.</summary>
        public static bool TryParse(string avatarKey, out int version)
        {
            version = 0;
            return avatarKey != null
                && avatarKey.StartsWith(Prefix, StringComparison.Ordinal)
                && int.TryParse(avatarKey.Substring(Prefix.Length), out version)
                && version > 0;
        }
    }
}
