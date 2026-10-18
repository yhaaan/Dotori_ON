using System;

namespace DOTORION.Events.Subjugation
{
    // Pure game rules: no attendance mutations, rendering, network or Unity dependency.
    // Tuned so 15 days of 6 working hours barely reach and clear R100 with Lv100 x2 + Lv70 (Documentation/ChiikawaBalance.md).
    [Serializable]
    public sealed class SubjugationBalance
    {
        public int[] hireCosts = { 0, 1500, 7500, 75000, 150000, 700000 };
        public int[] startLevels = { 1, 1, 1, 10, 10, 40 };
        public int maxLevel = 100;
        public int maxRound = 100;
        public double levelCost = 940;
        public double levelCostGrowth = 1.4;
        public double baseReward = 10;
        public double rewardGrowth = 1.3;
        public double enemyHealth = 25;
        public double enemyGrowth = 1.3;
        public double enemyAttack = .91;
        public double enemyAttackGrowth = 1;
        // Every 10th round is a boss hurdle: about 3.5x a normal fight, paid in proportion.
        public int bossInterval = 10;
        public double bossHealth = 2.5;
        public double bossAttack = 1.4;
        public double bossReward = 2.5;
        // Every member shares one curve; Shisa heals each living member by healRatio of it instead of attacking.
        // healRatio keeps a Shisa party as fast to R100 as three attackers: it survives more but kills slower.
        public double attack = 10;
        public double attackPerLevel = 3;
        public double health = 100;
        public double healthPerLevel = 20;
        public double healRatio = .37;
        public double retrySeconds = 3;
    }

    [Serializable]
    public sealed class SubjugationSave
    {
        public int version = 2;
        public int round = 1;
        public int maxRound = 1;
        // Highest round actually won; maxRound cannot say it once R100 repeats.
        public int bestClear;
        public bool autoAdvance = true;
        public long coins;
        public int[] levels = { 1, 0, 0, 0, 0, 0 };
        public bool[] party = { true, false, false, false, false, false };
    }

