using System;
using System.IO;
using DOTORION.Events.Subjugation;
using DOTORION.Core;
using NUnit.Framework;
using UnityEngine;

namespace DOTORION.Tests
{
    public sealed class SubjugationTests
    {
        [Test]
        public void PixelArtZoomRetainsExactPixelBlocksAndOpaqueEdges()
        {
            var frame = new SubjugationFrame(new[]
            {
                new Color32(250, 250, 245, 255), new Color32(60, 28, 16, 255),
                new Color32(10, 20, 30, 0), new Color32(247, 158, 181, 255)
            }, 2, 2, 2, 2);
            var native = frame.PixelArt(2, 2);
            var zoom = frame.PixelArt(4, 4);
            for (var y = 0; y < 4; y++) for (var x = 0; x < 4; x++)
            {
                Assert.That(zoom[y * 4 + x], Is.EqualTo(native[y / 2 * 2 + x / 2]));
                Assert.That(zoom[y * 4 + x].a == 0 || zoom[y * 4 + x].a == 255, Is.True);
            }
        }

        [Test]
        public void ApprovedHeroesEnemiesAndFallenPosesShareGridAndExactZoomBlocks()
        {
            using (var art = new SubjugationArt())
            {
                var frames = new System.Collections.Generic.List<SubjugationFrame>();
                for (var id = 0; id < 6; id++) { for (var state = 0; state < 3; state++) frames.Add(art.Frame(id, state)); frames.Add(art.Down(id)); }
                for (var round = 1; round <= SubjugationArt.MonsterCount; round++) frames.Add(art.Monster(round));
                foreach (var frame in frames)
                {
                    Assert.That(frame.PixelExtent, Is.EqualTo(SubjugationArt.CombatPixelExtent));
                    Assert.That(frame.MasterWidth, Is.InRange(1, SubjugationArt.CombatPixelExtent)); Assert.That(frame.MasterHeight, Is.InRange(1, SubjugationArt.CombatPixelExtent));
                    Assert.That(frame.UsesAuthoredPixels, Is.True);
                    var master = frame.PixelArt(frame.MasterWidth, frame.MasterHeight);
                    foreach (var zoom in new[] { 2, 3, 4 }) // Integer zooms preserve the approved pixel clusters.
                    {
                        var w = frame.MasterWidth * zoom; var h = frame.MasterHeight * zoom;
                        var pixels = frame.PixelArt(w, h);
                        for (var y = 0; y < h; y++) for (var x = 0; x < w; x++)
                            Assert.That(pixels[y * w + x], Is.EqualTo(master[y / zoom * frame.MasterWidth + x / zoom]));
                    }
                }
                for (var id = 0; id < 6; id++) Assert.That(art.PortraitFrame(id).PixelExtent, Is.EqualTo(32));
            }
        }

        [Test]
        public void ApprovedPixelArtPreservesSourceColorsInsteadOfAveragingOrRecoloring()
        {
            var source = new[] { new Color32(240, 7, 15, 254), new Color32(5, 244, 6, 254) };
            var frame = new SubjugationFrame(source, 2, 1, 2, 1, authoredPixels: true);
            var output = frame.PixelArt(1, 1)[0];
            Assert.That(output.r, Is.EqualTo(5)); Assert.That(output.g, Is.EqualTo(244)); Assert.That(output.b, Is.EqualTo(6));
            Assert.That(output.a, Is.EqualTo(255));
            Assert.That(source[1].a, Is.EqualTo(254));
        }

        [Test]
        public void OriginalArtReductionAveragesDetailsAndKeepsSourceAlpha()
        {
            var input = new[] { new Color32(255, 0, 0, 253), new Color32(0, 0, 255, 253) };
            var frame = new SubjugationFrame(input, 2, 1, 2);
            var reduced = frame.Resize(1, 1)[0];
            Assert.That(reduced.r, Is.InRange(127, 128));
            Assert.That(reduced.b, Is.InRange(127, 128));
            Assert.That(reduced.a, Is.EqualTo(253));
            Assert.That(input[0].r, Is.EqualTo(255));
            Assert.That(input[0].a, Is.EqualTo(253));
        }

