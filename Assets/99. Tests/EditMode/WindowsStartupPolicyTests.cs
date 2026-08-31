using System;
using NUnit.Framework;
using DOTORION.Platform.Windows;

namespace DOTORION.Tests.EditMode
{
    public sealed class WindowsStartupPolicyTests
    {
        private const string InstalledPath = @"C:\Apps\DOTORI ON\DOTORI ON.exe";

        [Test]
        public void Command_QuotesThePath()
        {
            // The executable ships with a space in its name. Windows reads an
            // unquoted command up to the first space, so without the quotes the
            // login would try to run C:\Apps\DOTORI.
            Assert.That(
                WindowsStartupPolicy.BuildCommand(InstalledPath),
                Is.EqualTo("\"" + InstalledPath + "\""));
        }

        [Test]
        public void Command_DoesNotQuoteAnAlreadyQuotedPathTwice()
        {
            Assert.That(
                WindowsStartupPolicy.BuildCommand("\"" + InstalledPath + "\""),
                Is.EqualTo("\"" + InstalledPath + "\""));
        }

        [Test]
        public void Command_RefusesAnEmptyPath()
        {
            Assert.Throws<ArgumentException>(() => WindowsStartupPolicy.BuildCommand("   "));
        }

        [Test]
        public void Matches_ReadsBackWhatItWrote()
        {
            Assert.That(
                WindowsStartupPolicy.Matches(
                    WindowsStartupPolicy.BuildCommand(InstalledPath), InstalledPath),
                Is.True);
        }

        [Test]
        public void Matches_AcceptsAnUnquotedEntry()
        {
            // Somebody may have added the entry by hand, or an older build may
            // have written it without quotes. It still points at this install.
            Assert.That(
                WindowsStartupPolicy.Matches(InstalledPath, InstalledPath),
                Is.True);
        }

        [Test]
        public void Matches_IgnoresCase()
        {
            Assert.That(
                WindowsStartupPolicy.Matches(
                    "\"c:\\apps\\dotori on\\dotori on.exe\"", InstalledPath),
                Is.True);
        }

        [Test]
        public void Matches_TreatsAnEntryForAnotherFolderAsNotRegistered()
        {
            // A copy that was unzipped somewhere else leaves an entry pointing at
            // a path this install is not running from. Reporting that as off is
            // what lets switching it back on repair the entry.
            Assert.That(
                WindowsStartupPolicy.Matches(
                    "\"D:\\Old\\DOTORI ON\\DOTORI ON.exe\"", InstalledPath),
                Is.False);
        }

        [Test]
        public void Matches_TreatsNothingAsNotRegistered()
        {
            Assert.That(WindowsStartupPolicy.Matches(null, InstalledPath), Is.False);
            Assert.That(WindowsStartupPolicy.Matches("", InstalledPath), Is.False);
            Assert.That(WindowsStartupPolicy.Matches("   ", InstalledPath), Is.False);
        }
    }
}
