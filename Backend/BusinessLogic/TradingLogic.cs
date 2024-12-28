using System;

namespace SemesterProjekt1
{
    public class TradingLogic
    {
        public Guid Id { get; set; }
        public long CardToTrade { get; set; }
        public CardType Type { get; set; }
        public int MinimumDamage { get; set; }
        public int UserId { get; set; }

        public TradingLogic(Guid id, long cardToTrade, CardType type, int minimumDamage, int userId)
        {
            Id = id;
            CardToTrade = cardToTrade;
            Type = type;
            MinimumDamage = minimumDamage;
            UserId = userId;
        }
    }
}