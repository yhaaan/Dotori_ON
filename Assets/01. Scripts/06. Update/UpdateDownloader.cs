using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace DOTORION.Update
{
    /// <summary>
    /// Fetches the manifest and the release zip.
    ///
    /// The download is written straight to disk rather than held in memory - the
    /// zip is around forty megabytes, and an overlay that briefly doubles its own
    /// footprint to install an update is a poor trade.
    /// </summary>
    public sealed class UpdateDownloader : IDisposable
    {
        private readonly HttpClient _client;

        public UpdateDownloader()
        {
            _client = new HttpClient
            {
                // Generous: this runs unattended in the background and the file
                // is large. The person is not waiting on the manifest fetch.
                Timeout = TimeSpan.FromMinutes(10)
            };
        }

        /// <summary>
        /// Reads the manifest. Returns null for anything that is not a manifest
        /// this build is willing to act on - unreachable, unparsable, or naming
        /// a download outside this project's releases.
        /// </summary>
        public async Task<UpdateManifest> FetchManifestAsync(CancellationToken cancellationToken)
        {
            using (var response = await _client.GetAsync(UpdateEndpoints.ManifestUrl, cancellationToken))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                return UpdateManifest.TryParse(json, out var manifest) ? manifest : null;
            }
        }

        /// <summary>
        /// Downloads the zip into <paramref name="folder"/> and checks it against
        /// the manifest's hash before handing back the path.
        ///
        /// A file that does not match is deleted rather than returned: the next
        /// thing that happens to it is being unpacked over the running install,
        /// so "probably fine" is not good enough, and a half-finished download
        /// left on disk would be picked up as if it were whole.
        /// </summary>
        /// <returns>The zip's path.</returns>
        /// <exception cref="InvalidDataException">The download did not match the manifest hash.</exception>
        public async Task<string> DownloadAsync(
            UpdateManifest manifest,
            string folder,
            IProgress<float> progress,
            CancellationToken cancellationToken)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (!UpdateEndpoints.IsTrustedDownload(manifest.download))
            {
                throw new InvalidDataException("The manifest names a download outside this project's releases.");
            }

            Directory.CreateDirectory(folder);
            var zipPath = Path.Combine(folder, "DOTORI_ON-" + manifest.version + ".zip");

            using (var response = await _client.GetAsync(
                manifest.download, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                // Content-Length is absent on a chunked response; progress then
                // stays at zero rather than reporting a made-up fraction.
                var total = response.Content.Headers.ContentLength ?? 0L;
                var read = 0L;

                using (var source = await response.Content.ReadAsStreamAsync())
                using (var destination = new FileStream(
                    zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = await source.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await destination.WriteAsync(buffer, 0, count, cancellationToken);
                        read += count;
                        if (total > 0)
                        {
                            progress?.Report((float)((double)read / total));
                        }
                    }
                }
            }

            var actual = Sha256(zipPath);
            if (!string.Equals(actual, manifest.sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(zipPath);
                throw new InvalidDataException(
                    "The download did not match the manifest hash (expected " +
                    manifest.sha256 + ", got " + actual + ").");
            }

            progress?.Report(1f);
            return zipPath;
        }

        public void Dispose() => _client.Dispose();

        private static string Sha256(string path)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                var hash = algorithm.ComputeHash(stream);
                var text = new System.Text.StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    text.Append(b.ToString("x2"));
                }

                return text.ToString();
            }
        }
    }
}
