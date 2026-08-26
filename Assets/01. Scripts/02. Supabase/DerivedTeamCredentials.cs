using System;
using System.Security.Cryptography;
using System.Text;
using TeamOverlay.Identity;

namespace TeamOverlay.Supabase
{
    /// <summary>
    /// Turns a visible member name into the Auth credentials for that member.
    ///
    /// The team asked for the name itself to be the account: typing "길동" on any
    /// PC signs in as 길동, with no password to remember. Deriving the credentials
    /// from the name keeps <c>members.id = auth.uid()</c> intact while making the
    /// identity portable, which an anonymous user per device can never be.
    ///
    /// This is deliberately not a security boundary. Anyone holding the
    /// application can compute any teammate's credentials from their name, so it
    /// suits an internal four-person tool and nothing wider.
    /// </summary>
    public static class DerivedTeamCredentials
    {
        // Reserved TLD from RFC 2606: guaranteed never to resolve, so a stray
        // password-recovery mail can never reach a real inbox.
        private const string EmailDomain = "teamoverlay.invalid";

        // Only namespaces the digest so the password is not the same value as the
        // email local part. It is not secret; it ships inside the build.
        private const string PasswordNamespace = "ProjectDDD.TeamOverlay.v1";

        public static string EmailFor(string uniqueNameKey)
        {
            // Korean names cannot go in an email local part, and hashing also
            // keeps the address a fixed, always-valid length.
            return "m" + Digest(uniqueNameKey).Substring(0, 32) + "@" + EmailDomain;
        }

        public static string PasswordFor(string uniqueNameKey)
        {
            return Digest(PasswordNamespace + "|" + uniqueNameKey);
        }

        private static string Digest(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("A unique name key is required.", nameof(value));
            }

            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value.Normalize(NormalizationForm.FormKC)));
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>Convenience overload for an already validated name.</summary>
        public static string EmailFor(DisplayNameValidationResult validation)
        {
            return EmailFor(RequireValid(validation).UniqueNameKey);
        }

        public static string PasswordFor(DisplayNameValidationResult validation)
        {
            return PasswordFor(RequireValid(validation).UniqueNameKey);
        }

        private static DisplayNameValidationResult RequireValid(DisplayNameValidationResult validation)
        {
            if (validation == null)
            {
                throw new ArgumentNullException(nameof(validation));
            }

            if (!validation.IsValid)
            {
                throw new ArgumentException("The display name is invalid.", nameof(validation));
            }

            return validation;
        }
    }
}