        [Test]
        public void TransparentEdgesDoNotCreateDarkHalosWhenShrinking()
        {
            var frame = new SubjugationFrame(new[] { new Color32(255, 255, 255, 255), new Color32(0, 0, 0, 0) }, 2, 1, 2);
            var reduced = frame.Resize(1, 1)[0];
            Assert.That(reduced.r, Is.EqualTo(255));
            Assert.That(reduced.g, Is.EqualTo(255));
            Assert.That(reduced.b, Is.EqualTo(255));
            Assert.That(reduced.a, Is.InRange(127, 128));
        }

        [TestCase(ActivityStatus.Working, ConnectionStatus.Connected, 0, true)]
        [TestCase(ActivityStatus.Break, ConnectionStatus.Connected, 0, false)]
        [TestCase(ActivityStatus.Meal, ConnectionStatus.Connected, 0, false)]
        [TestCase(ActivityStatus.Working, ConnectionStatus.Degraded, 0, false)]
        [TestCase(ActivityStatus.Working, ConnectionStatus.Disconnected, 0, false)]
        [TestCase(ActivityStatus.Working, ConnectionStatus.Connected, 181, false)]
        public void AttendanceGateOnlyAcceptsCurrentConnectedWork(ActivityStatus activity, ConnectionStatus connection, int heartbeatAge, bool expected)
        {
            var now = DateTimeOffset.UtcNow;
            var member = new MemberState("member", "테스트", "", 0, AttendanceStatus.ClockedIn,
                activity, connection, now, now, now.AddSeconds(-heartbeatAge), null, now);
            Assert.That(SubjugationEvent.CanAdvance(member, now), Is.EqualTo(expected));
        }

        [Test]
        public void ClockedOutAndMissingMemberNeverAdvance()
        {
            var now = DateTimeOffset.UtcNow;
            Assert.That(SubjugationEvent.CanAdvance(null, now), Is.False);
            var offline = new MemberState("member", "테스트", "", 0, AttendanceStatus.ClockedOut,
                null, ConnectionStatus.Connected, null, null, now, now, now);
            Assert.That(SubjugationEvent.CanAdvance(offline, now), Is.False);
        }

        [Test]
        public void StartsWithOnlyChiikawaAndNoCurrency()
        {
            var game = new SubjugationGame();
            Assert.That(game.Save.round, Is.EqualTo(1));
            Assert.That(game.Save.coins, Is.Zero);
            Assert.That(game.PartyCount, Is.EqualTo(1));
            Assert.That(game.NextHire, Is.EqualTo(1));
            Assert.That(game.Save.levels, Is.EqualTo(new[] { 1, 0, 0, 0, 0, 0 }));
        }

        [Test]
        public void PausedTimeDoesNotDamageRewardOrAdvanceRounds()
        {
            var game = new SubjugationGame();
            game.Tick(.5, true);
            for (var i = 0; i < 100; i++) game.Tick(1, false);
            Assert.That(game.EnemyHp, Is.EqualTo(game.EnemyMaxHp));
            Assert.That(game.PartyHp, Is.EqualTo(game.PartyMaxHp));
            Assert.That(game.Save.coins, Is.Zero);
            game.Tick(.5, true);
            Assert.That(game.EnemyHp, Is.LessThan(game.EnemyMaxHp));
        }

        [Test]
        public void ClearRewardUsesClearedRoundAndActualCredit()
        {
            var game = new SubjugationGame(new SubjugationSave { round = 4 },
                new SubjugationBalance { enemyHealth = 1 });
            game.Tick(1, true);
            Assert.That(game.Save.round, Is.EqualTo(5));
            Assert.That(game.LastClearReward, Is.EqualTo(61)); // round(10 * 4^1.3)
            game.Tick(1, true);
            game.Tick(1, true);
            Assert.That(game.LastClearReward, Is.EqualTo(81));
            var capped = new SubjugationGame(new SubjugationSave { coins = long.MaxValue - 1 },
                new SubjugationBalance { enemyHealth = 1 });
            capped.Tick(1, true);
            Assert.That(capped.LastClearReward, Is.EqualTo(1));
        }