    public sealed class SubjugationGame
    {
        public static readonly string[] Names = { "치이카와", "하치와레", "우사기", "모몽가", "시사", "랏코" };
        public readonly SubjugationSave Save;
        public readonly SubjugationBalance Balance;
        public double EnemyHp { get; private set; }
        private readonly double[] _health = new double[6];
        public double MemberHealth(int id) => _health[id];
        public bool IsDown(int id) => Save.party[id] && _health[id] <= 0;
        public double PartyHp { get { double hp = 0; for (var i = 0; i < 6; i++) if (Save.party[i]) hp += _health[i]; return hp; } }
        public int FrontMember { get { for (var i = 5; i >= 0; i--) if (Save.party[i] && _health[i] > 0) return i; return -1; } }
        public bool RepeatRound => !Save.autoAdvance;
        // Every 10th clear pays DOTORION points equal to its round, claimed once on the server.
        public static readonly int[] RewardMilestones = { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        public bool HasCleared(int round) => Save.bestClear >= round;
        public void GrantDebugCoins() { Save.coins = Save.coins > long.MaxValue - 9999 ? long.MaxValue : Save.coins + 9999; Changed?.Invoke(); }
        public double RetryRemaining { get; private set; }
        /// <summary>Seconds left on the "ROUND n" banner shown as each fight begins. The fight runs underneath.</summary>
        public double RoundBannerRemaining { get; private set; }
        public const double RoundBannerSeconds = 1.2;
        private bool _announce;
        public bool IsBoss(int round) => Balance.bossInterval > 0 && round % Balance.bossInterval == 0;
        public bool IsBossRound => IsBoss(Save.round);
        public double EnemyMaxHp => Balance.enemyHealth * Math.Pow(Save.round, Balance.enemyGrowth) * (IsBossRound ? Balance.bossHealth : 1);
        public double EnemyAttack => Balance.enemyAttack * Math.Pow(Save.round, Balance.enemyAttackGrowth) * (IsBossRound ? Balance.bossAttack : 1);
        public long Reward(int round) => (long)Math.Round(Balance.baseReward * Math.Pow(round, Balance.rewardGrowth) * (IsBoss(round) ? Balance.bossReward : 1));
        public double PartyMaxHp { get { double hp = 0; for (var i = 0; i < 6; i++) if (Save.party[i]) hp += MaxHealth(i); return hp; } }
        // Unhired members preview the level they join at.
        public int Level(int id) => Save.levels[id] > 0 ? Save.levels[id] : StartLevel(id);
        public int StartLevel(int id) => Math.Max(1, Math.Min(Balance.maxLevel, Balance.startLevels[id]));
        public bool IsMaxLevel(int id) => Save.levels[id] >= Balance.maxLevel;
        public double MaxHealth(int id) => Balance.health + Balance.healthPerLevel * (Level(id) - 1);
        public double Power(int id) => (Balance.attack + Balance.attackPerLevel * (Level(id) - 1)) * (id == 4 ? Balance.healRatio : 1);
        public int Outcome { get; private set; } // 1 success, -1 defeat
        public double OutcomeRemaining { get; private set; }
        public long LastClearReward { get; private set; }
        public double CombatTime { get; private set; }
        public int AttackFrame(int id) => Outcome != 0 ? 0 : Math.Min(3, (int)(_beat * 4));
        public readonly System.Collections.Generic.List<CombatNumber> Numbers = new System.Collections.Generic.List<CombatNumber>();
        public sealed class CombatNumber { public int actor, target; public double amount, age; public bool healing, enemy; }
        public bool SelectRound(int round)
        {
            if (round < 1 || round > Save.maxRound) return false;
            Save.round = round; ResetBattle(); Outcome = 0; OutcomeRemaining = 0; Numbers.Clear(); Changed?.Invoke(); return true;
        }
        public void ToggleAutoAdvance() { Save.autoAdvance = !Save.autoAdvance; Changed?.Invoke(); }
        public event Action Changed;
        private double _beat;

        public SubjugationGame(SubjugationSave save = null, SubjugationBalance balance = null)
        {
            Save = save ?? new SubjugationSave();
            Balance = balance ?? new SubjugationBalance();
            if ((Save.version != 1 && Save.version != 2) || Save.levels == null || Save.levels.Length != 6 ||
                Save.party == null || Save.party.Length != 6 || Save.round < 1 || Save.coins < 0)
                throw new ArgumentException("Invalid event save");
            if (Save.version == 1) Save.autoAdvance = true;
            Save.version = 2;
            Save.round = Math.Min(Save.round, Balance.maxRound);
            Save.maxRound = Math.Min(Math.Max(Save.round, Save.maxRound), Balance.maxRound);
            // Saves from before bestClear: every round below the unlocked frontier was won.
            Save.bestClear = Math.Min(Balance.maxRound, Math.Max(Save.bestClear, Save.maxRound - 1));
            var gap = false;
            var count = 0;
            for (var i = 0; i < 6; i++)
            {
                if (Save.levels[i] < 0 || Save.levels[i] > 10000 || (gap && Save.levels[i] > 0))
                    throw new ArgumentException("Invalid event roster");
                // Saves from earlier balance passes fit the current start levels and cap.
                if (Save.levels[i] > 0) Save.levels[i] = Math.Min(Balance.maxLevel, Math.Max(Save.levels[i], StartLevel(i)));
                if (Save.levels[i] == 0) gap = true;
                if (Save.party[i] && (Save.levels[i] == 0 || ++count > 3))
                    throw new ArgumentException("Invalid event party");
            }
            if (Save.levels[0] < 1 || count == 0) throw new ArgumentException("Missing starter");
            ResetBattle();
        }

        public int PartyCount { get { var n = 0; foreach (var active in Save.party) if (active) n++; return n; } }
        public int PartyLevel { get { var n = 0; for (var i = 0; i < 6; i++) if (Save.party[i]) n += Save.levels[i]; return n; } }
        public int NextHire { get { for (var i = 0; i < 6; i++) if (Save.levels[i] == 0) return i; return -1; } }
        public long LevelCost(int index) => (long)Math.Round(Balance.levelCost * Math.Pow(Save.levels[index], Balance.levelCostGrowth));

        public bool Hire(int index)
        {
            if (index < 0 || index >= 6 || index != NextHire || Save.coins < Balance.hireCosts[index]) return false;
            Save.coins -= Balance.hireCosts[index];
            Save.levels[index] = StartLevel(index);
            _health[index] = MaxHealth(index);
            Save.party[index] = PartyCount < 3;
            RosterChanged();
            return true;
        }

        public bool LevelUp(int index)
        {
            if (index < 0 || index >= 6 || Save.levels[index] < 1 || IsMaxLevel(index) || Save.coins < LevelCost(index)) return false;
            Save.coins -= LevelCost(index);
            Save.levels[index]++;
            RosterChanged();
            return true;
        }

        public bool ToggleParty(int index)
        {
            if (index < 0 || index >= 6 || Save.levels[index] == 0) return false;
            if (Save.party[index] ? PartyCount <= 1 : PartyCount >= 3) return false;
            // A healer alone cannot clear a round.
            if (Save.party[index] && index != 4 && PartyCount == 2 && Save.party[4]) return false;
            Save.party[index] = !Save.party[index];
            RosterChanged();
            return true;
        }

        private void RosterChanged()
        {
            // Changing members must not heal the party or resurrect a lost attempt.
            for (var i = 0; i < 6; i++) _health[i] = Math.Min(_health[i], MaxHealth(i));
            Changed?.Invoke();
        }

        public void Tick(double seconds, bool working)
        {
            if (!working || seconds <= 0 || double.IsNaN(seconds) || double.IsInfinity(seconds)) return;
            // No offline/background catch-up burst. Each call represents at most one second of live work.
            seconds = Math.Min(seconds, 1);
            CombatTime += seconds;
            if (RoundBannerRemaining > 0) RoundBannerRemaining = Math.Max(0, RoundBannerRemaining - seconds);
            for (var i = Numbers.Count - 1; i >= 0; i--) { Numbers[i].age += seconds; if (Numbers[i].age >= .9) Numbers.RemoveAt(i); }
            if (OutcomeRemaining > 0) { OutcomeRemaining = Math.Max(0, OutcomeRemaining - seconds); if (OutcomeRemaining == 0) Outcome = 0; if (RetryRemaining == 0) return; }
            if (RetryRemaining > 0)
            {
                RetryRemaining = Math.Max(0, RetryRemaining - seconds);
                if (RetryRemaining == 0) { FallBack(); ResetBattle(); }
                return;
            }
            if (_announce) { _announce = false; RoundBannerRemaining = RoundBannerSeconds; }
            _beat += seconds;
            if (_beat < 1) return;
            _beat -= 1;
            for (var i = 0; i < 6; i++)
            {
                if (!Save.party[i] || IsDown(i)) continue;
                if (i == 4)
                {
                    // Heals every living member, herself included. No resurrection, and no stalemate once all attackers fall.
                    var attackerAlive = false;
                    for (var ally = 0; ally < 6; ally++) if (ally != 4 && Save.party[ally] && _health[ally] > 0) attackerAlive = true;
                    if (!attackerAlive) continue;
                    for (var ally = 0; ally < 6; ally++)
                    {
                        if (!Save.party[ally] || _health[ally] <= 0) continue;
                        var heal = Math.Min(Power(i), MaxHealth(ally) - _health[ally]);
                        if (heal <= 0) continue;
                        _health[ally] += heal;
                        Numbers.Add(new CombatNumber { actor = i, target = ally, amount = heal, healing = true });
                    }
                }
                else { var damage = Math.Min(EnemyHp, Power(i)); EnemyHp -= damage; if (damage > 0) Numbers.Add(new CombatNumber { actor = i, amount = damage }); }
            }
            if (EnemyHp == 0)
            {
                var reward = Reward(Save.round);
                var coinsBeforeReward = Save.coins;
                Save.coins = Save.coins > long.MaxValue - reward ? long.MaxValue : Save.coins + reward;
                LastClearReward = Save.coins - coinsBeforeReward;
                Save.bestClear = Math.Max(Save.bestClear, Save.round);
                // The final round repeats after a clear.
                if (Save.round < Balance.maxRound) Save.maxRound = Math.Max(Save.maxRound, Save.round + 1);
                if (Save.autoAdvance && Save.round < Balance.maxRound) Save.round++;
                ResetBattle();
                Outcome = 1; OutcomeRemaining = 1;
                Changed?.Invoke();
                return;
            }
            var front = FrontMember;
            if (front < 0) return;
            var received = Math.Min(_health[front], EnemyAttack);
            _health[front] -= received;
            Numbers.Add(new CombatNumber { target = front, amount = received, enemy = true });
            if (PartyHp == 0) { RetryRemaining = Balance.retrySeconds; Outcome = -1; OutcomeRemaining = Balance.retrySeconds; }
        }

        /// <summary>A lost fight drops back one round and repeats it, so idle time keeps earning coins.</summary>
        private void FallBack()
        {
            if (Save.round <= 1) return;
            Save.round--;
            Save.autoAdvance = false;
            Changed?.Invoke();
        }

        private void ResetBattle()
        {
            _announce = true;
            EnemyHp = EnemyMaxHp;
            for (var i = 0; i < 6; i++) _health[i] = Save.levels[i] > 0 ? MaxHealth(i) : 0;
            RetryRemaining = 0;
            _beat = 0;
        }
    }
}
