using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace DOTORION.Supabase
{
    // Optional event backend extension; the existing roster query does not depend on the event schema.
    public sealed partial class SupabaseTeamBackend
    {
        /// <summary>Each teammate's current round and the furthest round they have reached.</summary>
        public async Task<Dictionary<string, (int current, int best)>> GetSubjugationRoundsAsync(CancellationToken cancellationToken)
        {
            var response = await SendAuthorizedAsync("GET",
                _projectUrl + "/rest/v1/chiikawa_event_rounds?select=member_id,round,max_round", null, cancellationToken);
            var document = JsonUtility.FromJson<SubjugationRoundList>("{\"items\":" + response.Body + "}");
            var rounds = new Dictionary<string, (int current, int best)>();
            if (document?.items != null) foreach (var row in document.items)
                if (!string.IsNullOrEmpty(row.member_id) && row.round > 0) rounds[row.member_id] = (row.round, Math.Max(row.round, row.max_round));
            return rounds;
        }

        public async Task SetSubjugationRoundAsync(int round, int best, CancellationToken cancellationToken)
        {
            await CallRpcAsync("set_chiikawa_event_round",
                JsonUtility.ToJson(new SubjugationRoundRequest { p_round = round, p_max_round = Math.Max(round, best) }), cancellationToken);
        }

        public async Task<int[]> GetSubjugationClaimedRewardsAsync(CancellationToken cancellationToken)
        {
            var response = await CallRpcAsync("chiikawa_event_claimed_rewards", "{}", cancellationToken);
            var document = JsonUtility.FromJson<SubjugationClaimList>("{\"items\":" + response.Body + "}");
            var claimed = new List<int>();
            if (document?.items != null) foreach (var row in document.items) claimed.Add(row.milestone);
            return claimed.ToArray();
        }

        /// <summary>Returns the points this call awarded: zero when the milestone was already paid.</summary>
        public async Task<int> ClaimSubjugationRewardAsync(int milestone, int cleared, CancellationToken cancellationToken)
        {
            var response = await CallRpcAsync("claim_chiikawa_event_reward",
                JsonUtility.ToJson(new SubjugationClaimRequest { p_milestone = milestone, p_cleared = cleared }), cancellationToken);
            var document = JsonUtility.FromJson<SubjugationClaimResultList>("{\"items\":" + response.Body + "}");
            return document?.items != null && document.items.Length > 0 ? document.items[0].awarded : 0;
        }

        /// <summary>Developer reset: the whole team's event rows for an admin, otherwise the caller's own.</summary>
        public async Task ResetSubjugationAsync(CancellationToken cancellationToken)
        {
            await CallRpcAsync("reset_chiikawa_event", "{}", cancellationToken);
        }

        [Serializable] private sealed class SubjugationRoundRequest { public int p_round; public int p_max_round; }
        [Serializable] private sealed class SubjugationRoundRow { public string member_id; public int round; public int max_round; }
        [Serializable] private sealed class SubjugationRoundList { public SubjugationRoundRow[] items; }
        [Serializable] private sealed class SubjugationClaimRequest { public int p_milestone; public int p_cleared; }
        [Serializable] private sealed class SubjugationClaimRow { public int milestone; }
        [Serializable] private sealed class SubjugationClaimList { public SubjugationClaimRow[] items; }
        [Serializable] private sealed class SubjugationClaimResult { public bool claimed; public int awarded; public int total_points; }
        [Serializable] private sealed class SubjugationClaimResultList { public SubjugationClaimResult[] items; }
    }
}
