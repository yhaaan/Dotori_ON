using System;
using NUnit.Framework;
using TeamOverlay.Supabase;

namespace TeamOverlay.Tests.EditMode
{
    public sealed class WindowsCredentialSupabaseAuthSessionStoreTests
    {
        [Test]
        public void SaveLoadDelete_RoundTripsSessionInWindowsCredentialManager()
        {
            var target = "ProjectDDD.TeamOverlay.Tests." + Guid.NewGuid().ToString("N");
            var store = new WindowsCredentialSupabaseAuthSessionStore(target);
            var expected = new SupabaseAuthSession(
                new Guid("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"),
                "test-access-token",
                "test-refresh-token",
                new DateTimeOffset(2026, 8, 26, 3, 4, 5, TimeSpan.Zero));

            try
            {
                store.Save(expected);

                Assert.That(store.TryLoad(out var actual), Is.True);
                Assert.That(actual.UserId, Is.EqualTo(expected.UserId));
                Assert.That(actual.AccessToken, Is.EqualTo(expected.AccessToken));
                Assert.That(actual.RefreshToken, Is.EqualTo(expected.RefreshToken));
                Assert.That(actual.ExpiresAtUtc, Is.EqualTo(expected.ExpiresAtUtc));
            }
            finally
            {
                store.Delete();
            }

            Assert.That(store.TryLoad(out _), Is.False);
        }
    }
}
