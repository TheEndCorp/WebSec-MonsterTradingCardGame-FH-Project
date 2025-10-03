using System.Web;

namespace SemesterProjekt1
{
    public class HTMLGEN
    {
        public UserServiceHandler _userServiceHandler;

        public HTMLGEN(UserServiceHandler _userServiceHandler)
        {
            this._userServiceHandler = _userServiceHandler;
        }

        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&#39;");
        }

        public void SendLoginPage(StreamWriter stream)
        {
            string loginForm = @"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Login</title>
                </head>
                <body>
                    <h1>Login</h1>
                    <form method='post' action='/login'>
                        <label for='username'>Username:</label>
                        <input type='text' id='username' name='username' maxlength='20' required><br>
                        <label for='password'>Password:</label>
                        <input type='password' id='password' name='password' maxlength='100' required><br>
                        <input type='submit' value='Login'>
                    </form>
                </body>
                </html>";

            SendResponse(stream, loginForm, "text/html");
        }

        public string GenerateOptionsPage(int size)
        {
            // Validierung der Eingabe
            if (size < 0 || size > 1000)
                size = 0;

            string htmlResponse = @"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Options</title>
        </head>
        <body>
            <h1>Options</h1>
            <form method='get' action='/users'>
                <button type='submit'>Show All Users</button>
            </form>
            <form method='get' action='/login'>
                <button type='submit'>Login</button>
            </form>";

            for (int i = 0; i < size; i++)
            {
                htmlResponse += $@"
            <form method='get' action='/user/{i}'>
                <button type='submit'>Show User with ID {HtmlEncode(i.ToString())}</button>
            </form>";
            }

            htmlResponse += @"
        </body>
        </html>";
            return htmlResponse;
        }

        public string GenerateInventoryHtml(Inventory inventory)
        {
            if (inventory == null)
                return string.Empty;

            string html = $@"
                <!DOCTYPE html>
                <html lang='en'>
                <head>
                    <meta charset='UTF-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                    <title>Inventory</title>
                </head>
                <body>
                    <h1>Inventory</h1>
                    <h2>Manage Your Cards</h2>
                    <form method='post' action='/add-card-to-deck'>
                        <h3>All Cards</h3>
                        <ul>";

            foreach (var card in inventory.OwnedCards)
            {
                int cardIndex = inventory.OwnedCards.IndexOf(card);
                bool isInDeck = card.InDeck;
                html += $@"
                        <li>
                            <input type='checkbox' name='cardIndices' value='{cardIndex}' {(isInDeck ? "checked" : "")}>
                            {HtmlEncode(cardIndex.ToString())} : {HtmlEncode(card.Name)} - {HtmlEncode(card.Damage.ToString())} Damage - {HtmlEncode(card.Element.ToString())} - {HtmlEncode(card.Type.ToString())} {(isInDeck ? "(In Deck)" : "")}
                        </li>";
            }

            html += $@"
                        </ul>
                        <input type='hidden' name='userID' value='{inventory.UserID}' />
                        <input type='submit' value='Save to Deck'>
                    </form>
                    <h2>Cards in Deck ({inventory.Deck.Cards.Count}/20)</h2>
                    <ul>";

            foreach (var card in inventory.Deck.Cards)
            {
                html += $@"
                        <li>
                            {HtmlEncode(card.Name)} - {HtmlEncode(card.Damage.ToString())} Damage - {HtmlEncode(card.Element.ToString())} - {HtmlEncode(card.Type.ToString())}
                        </li>";
            }

            html += $@"
                    </ul>
                    <h2>Money: {HtmlEncode(inventory.Money.ToString())}</h2>
                    <form method='post' action='/openpack'>
                        <input type='hidden' name='userID' value='{inventory.UserID}' />
                        <input type='submit' value='Open Card Pack'>
                    </form>
                    <form method='post' action='/inventory'>
                        <input type='hidden' name='userID' value='{inventory.UserID}' />
                        <label for='Amount'>Amount:</label>
                        <input type='number' id='Amount' name='Amount' min='1' max='100' required><br>
                        <input type='submit' value='Buy Card Pack'>
                    </form>
                    <form id='joinLobbyForm' method='post' action='/battles'>
                        <input type='hidden' name='userID' value='{inventory.UserID}' />
                        <input type='submit' value='Lobby beitreten'>
                    </form>

                    <form method='get' action='/logout'>
                        <button type='submit'>Logout</button>
                    </form>

                </body>
                </html>";
            return html;
        }

        public void SendResponse(StreamWriter stream, string content, string contentType)
        {
            string response = $"HTTP/1.1 200 OK\r\n" +
                            $"Content-Type: {contentType}\r\n" +
                            $"Content-Length: {content.Length}\r\n" +
                            $"X-Content-Type-Options: nosniff\r\n" +
                            $"X-Frame-Options: DENY\r\n" +
                            $"X-XSS-Protection: 1; mode=block\r\n" +
                            $"Content-Security-Policy: default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; form-action 'self'\r\n" +
                            $"Strict-Transport-Security: max-age=31536000; includeSubDomains\r\n" +
                            $"\r\n{content}";
            stream.Write(response);
            stream.Flush();
        }
    }
}