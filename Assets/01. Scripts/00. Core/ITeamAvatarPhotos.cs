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
        /// their avatar to it. The PNG must be <see cref="AvatarPhotoKey.Size"/>
        /// square (the server also still takes 64x64 from older clients) and is
        /// rejected otherwise.
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

        /// <summary>
        /// Width and height of the stored PNG. The card draws the avatar at 48
        /// units, which is 48 / 72 / 96 pixels at the 100 / 150 / 200 % UI
        /// scales, so 96 is drawn 1:1 at the largest and halved exactly at the
        /// smallest. The server checks it too.
        /// </summary>
        public const int Size = 96;

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
