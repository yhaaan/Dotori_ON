using NUnit.Framework;
using DOTORION.Update;

namespace DOTORION.Tests.EditMode
{
    public sealed class SemanticVersionTests
    {
        [Test]
        public void Compare_OrdersByNumberNotByText()
        {
            // The whole point of the type: as strings, "0.10.0" sorts below
            // "0.9.0", which would offer an update backwards.
            Assert.That(Parse("0.10.0") > Parse("0.9.0"), Is.True);
            Assert.That(Parse("1.0.0") > Parse("0.99.99"), Is.True);
            Assert.That(Parse("0.8.1") > Parse("0.8.0"), Is.True);
        }

        [Test]
        public void Parse_TakesTheVATagCarries()
        {
            Assert.That(Parse("v1.2.3"), Is.EqualTo(new SemanticVersion(1, 2, 3)));
        }

        [Test]
        public void Parse_RefusesAnythingItWouldHaveToGuessAt()
        {
            foreach (var text in new[] { null, "", "  ", "1.2", "1.2.3.4", "1.2.x", "-1.2.3", "1.2.+3" })
            {
                Assert.That(SemanticVersion.TryParse(text, out _), Is.False, "accepted: " + (text ?? "null"));
            }
        }

        private static SemanticVersion Parse(string text)
        {
            Assert.That(SemanticVersion.TryParse(text, out var version), Is.True, "could not parse " + text);
            return version;
        }
    }

    public sealed class UpdateManifestTests
    {
        private const string Sha = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        private static string Json(string version, string download, string sha256)
        {
            return "{\"version\":\"" + version + "\",\"download\":\"" + download +
                   "\",\"sha256\":\"" + sha256 + "\",\"notes\":\"n\"}";
        }

        private static string TrustedUrl => UpdateEndpoints.ReleasesRoot + "latest/download/DOTORI_ON.zip";

        [Test]
        public void Parse_ReadsAWellFormedManifest()
        {
            Assert.That(UpdateManifest.TryParse(Json("1.0.0", TrustedUrl, Sha), out var manifest), Is.True);
            Assert.That(manifest.version, Is.EqualTo("1.0.0"));
            Assert.That(manifest.download, Is.EqualTo(TrustedUrl));
        }

        [Test]
        public void Parse_RefusesADownloadThatIsNotOurRelease()
        {
            // The zip is unpacked over the running install, so the one thing the
            // manifest must never be able to do is send the updater elsewhere.
            Assert.That(
                UpdateManifest.TryParse(Json("1.0.0", "https://evil.example/DOTORI_ON.zip", Sha), out _),
                Is.False);
            Assert.That(
                UpdateManifest.TryParse(Json("1.0.0", "http://github.com/yhaaan/Dotori_ON/releases/x", Sha), out _),
                Is.False);
        }

        [Test]
        public void Parse_RefusesAMissingOrMalformedHash()
        {
            Assert.That(UpdateManifest.TryParse(Json("1.0.0", TrustedUrl, ""), out _), Is.False);
            Assert.That(UpdateManifest.TryParse(Json("1.0.0", TrustedUrl, "abc"), out _), Is.False);
            Assert.That(UpdateManifest.TryParse(Json("1.0.0", TrustedUrl, new string('z', 64)), out _), Is.False);
        }

        [Test]
        public void Parse_RefusesRubbish()
        {
            Assert.That(UpdateManifest.TryParse(null, out _), Is.False);
            Assert.That(UpdateManifest.TryParse("", out _), Is.False);
            Assert.That(UpdateManifest.TryParse("not json at all", out _), Is.False);
        }
    }

    public sealed class UpdateCheckTests
    {
        private static UpdateManifest Manifest(string version)
        {
            return new UpdateManifest
            {
                version = version,
                download = UpdateEndpoints.ReleasesRoot + "latest/download/DOTORI_ON.zip",
                sha256 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                notes = UpdateEndpoints.LatestReleasePage
            };
        }

        [Test]
        public void ANewerReleaseIsOffered()
        {
            Assert.That(
                UpdateCheck.Evaluate("0.8.0", Manifest("1.0.0"), out var offered),
                Is.EqualTo(UpdateAvailability.Available));
            Assert.That(offered.ToString(), Is.EqualTo("1.0.0"));
        }

        [Test]
        public void TheSameVersionIsNotOffered()
        {
            Assert.That(
                UpdateCheck.Evaluate("1.0.0", Manifest("1.0.0"), out _),
                Is.EqualTo(UpdateAvailability.UpToDate));
        }

        [Test]
        public void ABuildAheadOfTheLastReleaseIsNotDowngraded()
        {
            // A build run straight from the Editor's output folder is routinely
            // ahead of the newest release. Offering to "update" it would replace
            // work that was never released.
            Assert.That(
                UpdateCheck.Evaluate("1.1.0", Manifest("1.0.0"), out _),
                Is.EqualTo(UpdateAvailability.UpToDate));
        }

        [Test]
        public void NothingIsOfferedWhenTheCheckCannotConclude()
        {
            Assert.That(
                UpdateCheck.Evaluate("0.8", Manifest("1.0.0"), out _),
                Is.EqualTo(UpdateAvailability.Unknown));
            Assert.That(
                UpdateCheck.Evaluate("0.8.0", null, out _),
                Is.EqualTo(UpdateAvailability.Unknown));

            var untrusted = Manifest("1.0.0");
            untrusted.download = "https://evil.example/x.zip";
            Assert.That(
                UpdateCheck.Evaluate("0.8.0", untrusted, out _),
                Is.EqualTo(UpdateAvailability.Unknown));
        }
    }
}
