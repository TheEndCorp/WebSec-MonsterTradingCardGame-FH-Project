using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SemesterProjekt1.CardTypes;

namespace SemesterProjekt1
{
    public class FightLogic : CardTypes
    {
        private List<Card> player1Deck;
        private List<Card> player2Deck;
        private Random random;

        public FightLogic(List<Card> player1Deck, List<Card> player2Deck)
        {
            this.player1Deck = player1Deck;
            this.player2Deck = player2Deck;
            this.random = new Random();
        }

        public void StartBattle()
        {
            int round = 0;
            while (round < 100 && player1Deck.Count > 0 && player2Deck.Count > 0)
            {
                Card player1Card = player1Deck[random.Next(player1Deck.Count)];
                Card player2Card = player2Deck[random.Next(player2Deck.Count)];

                Console.WriteLine($"Runde {round + 1}: {player1Card.Name} vs {player2Card.Name}");

                int player1Damage = CalculateDamage(player1Card, player2Card);
                int player2Damage = CalculateDamage(player2Card, player1Card);

                if (player1Damage > player2Damage)
                {
                    Console.WriteLine($"{player1Card.Name} gewinnt die Runde!");
                    player1Deck.Add(player2Card);
                    player2Deck.Remove(player2Card);
                }
                else if (player2Damage > player1Damage)
                {
                    Console.WriteLine($"{player2Card.Name} gewinnt die Runde!");
                    player2Deck.Add(player1Card);
                    player1Deck.Remove(player1Card);
                }
                else
                {
                    Console.WriteLine("Unentschieden!");
                }

                round++;
            }

            Console.WriteLine("Kampf beendet!");

            if (player1Deck.Count > player2Deck.Count)
            {
                Console.WriteLine("Spieler 1 gewinnt den Kampf!");
            }
            else if (player2Deck.Count > player1Deck.Count)
            {
                Console.WriteLine("Spieler 2 gewinnt den Kampf!");
            }
            else
            {
                Console.WriteLine("Der Kampf endet unentschieden!");
            }
        }

        private int CalculateDamage(Card attacker, Card defender)
        {
            int damage = attacker.Damage;

            if (attacker is SpellCard || defender is SpellCard)
            {
                if (attacker.Element == ElementType.Water && defender.Element == ElementType.Fire ||
                    attacker.Element == ElementType.Fire && defender.Element == ElementType.Normal ||
                    attacker.Element == ElementType.Normal && defender.Element == ElementType.Water)
                {
                    damage *= 2;
                }
                else if (attacker.Element == ElementType.Fire && defender.Element == ElementType.Water ||
                         attacker.Element == ElementType.Normal && defender.Element == ElementType.Fire ||
                         attacker.Element == ElementType.Water && defender.Element == ElementType.Normal)
                {
                    damage /= 2;
                }
            }

       
            if (attacker.Name == "Goblin" && defender.Name == "Dragon")
            {
                damage = 0;
            }
            else if (attacker.Name == "Wizzard" && defender.Name == "Ork")
            {
                damage = 0;
            }
            else if (attacker.Name == "Knight" && defender is SpellCard && defender.Element == ElementType.Water)
            {
                damage = int.MaxValue; // Sofortige Niederlage
            }
            else if (attacker.Name == "Kraken" && defender is SpellCard)
            {
                damage = 0; // Immun gegen Zauber
            }
            else if (attacker.Name == "FireElf" && defender.Name == "Dragon")
            {
                damage = 0; // Weicht Angriffen aus
            }

            return damage;
        }
    }
}

