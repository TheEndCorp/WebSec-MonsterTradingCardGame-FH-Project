using System.Net.WebSockets;
using System.Text;

namespace SemesterProjekt1
{
    public class FightLogic : CardTypes
    {
        private List<Card> player1Deck;
        private List<Card> player2Deck;
        private Random random;
        public StringBuilder battleLog { get; set; }
        public User User1;
        public User User2;

        public FightLogic(User User1, User User2)
        {
            this.User1 = User1;
            this.User2 = User2;
            this.player1Deck = User1.Inventory.Deck.Cards;
            this.player2Deck = User2.Inventory.Deck.Cards;
            this.random = new Random();
            this.battleLog = new StringBuilder();
        }

        private async Task SendBattleLogAsync(WebSocket player1Socket, WebSocket player2Socket, string log)
        {
            var buffer = Encoding.UTF8.GetBytes(log);
            var segment = new ArraySegment<byte>(buffer);

            if (player1Socket.State == WebSocketState.Open)
            {
                await player1Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            if (player2Socket.State == WebSocketState.Open)
            {
                await player2Socket.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
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

        public async Task StartBattleAsync(WebSocket player1Socket, WebSocket player2Socket)
        {
            int round = 0;
            StringBuilder battleLog = new StringBuilder();

            while (round < 100 && player1Deck.Count > 0 && player2Deck.Count > 0)
            {
                Card player1Card = player1Deck[random.Next(player1Deck.Count)];
                Card player2Card = player2Deck[random.Next(player2Deck.Count)];

                battleLog.AppendLine($"Runde {round + 1}: {player1Card.Name} vs {player2Card.Name}");

                int player1Damage = CalculateDamage(player1Card, player2Card);
                int player2Damage = CalculateDamage(player2Card, player1Card);

                if (player1Damage > player2Damage)
                {
                    battleLog.AppendLine($"{player1Card.Name} gewinnt die Runde!");
                    player1Deck.Add(player2Card);
                    player2Deck.Remove(player2Card);
                }
                else if (player2Damage > player1Damage)
                {
                    battleLog.AppendLine($"{player2Card.Name} gewinnt die Runde!");
                    player2Deck.Add(player1Card);
                    player1Deck.Remove(player1Card);
                }
                else
                {
                    battleLog.AppendLine("Unentschieden!");
                }

                round++;
                await SendBattleLogAsync(player1Socket, player2Socket, battleLog.ToString());
                battleLog.Clear();
            }

            battleLog.AppendLine("Kampf beendet!");

            if (player1Deck.Count > player2Deck.Count)
            {
                battleLog.AppendLine($"Spieler 1 ({User1.Username}) gewinnt den Kampf!");
                User1.Inventory.ELO += 3;
                User2.Inventory.ELO -= 5;
            }
            else if (player2Deck.Count > player1Deck.Count)
            {
                battleLog.AppendLine($"Spieler 2 ({User2.Username}) gewinnt den Kampf!");
                User1.Inventory.ELO -= 5;
                User2.Inventory.ELO += 3;
            }
            else
            {
                battleLog.AppendLine("Der Kampf endet unentschieden!");
            }

            await SendBattleLogAsync(player1Socket, player2Socket, battleLog.ToString());
        }

        public async Task<Dictionary<string, string>> StartBattleAsync()
        {
            int round = 0;
            StringBuilder battleLog = new StringBuilder();
            Dictionary<string, string> result = new Dictionary<string, string>();

            while (round < 100 && player1Deck.Count > 0 && player2Deck.Count > 0)
            {
                Card player1Card = player1Deck[random.Next(player1Deck.Count)];
                Card player2Card = player2Deck[random.Next(player2Deck.Count)];

                battleLog.AppendLine($"Runde {round + 1}: {player1Card.Name} vs {player2Card.Name}");

                int player1Damage = CalculateDamage(player1Card, player2Card);
                int player2Damage = CalculateDamage(player2Card, player1Card);

                if (player1Damage > player2Damage)
                {
                    battleLog.AppendLine($"{player1Card.Name} gewinnt die Runde!");
                    player1Deck.Add(player2Card);
                    player2Deck.Remove(player2Card);
                }
                else if (player2Damage > player1Damage)
                {
                    battleLog.AppendLine($"{player2Card.Name} gewinnt die Runde!");
                    player2Deck.Add(player1Card);
                    player1Deck.Remove(player1Card);
                }
                else
                {
                    battleLog.AppendLine("Unentschieden!");
                }

                round++;
            }

            battleLog.AppendLine("Kampf beendet!");

            if (player1Deck.Count > player2Deck.Count)
            {
                battleLog.AppendLine($"Spieler 1 ({User1.Username}) gewinnt den Kampf!");
                result["winner"] = User1.Username;
            }
            else if (player2Deck.Count > player1Deck.Count)
            {
                battleLog.AppendLine($"Spieler 2 ({User2.Username}) gewinnt den Kampf!");
                result["winner"] = User2.Username;
            }
            else
            {
                battleLog.AppendLine("Der Kampf endet unentschieden!");
                result["winner"] = "draw";
            }

            result["log"] = battleLog.ToString();
            return result;
        }
    }
}