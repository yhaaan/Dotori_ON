namespace DOTORION.Update
{
    /// <summary>What the version check concluded.</summary>
    public enum UpdateAvailability
    {
        /// <summary>Nothing to offer: the running build is the newest, or newer.</summary>
        UpToDate,

        /// <summary>A newer release exists and can be installed.</summary>
        Available,

        /// <summary>
        /// The check could not reach a conclusion - no manifest, an unreadable
        /// one, or a running version that does not parse. Nothing is offered and
        /// nothing is said; a person who cannot reach GitHub does not need a
        /// error about it every time the app starts.
        /// </summary>
        Unknown
    }

    /// <summary>
    /// Decides whether to offer an update. Pure, so the awkward cases - the same
    /// version, a downgrade, a manifest that arrived broken - are pinned by tests
    /// rather than discovered on somebody's machine.
    /// </summary>
    public static class UpdateCheck
    {
        public static UpdateAvailability Evaluate(
            string runningVersion,
            UpdateManifest manifest,
            out SemanticVersion offered)
        {
            offered = default;

            if (!SemanticVersion.TryParse(runningVersion, out var current))
            {
                return UpdateAvailability.Unknown;
            }

            if (manifest == null || !manifest.IsUsable())
            {
                return UpdateAvailability.Unknown;
            }

            if (!SemanticVersion.TryParse(manifest.version, out var latest))
            {
                return UpdateAvailability.Unknown;
            }

            offered = latest;

            // Equal is the common case and older is the interesting one: a build
            // run from a developer's own folder can be ahead of the last release,
            // and offering to "update" it downwards would replace their work.
            return latest > current
                ? UpdateAvailability.Available
                : UpdateAvailability.UpToDate;
        }
    }
}
