using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace SemesterProjekt1
{
    public class Deck
    {
        private const int MaxCards = 20;
        public List<Card> Cards { get; set; }

        [JsonConstructor]
        public Deck(List<Card> Cards)
        {
            this.Cards = Cards ?? new List<Card>();
        }

        public Deck()
        {
            Cards = new List<Card>();
        }

        public bool AddCard(Card card)
        {
            if (Cards.Count >= MaxCards)
            {
                Console.WriteLine("Deck is full.");
                return false;
            }

            if (Cards.Any(c => c.Name == card.Name))
            {
                Console.WriteLine("Card already in deck.");
                return false;
            }

            Cards.Add(card);
            return true;
        }

        public void RemoveCard(Card card)
        {
            Cards.Remove(card);
        }
    }
}