        [Test]
        public void ExactlyOneEnemyClearGrantsOneRewardAndNextRound()
        {
            var game = new SubjugationGame();
            for (var i = 0; i < 4; i++) game.Tick(1, true);
            Assert.That(game.Save.round, Is.EqualTo(2));
            Assert.That(game.Save.coins, Is.EqualTo(10));
            Assert.That(game.EnemyHp, Is.EqualTo(game.EnemyMaxHp));
        }

        [Test]
        public void DefeatFallsBackOneRoundWithRepeatOnAndPauseFreezesRetry()
        {
            var game = new SubjugationGame(new SubjugationSave { round = 100 });
            Assert.That(game.RepeatRound, Is.False);
            game.Tick(1, true);
            Assert.That(game.RetryRemaining, Is.GreaterThan(0));
            var remaining = game.RetryRemaining;
            game.Tick(1, false);
            Assert.That(game.RetryRemaining, Is.EqualTo(remaining));
            Assert.That(game.Save.round, Is.EqualTo(100)); // The failure is shown on the round that was lost.
            for (var i = 0; i < 3; i++) game.Tick(1, true);
            Assert.That(game.Save.round, Is.EqualTo(99));
            Assert.That(game.RepeatRound, Is.True);
            Assert.That(game.Save.maxRound, Is.EqualTo(100));
            Assert.That(game.Save.coins, Is.Zero);
            Assert.That(game.PartyHp, Is.EqualTo(game.PartyMaxHp));
        }

        [Test]
        public void LosingRoundOneRetriesRoundOne()
        {
            var game = new SubjugationGame(null, new SubjugationBalance { enemyHealth = 100000, enemyAttack = 100000 });
            game.Tick(1, true);
            for (var i = 0; i < 3; i++) game.Tick(1, true);
            Assert.That(game.Save.round, Is.EqualTo(1));
            Assert.That(game.RepeatRound, Is.False);
        }

        [Test]
        public void RoundBannerShowsWhileEachFightStartsAndFreezesOutsideWork()
        {
            var game = new SubjugationGame(null, new SubjugationBalance { enemyHealth = 15 });
            Assert.That(game.RoundBannerRemaining, Is.Zero);
            game.Tick(.5, true);
            Assert.That(game.RoundBannerRemaining, Is.EqualTo(SubjugationGame.RoundBannerSeconds));
            game.Tick(1, false);
            Assert.That(game.RoundBannerRemaining, Is.EqualTo(SubjugationGame.RoundBannerSeconds));
            game.Tick(.5, true); game.Tick(1, true); // Two hits win round 1 while the banner runs out underneath.
            Assert.That(game.Outcome, Is.EqualTo(1));
            Assert.That(game.RoundBannerRemaining, Is.LessThan(SubjugationGame.RoundBannerSeconds));
            game.Tick(1, true); // Success display ends.
            game.Tick(.1, true); // Round 2 begins.
            Assert.That(game.Save.round, Is.EqualTo(2));
            Assert.That(game.RoundBannerRemaining, Is.EqualTo(SubjugationGame.RoundBannerSeconds));
        }

