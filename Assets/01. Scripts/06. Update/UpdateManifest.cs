using System;
using UnityEngine;

namespace DOTORION.Update
{
    /// <summary>
    /// What the newest release says about itself. The release script writes this
    /// file and uploads it beside the zip, so the two are produced together and
    /// cannot describe different builds.
    ///
    /// <code>
    /// {
    ///   "version": "1.0.0",
    ///   "download": "https://github.com/.../releases/latest/download/DOTORI_ON.zip",
    ///   "sha256": "&lt;64 hex&gt;",
    ///   "notes": "https://github.com/.../releases/latest"
    /// }
    /// </code>
    /// </summary>
    [Serializable]
    public sealed class UpdateManifest
    {
        // Public fields in this shape because JsonUtility fills them by name.
        public string version;
        public string download;
        public string sha256;
        public string notes;

        /// <summary>
        /// Parses and checks in one step. A manifest that is missing a field, or
        /// carries a download that is not a release of this project, comes back
        /// as a failure - the caller has no safe way to act on half of one.
        /// </summary>
        public static bool TryParse(string json, out UpdateManifest manifest)
        {
            manifest = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            UpdateManifest parsed;
            try
            {
                parsed = JsonUtility.FromJson<UpdateManifest>(json);
            }
            catch (Exception)
            {
                // JsonUtility throws on malformed input rather than returning null.
                return false;
            }

            if (parsed == null || !parsed.IsUsable())
            {
                return false;
            }

            manifest = parsed;
            return true;
        }

        /// <summary>
        /// A sha256 is required, not optional. The zip is unpacked over the
        /// running install, so an interrupted or tampered download must be
        /// detectable before anything is overwritten.
        /// </summary>
        public bool IsUsable()
        {
            return SemanticVersion.TryParse(version, out _) &&
                   UpdateEndpoints.IsTrustedDownload(download) &&
                   IsSha256(sha256);
        }

        private static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64)
            {
                return false;
            }

            foreach (var character in value)
            {
                var isHex = (character >= '0' && character <= '9') ||
                            (character >= 'a' && character <= 'f') ||
                            (character >= 'A' && character <= 'F');
                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
