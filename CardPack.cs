using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemesterProjekt1
{
    public class CardPack : CardTypes
    {
        private Rarity _Rarity;
        private int _UserID;
        private int _Price = 5;



        private Rarity GenerateRandomRarity()
        {
            var rarities = Enum.GetValues(typeof(Rarity));
            return (Rarity)(rarities.GetValue(random.Next(rarities.Length)) ?? Rarity.Common);
        }


        public CardPack(int userID)
        {
            _UserID = userID;
            _Rarity = GenerateRandomRarity();
        }


        public List<Card> OpenCardPack(int Userid)
        {
            var cards = GenerateCards(5 * ((int)_Rarity / 2), Userid);
            return cards;

        }


        private List<Card> GenerateCards(int numberOfCards, int Userid)
        {
            var cards = new List<Card>();
            for (int i = 0; i < numberOfCards; i++)
            {
                cards.Add(GenerateRandomCard(Userid));
            }
            return cards;
        }

        private static Random random = new Random();

        private Card GenerateRandomCard(int userID)
        {




            var element = (ElementType)random.Next(0, Enum.GetValues(typeof(ElementType)).Length);
            var type = (CardType)random.Next(0, Enum.GetValues(typeof(CardType)).Length);
            var rarity = (Rarity)random.Next(1, Enum.GetValues(typeof(Rarity)).Length);
            int damage = random.Next(1, 10);

            if (type == CardType.Monster)
            {
                string name = MonsterNames[random.Next(0, MonsterNames.Length)];
                if (name == "FireElf")
                {
                    element = ElementType.Fire;
                }

                return new MonsterCard(name, damage, element, rarity, userID);
            }
            else
            {
                string name = SpellNames[random.Next(0, SpellNames.Length)];

                return new SpellCard(name, damage, element, rarity, userID);
            }
        }
    }
}
