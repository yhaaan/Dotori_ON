using System;

namespace DOTORION.Update
{
    /// <summary>
    /// A three part version, compared number by number.
    ///
    /// The comparison is the whole reason this type exists. bundleVersion is a
    /// string, and comparing strings sorts 0.10.0 below 0.9.0 - which would
    /// offer somebody an "update" back to the version they just left.
    /// </summary>
    public readonly struct SemanticVersion : IComparable<SemanticVersion>, IEquatable<SemanticVersion>
    {
        public SemanticVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        public int Major { get; }

        public int Minor { get; }

        public int Patch { get; }

        /// <summary>
        /// Accepts "1.2.3" and the "v1.2.3" that tag names carry. Anything else -
        /// two parts, four parts, letters, a negative - is refused rather than
        /// guessed at, because every caller here would rather skip the update
        /// check than act on a version it had to invent.
        /// </summary>
        public static bool TryParse(string text, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.Trim();
            if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(1);
            }

            var parts = trimmed.Split('.');
            if (parts.Length != 3)
            {
                return false;
            }

            if (!TryParsePart(parts[0], out var major) ||
                !TryParsePart(parts[1], out var minor) ||
                !TryParsePart(parts[2], out var patch))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch);
            return true;
        }

        public int CompareTo(SemanticVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(SemanticVersion other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is SemanticVersion other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Major * 397) ^ Minor) * 397 ^ Patch;
            }
        }

        public override string ToString() => Major + "." + Minor + "." + Patch;

        public static bool operator >(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) > 0;

        public static bool operator <(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) < 0;

        public static bool operator >=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) >= 0;

        public static bool operator <=(SemanticVersion left, SemanticVersion right) => left.CompareTo(right) <= 0;

        public static bool operator ==(SemanticVersion left, SemanticVersion right) => left.Equals(right);

        public static bool operator !=(SemanticVersion left, SemanticVersion right) => !left.Equals(right);

        /// <summary>
        /// int.TryParse on its own would take "+1" and " 1", and would take a
        /// negative for the sign-carrying overloads. A version part is digits.
        /// </summary>
        private static bool TryParsePart(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(text) || text.Length > 9)
            {
                return false;
            }

            foreach (var character in text)
            {
                if (character < '0' || character > '9')
                {
                    return false;
                }

                value = (value * 10) + (character - '0');
            }

            return true;
        }
    }
}