        [Test]
        public void HiringIsSequentialAndNeverExceedsThreeActiveMembers()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 1000000 });
            Assert.That(game.Hire(5), Is.False);
            for (var i = 1; i < 6; i++) Assert.That(game.Hire(i), Is.True);
            Assert.That(game.PartyCount, Is.EqualTo(3));
            Assert.That(game.NextHire, Is.EqualTo(-1));
            Assert.That(game.ToggleParty(5), Is.False);
            Assert.That(game.ToggleParty(0), Is.True);
            Assert.That(game.ToggleParty(5), Is.True);
            Assert.That(game.PartyCount, Is.EqualTo(3));
            Assert.That(game.Hire(5), Is.False);
        }

        [Test]
        public void PurchasesRequireEnoughCoinsAndChargeExactlyOnce()
        {
            var game = new SubjugationGame();
            Assert.That(game.Hire(1), Is.False);
            Assert.That(game.LevelUp(0), Is.False);
            game.Save.coins = game.Balance.hireCosts[1] + game.LevelCost(0);
            Assert.That(game.Hire(1), Is.True);
            Assert.That(game.Save.coins, Is.EqualTo(game.LevelCost(0)));
            Assert.That(game.Hire(1), Is.False);
            Assert.That(game.LevelUp(0), Is.True);
            Assert.That(game.Save.coins, Is.Zero);
            Assert.That(game.Save.levels[0], Is.EqualTo(2));
            Assert.That(game.LevelUp(0), Is.False);
        }

        [Test]
        public void BackgroundDeltaDoesNotGrantCatchUpRewards()
        {
            var game = new SubjugationGame();
            game.Tick(5000, true);
            Assert.That(game.Save.round, Is.EqualTo(1));
            Assert.That(game.EnemyHp, Is.EqualTo(game.EnemyMaxHp - game.Power(0)));
        }

        [Test]
        public void LastActiveMemberCannotBeRemovedAndRosterChangesDoNotHeal()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 3000 });
            Assert.That(game.ToggleParty(0), Is.False);
            game.Tick(1, true);
            var hp = game.MemberHealth(0);
            game.Hire(1); game.LevelUp(0);
            Assert.That(game.MemberHealth(0), Is.EqualTo(hp));
        }

        [Test]
        public void SaveIsPerMemberAndRecoversPreviousAtomicWrite()
        {
            var dir = Path.Combine(Path.GetTempPath(), "dotori-event-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new SubjugationStore(dir, "one");
                store.Write(new SubjugationSave { round = 5, coins = 99 });
                store.Write(new SubjugationSave { round = 6, coins = 109 });
                Assert.That(store.Load(null).Save.round, Is.EqualTo(6));
                Assert.That(new SubjugationStore(dir, "two").Load(null).Save.round, Is.EqualTo(1));
                File.WriteAllText(Path.Combine(dir, "chiikawa-v1-one.json"), "invalid json");
                var recovered = store.Load(null);
                Assert.That(recovered.Save.round, Is.EqualTo(5));
                Assert.That(recovered.Save.coins, Is.EqualTo(99));
            }
            finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
        }

        [Test]
        public void RepeatVictoryPaysOnceAndKeepsRoundWithVisibleSuccess()
        {
            var game = new SubjugationGame(null, new SubjugationBalance { enemyHealth = 40 }); game.ToggleAutoAdvance();
            for (var i = 0; i < 4; i++) game.Tick(1, true);
            Assert.That(game.Save.round, Is.EqualTo(1));
            Assert.That(game.Save.maxRound, Is.EqualTo(2));
            Assert.That(game.Save.coins, Is.EqualTo(10));
            Assert.That(game.Outcome, Is.EqualTo(1));
            game.Tick(.5, false); Assert.That(game.OutcomeRemaining, Is.EqualTo(1));
            game.Tick(1, true); Assert.That(game.Outcome, Is.Zero);
            Assert.That(game.Save.coins, Is.EqualTo(10));
            for (var i = 0; i < 4; i++) game.Tick(1, true);
            Assert.That(game.Save.coins, Is.EqualTo(20));
            Assert.That(game.Save.round, Is.EqualTo(1));
        }
        [Test]
        public void RoundSelectionCannotExceedUnlockedFrontierAndPreservesCurrency()
        {
            var game = new SubjugationGame(new SubjugationSave { round = 5, coins = 100 });
            Assert.That(game.SelectRound(1), Is.True);
            Assert.That(game.SelectRound(6), Is.False);
            Assert.That(game.SelectRound(0), Is.False);
            Assert.That(game.Save.round, Is.EqualTo(1));
            Assert.That(game.Save.maxRound, Is.EqualTo(5));
            Assert.That(game.SelectRound(5), Is.True);
            Assert.That(game.Save.coins, Is.EqualTo(100));
        }
        [Test]
        public void LevelUpRaisesHealthAndAttackOrHealingAndShisaRestoresHealth()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 10000000 }, new SubjugationBalance { enemyHealth = 10000 });
            for (var i = 1; i < 6; i++) game.Hire(i);
            for (var i = 0; i < 6; i++)
            {
                var hp = game.MaxHealth(i); var power = game.Power(i);
                game.LevelUp(i);
                Assert.That(game.MaxHealth(i), Is.GreaterThan(hp));
                Assert.That(game.Power(i), Is.GreaterThan(power));
            }
            game.ToggleParty(1); game.ToggleParty(2); game.ToggleParty(4); game.ToggleParty(5);
            game.Tick(1, true); var wounded = game.PartyHp;
            game.Tick(1, true);
            Assert.That(game.Numbers.Exists(n => n.healing && n.actor == 4), Is.True);
            Assert.That(game.PartyHp, Is.GreaterThanOrEqualTo(wounded));
        }
        [Test]
        public void FourPoseKeysAndDamageNumbersFreezeOutsideWork()
        {
            var game = new SubjugationGame();
            for (var phase = 0; phase < 4; phase++) { Assert.That(game.AttackFrame(0), Is.EqualTo(phase)); game.Tick(.25, true); }
            Assert.That(game.Numbers.Exists(n => !n.enemy && n.amount == game.Power(0)), Is.True);
            var age = game.Numbers[0].age;
            game.Tick(1, false); Assert.That(game.Numbers[0].age, Is.EqualTo(age));
        }

        [Test]
        public void LegacySaveMigratesAndRepeatSettingSurvivesReload()
        {
            var legacy = JsonUtility.FromJson<SubjugationSave>("{\"version\":1,\"round\":12,\"coins\":123,\"levels\":[1,0,0,0,0,0],\"party\":[true,false,false,false,false,false]}");
            var game = new SubjugationGame(legacy);
            Assert.That(game.Save.maxRound, Is.EqualTo(12));
            Assert.That(game.Save.autoAdvance, Is.True);
            game.ToggleAutoAdvance();
            var restored = new SubjugationGame(JsonUtility.FromJson<SubjugationSave>(JsonUtility.ToJson(game.Save)));
            Assert.That(restored.Save.autoAdvance, Is.False);
            Assert.That(restored.Save.coins, Is.EqualTo(123));
        }

        [Test]
        public void EnemyHitsOnlyFrontAndSkipsFallenMembersOnNextAttack()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 10000, round = 100 }, new SubjugationBalance { enemyHealth = 100000 });
            game.Hire(1); game.Hire(2);
            Assert.That(game.FrontMember, Is.EqualTo(2));
            game.Tick(1, true);
            Assert.That(game.IsDown(2), Is.True);
            Assert.That(game.MemberHealth(1), Is.EqualTo(game.MaxHealth(1)));
            Assert.That(game.MemberHealth(0), Is.EqualTo(game.MaxHealth(0)));
            Assert.That(game.FrontMember, Is.EqualTo(1));
            var enemyHp = game.EnemyHp;
            game.Tick(1, true);
            Assert.That(game.IsDown(1), Is.True);
            Assert.That(game.EnemyHp, Is.EqualTo(enemyHp - game.Power(0) - game.Power(1)));
            Assert.That(game.Numbers.Exists(n => n.enemy && n.target == 1), Is.True);
            Assert.That(game.MemberHealth(0), Is.EqualTo(game.MaxHealth(0)));
            game.Tick(1, true);
            Assert.That(game.Outcome, Is.EqualTo(-1));
            Assert.That(game.PartyHp, Is.Zero);
            for (var i = 0; i < 3; i++) game.Tick(1, true);
            Assert.That(game.MemberHealth(2), Is.EqualTo(game.MaxHealth(2)));
            Assert.That(game.FrontMember, Is.EqualTo(2));
        }
        [Test]
        public void FallenShisaDoesNotHealAndSurvivingShisaCannotRevive()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 1000000 }, new SubjugationBalance { enemyHealth = 100000, enemyAttack = 100000 });
            for (var i = 1; i < 6; i++) game.Hire(i);
            game.ToggleParty(1); game.ToggleParty(2); game.ToggleParty(4); game.ToggleParty(5);
            game.Tick(1, true); // Rakko falls; Shisa must not resurrect him.
            game.Tick(1, true); // Shisa falls next.
            Assert.That(game.IsDown(5), Is.True); Assert.That(game.IsDown(4), Is.True);
            Assert.That(game.Numbers.Exists(n => n.healing), Is.False);
            game.Tick(1, true); // Chiikawa falls and the attempt ends.
            Assert.That(game.Outcome, Is.EqualTo(-1));
        }

        [Test]
        public void ShisaHealsEveryLivingMemberIncludingHerself()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 1000000 }, new SubjugationBalance { enemyHealth = 100000 });
            for (var i = 1; i < 6; i++) game.Hire(i);
            game.ToggleParty(1); game.ToggleParty(2); game.ToggleParty(4); game.ToggleParty(5);
            game.Tick(1, true); // Rakko is hit.
            game.ToggleParty(5);
            game.Tick(1, true); // Shisa is front now and is hit too.
            game.ToggleParty(5);
            Assert.That(game.MemberHealth(4), Is.LessThan(game.MaxHealth(4)));
            Assert.That(game.MemberHealth(5), Is.LessThan(game.MaxHealth(5)));
            game.Numbers.Clear();
            game.Tick(1, true);
            Assert.That(game.Numbers.Exists(n => n.healing && n.target == 4), Is.True);
            Assert.That(game.Numbers.Exists(n => n.healing && n.target == 5), Is.True);
            Assert.That(game.Numbers.Exists(n => n.healing && n.target == 0), Is.False); // Already full: no +0.
            Assert.That(game.MemberHealth(4), Is.EqualTo(game.MaxHealth(4)));
        }

        [Test]
        public void MembersShareOneCurveButJoinAtTheirStartLevelsAndCapAt100()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 1000000 });
            Assert.That(game.Level(5), Is.EqualTo(40)); // Unhired members preview their joining level.
            for (var i = 1; i < 6; i++) Assert.That(game.Hire(i), Is.True);
            Assert.That(game.Save.levels, Is.EqualTo(new[] { 1, 1, 1, 10, 10, 40 }));
            for (var i = 0; i < 6; i++) game.Save.levels[i] = 70;
            for (var i = 1; i < 6; i++) Assert.That(game.MaxHealth(i), Is.EqualTo(game.MaxHealth(0)));
            foreach (var i in new[] { 1, 2, 3, 5 }) Assert.That(game.Power(i), Is.EqualTo(game.Power(0)));
            Assert.That(game.Power(4), Is.EqualTo(game.Power(0) * game.Balance.healRatio));
            game.Save.levels[0] = 99; game.Save.coins = game.LevelCost(0);
            Assert.That(game.LevelUp(0), Is.True);
            Assert.That(game.IsMaxLevel(0), Is.True);
            game.Save.coins = long.MaxValue / 2;
            Assert.That(game.LevelUp(0), Is.False);
            Assert.That(game.Save.levels[0], Is.EqualTo(100));
            var legacy = new SubjugationGame(new SubjugationSave { levels = new[] { 250, 1, 1, 1, 1, 0 } });
            Assert.That(legacy.Save.levels, Is.EqualTo(new[] { 100, 1, 1, 10, 10, 0 }));
        }

        [Test]
        public void FinalRoundIsTheCapAndRepeatsAfterClear()
        {
            var game = new SubjugationGame(new SubjugationSave { round = 150, maxRound = 180 }, new SubjugationBalance { enemyHealth = .001 });
            Assert.That(game.Save.round, Is.EqualTo(100)); Assert.That(game.Save.maxRound, Is.EqualTo(100));
            game.Tick(1, true);
            Assert.That(game.Outcome, Is.EqualTo(1));
            Assert.That(game.Save.round, Is.EqualTo(100)); Assert.That(game.Save.maxRound, Is.EqualTo(100));
        }

        // Three attackers need two Lv100 members and one around Lv70; a little less does not clear.
        // A Shisa party survives longer, so it clears with fewer levels but kills (and earns) slower.
        [TestCase(new int[] { 2, 3, 5 }, new int[] { 70, 100, 100 }, true)]
        [TestCase(new int[] { 2, 3, 5 }, new int[] { 100, 100, 70 }, true)]
        [TestCase(new int[] { 2, 3, 5 }, new int[] { 90, 90, 90 }, true)]
        [TestCase(new int[] { 2, 3, 5 }, new int[] { 60, 100, 100 }, false)]
        [TestCase(new int[] { 2, 3, 5 }, new int[] { 85, 85, 85 }, false)]
        [TestCase(new int[] { 3, 4, 5 }, new int[] { 100, 70, 100 }, true)]
        [TestCase(new int[] { 3, 4, 5 }, new int[] { 75, 75, 75 }, true)]
        [TestCase(new int[] { 3, 4, 5 }, new int[] { 70, 70, 70 }, false)]
        public void FinalRoundNeedsTwoMaxLevelsAndOneNearSeventy(int[] members, int[] levels, bool clears)
        {
            var save = new SubjugationSave { round = 100, levels = new[] { 1, 1, 1, 10, 10, 40 }, party = new bool[6] };
            for (var i = 0; i < members.Length; i++) { save.levels[members[i]] = levels[i]; save.party[members[i]] = true; }
            var game = new SubjugationGame(save, LoadBalance());
            for (var beat = 0; beat < 600 && game.Outcome == 0; beat++) game.Tick(1, true);
            Assert.That(game.Outcome, Is.EqualTo(clears ? 1 : -1));
        }

        [Test]
        public void EveryTenthRoundIsAStrongerBossThatPaysMore()
        {
            var game = new SubjugationGame(new SubjugationSave { round = 10, maxRound = 11 });
            var b = game.Balance;
            Assert.That(game.IsBossRound, Is.True);
            Assert.That(game.EnemyMaxHp, Is.EqualTo(b.enemyHealth * Math.Pow(10, b.enemyGrowth) * b.bossHealth));
            Assert.That(game.EnemyAttack, Is.EqualTo(b.enemyAttack * 10 * b.bossAttack));
            Assert.That(game.Reward(10), Is.EqualTo(499));
            var bossHp = game.EnemyMaxHp; var bossAttack = game.EnemyAttack;
            game.SelectRound(11);
            Assert.That(game.IsBossRound, Is.False);
            Assert.That(game.EnemyMaxHp, Is.LessThan(bossHp / 2));
            Assert.That(game.EnemyAttack, Is.LessThan(bossAttack));
            Assert.That(game.Reward(11), Is.LessThan(game.Reward(10) / 2));
            Assert.That(game.IsBoss(100), Is.True); Assert.That(game.IsBoss(99), Is.False);
        }

        [Test]
        public void MilestoneRewardsFollowClearsAndPayTheirRound()
        {
            Assert.That(SubjugationGame.RewardMilestones, Is.EqualTo(new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }));
            var total = 0; foreach (var m in SubjugationGame.RewardMilestones) total += m;
            Assert.That(total, Is.EqualTo(550));
            var game = new SubjugationGame(new SubjugationSave { round = 10 }, new SubjugationBalance { enemyHealth = .001 });
            Assert.That(game.HasCleared(10), Is.False); // Reaching R10 is not clearing it.
            game.Tick(1, true);
            Assert.That(game.HasCleared(10), Is.True); Assert.That(game.HasCleared(20), Is.False);
            var final = new SubjugationGame(new SubjugationSave { round = 100 }, new SubjugationBalance { enemyHealth = .001 });
            Assert.That(final.HasCleared(100), Is.False);
            final.Tick(1, true);
            Assert.That(final.HasCleared(100), Is.True); // maxRound stays 100, bestClear records the win.
            var legacy = new SubjugationGame(new SubjugationSave { round = 31, maxRound = 41 });
            Assert.That(legacy.Save.bestClear, Is.EqualTo(40));
        }

        [Test]
        public void EventOpensAt1110OnOctober19AndEndsAfterNovember30InSeoul()
        {
            Assert.That(SubjugationEvent.IsRunning(new DateTimeOffset(2026, 10, 19, 11, 9, 59, TimeSpan.FromHours(9))), Is.False);
            Assert.That(SubjugationEvent.IsRunning(new DateTimeOffset(2026, 10, 19, 11, 10, 0, TimeSpan.FromHours(9))), Is.True);
            Assert.That(SubjugationEvent.IsRunning(new DateTimeOffset(2026, 10, 19, 2, 10, 0, TimeSpan.Zero)), Is.True); // Same instant in UTC.
            Assert.That(SubjugationEvent.PeriodText, Is.EqualTo("이벤트 기간 2026/10/19 11:10 ~ 2026/11/30 23:59"));
            Assert.That(SubjugationEvent.EndsAt.UtcDateTime, Is.EqualTo(new DateTime(2026, 11, 30, 15, 0, 0, DateTimeKind.Utc)));
            Assert.That(SubjugationEvent.IsRunning(new DateTimeOffset(2026, 11, 30, 23, 59, 59, TimeSpan.FromHours(9))), Is.True);
            Assert.That(SubjugationEvent.IsRunning(new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(9))), Is.False);
        }

        [Test]
        public void ShippedBalanceMatchesCodeDefaults()
        {
            Assert.That(JsonUtility.ToJson(LoadBalance()), Is.EqualTo(JsonUtility.ToJson(new SubjugationBalance())));
        }

        private static SubjugationBalance LoadBalance()
        {
            var config = Resources.Load<TextAsset>("DOTORION/Events/ChiikawaBalance");
            Assert.That(config, Is.Not.Null);
            return JsonUtility.FromJson<SubjugationBalance>(config.text);
        }
        [Test]
        public void DebugGrantAdds9999WithoutChangingRosterAndClampsOverflow()
        {
            var game = new SubjugationGame(new SubjugationSave { coins = 20 });
            game.GrantDebugCoins(); Assert.That(game.Save.coins, Is.EqualTo(10019));
            Assert.That(game.NextHire, Is.EqualTo(1)); Assert.That(game.Save.round, Is.EqualTo(1));
            game.Save.coins = long.MaxValue - 1; game.GrantDebugCoins(); Assert.That(game.Save.coins, Is.EqualTo(long.MaxValue));
            Assert.That(game.RepeatRound, Is.False); game.ToggleAutoAdvance(); Assert.That(game.RepeatRound, Is.True);
        }
        [Test]
        public void AllTenEnemiesAndSixDownSpritesAreAvailable()
        {
            using (var art = new SubjugationArt())
            {
                for (var i = 1; i <= 10; i++) { Assert.That(art.Monster(i), Is.Not.Null); Assert.That(art.Monster(i).Width, Is.GreaterThan(1)); }
                Assert.That(art.Monster(11), Is.SameAs(art.Monster(1)));
                for (var i = 0; i < 6; i++) Assert.That(art.Down(i), Is.Not.Null);
            }
        }

        [Test]
        public void InvalidRosterCannotCreateCurrencyOrLockedCharacters()
        {
            Assert.Throws<ArgumentException>(() => new SubjugationGame(new SubjugationSave { coins = -1 }));
            Assert.Throws<ArgumentException>(() => new SubjugationGame(new SubjugationSave { levels = new[] { 1, 0, 2, 0, 0, 0 } }));
        }
    }
}
