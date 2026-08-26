using System;
using System.Globalization;
using System.Text;

namespace TeamOverlay.Identity
{
    public enum DisplayNameValidationError
    {
        None = 0,
        Required = 1,
        TooLong = 2,
        UnsupportedCharacter = 3,
        LetterOrNumberRequired = 4
    }

    /// <summary>
    /// Result of canonicalizing a user-entered display name. DisplayName is safe
    /// for presentation, while UniqueNameKey is the ordinal key that the server
    /// should protect with a unique constraint.
    /// </summary>
    public sealed class DisplayNameValidationResult
    {
        internal DisplayNameValidationResult(
            DisplayNameValidationError error,
            string displayName,
            string uniqueNameKey)
        {
            Error = error;
            DisplayName = displayName ?? string.Empty;
            UniqueNameKey = uniqueNameKey ?? string.Empty;
        }

        public bool IsValid => Error == DisplayNameValidationError.None;

        public DisplayNameValidationError Error { get; }

        public string DisplayName { get; }

        public string UniqueNameKey { get; }
    }

    /// <summary>
    /// Shared client-side name policy. The backend must apply the same canonical
    /// key and enforce uniqueness transactionally; local validation alone cannot
    /// prove that a name is globally unique.
    /// </summary>
    public static class DisplayNamePolicy
    {
        public const int MaximumTextElements = 16;

        public static DisplayNameValidationResult Validate(string rawDisplayName)
        {
            if (string.IsNullOrWhiteSpace(rawDisplayName))
            {
                return Invalid(DisplayNameValidationError.Required);
            }

            string unicodeNormalized;
            try
            {
                unicodeNormalized = rawDisplayName.Normalize(NormalizationForm.FormKC);
            }
            catch (ArgumentException)
            {
                return Invalid(DisplayNameValidationError.UnsupportedCharacter);
            }

            var builder = new StringBuilder(unicodeNormalized.Length);
            var previousWasSpace = false;
            var hasLetterOrNumber = false;

            for (var index = 0; index < unicodeNormalized.Length;)
            {
                var current = unicodeNormalized[index];
                if (char.IsWhiteSpace(current))
                {
                    if (builder.Length > 0)
                    {
                        previousWasSpace = true;
                    }

                    index++;
                    continue;
                }

                if (previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = false;
                }

                var category = CharUnicodeInfo.GetUnicodeCategory(unicodeNormalized, index);
                if (!IsAllowedCategory(category))
                {
                    return Invalid(DisplayNameValidationError.UnsupportedCharacter);
                }

                hasLetterOrNumber |= IsLetterOrNumber(category);
                if (char.IsHighSurrogate(current)
                    && index + 1 < unicodeNormalized.Length
                    && char.IsLowSurrogate(unicodeNormalized[index + 1]))
                {
                    builder.Append(current);
                    builder.Append(unicodeNormalized[index + 1]);
                    index += 2;
                }
                else
                {
                    if (char.IsSurrogate(current))
                    {
                        return Invalid(DisplayNameValidationError.UnsupportedCharacter);
                    }

                    builder.Append(current);
                    index++;
                }
            }

            var displayName = builder.ToString();
            if (displayName.Length == 0)
            {
                return Invalid(DisplayNameValidationError.Required);
            }

            if (!hasLetterOrNumber)
            {
                return Invalid(DisplayNameValidationError.LetterOrNumberRequired);
            }

            if (StringInfo.ParseCombiningCharacters(displayName).Length > MaximumTextElements)
            {
                return Invalid(DisplayNameValidationError.TooLong);
            }

            var uniqueNameKey = displayName
                .ToLowerInvariant()
                .Normalize(NormalizationForm.FormKC);
            return new DisplayNameValidationResult(
                DisplayNameValidationError.None,
                displayName,
                uniqueNameKey);
        }

        private static DisplayNameValidationResult Invalid(DisplayNameValidationError error)
        {
            return new DisplayNameValidationResult(error, string.Empty, string.Empty);
        }

        private static bool IsAllowedCategory(UnicodeCategory category)
        {
            switch (category)
            {
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.ConnectorPunctuation:
                case UnicodeCategory.DashPunctuation:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsLetterOrNumber(UnicodeCategory category)
        {
            return category == UnicodeCategory.UppercaseLetter
                || category == UnicodeCategory.LowercaseLetter
                || category == UnicodeCategory.TitlecaseLetter
                || category == UnicodeCategory.ModifierLetter
                || category == UnicodeCategory.OtherLetter
                || category == UnicodeCategory.DecimalDigitNumber;
        }
    }
}
