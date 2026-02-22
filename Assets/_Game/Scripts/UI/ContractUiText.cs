using FantasyGuildmaster.Map;

namespace FantasyGuildmaster.UI
{
    public static class ContractUiText
    {
        public static string FormatContractReq(ContractData contract)
        {
            if (contract == null)
            {
                return "BOTH • Rank E";
            }

            var party = contract.allowSolo && contract.allowSquad
                ? "BOTH"
                : (contract.allowSolo
                    ? "SOLO"
                    : (contract.allowSquad ? "SQUAD" : "NONE"));
            return $"{party} • Rank {contract.minRank}";
        }

        public static string FormatBlockedReasonNoEligible(ContractData contract)
        {
            return "No eligible party";
        }
    }
}
