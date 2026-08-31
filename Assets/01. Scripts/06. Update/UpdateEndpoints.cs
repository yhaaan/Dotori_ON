using System;

namespace DOTORION.Update
{
    /// <summary>
    /// The one place the release URLs are written down. Both of them resolve to
    /// whatever the newest release is, so a build never has to be told where the
    /// next one will live.
    /// </summary>
    public static class UpdateEndpoints
    {
        /// <summary>
        /// Every release asset lives under this. Nothing outside it is ever
        /// downloaded, which is what keeps a mistyped or altered manifest from
        /// pointing the updater at some other host.
        /// </summary>
        public const string ReleasesRoot = "https://github.com/yhaaan/Dotori_ON/releases/";

        /// <summary>
        /// The manifest, uploaded beside the zip by <c>Tools/release.ps1</c>.
        /// </summary>
        public const string ManifestUrl = ReleasesRoot + "latest/download/version.json";

        /// <summary>The release page, for the person who would rather read first.</summary>
        public const string LatestReleasePage = ReleasesRoot + "latest";

        /// <summary>
        /// Whether a URL out of the manifest may be downloaded. https only, and
        /// only from this project's releases - a plain prefix test, because
        /// anything cleverer is a way to be talked into an exception.
        /// </summary>
        public static bool IsTrustedDownload(string url)
        {
            return !string.IsNullOrWhiteSpace(url) &&
                   url.StartsWith(ReleasesRoot, StringComparison.Ordinal);
        }
    }
}
