using System.Web;
using System.Security.Cryptography;

namespace SemesterProjekt1
{
    public class HTMLGEN
    {
        public UserServiceHandler _userServiceHandler;

        public HTMLGEN(UserServiceHandler _userServiceHandler)
        {
            this._userServiceHandler = _userServiceHandler;
        }

        private static string GenerateNonce()
        {
            byte[] nonceBytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(nonceBytes);
            }
            return Convert.ToBase64String(nonceBytes);
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
            string nonce = GenerateNonce();
            string loginForm = $@"
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

            SendResponse(stream, loginForm, "text/html", nonce);
        }

        public string GenerateOptionsPage(int size)
        {
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

        public (string html, string nonce) GenerateInventoryHtml(Inventory inventory)
        {
            if (inventory == null)
                return (string.Empty, null);

            string nonce = GenerateNonce();
            string html = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>Inventory</title>
            <style nonce='{nonce}'>
                .header-buttons {{
                    display: flex;
                    justify-content: space-between;
                    margin-bottom: 20px;
                }}
                .profile-button {{
                    background-color: #2196F3;
                    color: white;
                    padding: 10px 20px;
                    border: none;
                    border-radius: 4px;
                    cursor: pointer;
                    text-decoration: none;
                    display: inline-block;
                }}
                .profile-button:hover {{
                    background-color: #0b7dda;
                }}
            </style>
        </head>
        <body>
            <div class='header-buttons'>
                <h1>Inventory</h1>
                <a href='/profile' class='profile-button'>Profile Settings</a>
            </div>
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
                <input type='submit' value='Open Card Pack'>
            </form>
            <form method='post' action='/inventory'>
                <label for='Amount'>Amount:</label>
                <input type='number' id='Amount' name='Amount' min='1' max='100' required><br>
                <input type='submit' value='Buy Card Pack'>
            </form>
            <form id='joinLobbyForm' method='post' action='/battles'>
                <input type='submit' value='Lobby beitreten'>
            </form>

            <form method='get' action='/logout'>
                <button type='submit'>Logout</button>
            </form>

        </body>
        </html>";
            return (html, nonce);
        }

        public (string html, string nonce) GenerateProfilePage(User user)
        {
            if (user == null)
                return (string.Empty, null);

            string nonce = GenerateNonce();
            string html = $@"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>User Profile</title>
            <style nonce='{nonce}'>
                body {{
                    font-family: Arial, sans-serif;
                    max-width: 600px;
                    margin: 50px auto;
                    padding: 20px;
                }}
                .form-group {{
                    margin-bottom: 15px;
                }}
                label {{
                    display: block;
                    margin-bottom: 5px;
                    font-weight: bold;
                }}
                input[type='text'], textarea {{
                    width: 100%;
                    padding: 8px;
                    border: 1px solid #ddd;
                    border-radius: 4px;
                    box-sizing: border-box;
                }}
                textarea {{
                    min-height: 100px;
                    resize: vertical;
                }}
                button {{
                    background-color: #4CAF50;
                    color: white;
                    padding: 10px 20px;
                    border: none;
                    border-radius: 4px;
                    cursor: pointer;
                    margin-right: 10px;
                }}
                button:hover {{
                    background-color: #45a049;
                }}
                .back-button {{
                    background-color: #808080;
                }}
                .back-button:hover {{
                    background-color: #696969;
                }}
                .info {{
                    background-color: #f0f0f0;
                    padding: 10px;
                    border-radius: 4px;
                    margin-bottom: 20px;
                }}
                .inline-form {{
                    display: inline;
                }}
            </style>
        </head>
        <body>
            <h1>Profile Settings</h1>
            <div class='info'>
                <p><strong>Username:</strong> {HtmlEncode(user.Username)}</p>
                <p><strong>User ID:</strong> {HtmlEncode(user.Id.ToString())}</p>
                <p><strong>ELO:</strong> {HtmlEncode(user.Inventory?.ELO.ToString() ?? "0")}</p>
            </div>

            <form id='profileForm'>
                <div class='form-group'>
                    <label for='name'>Display Name:</label>
                    <input type='text' id='name' name='Name' maxlength='50' value='{HtmlEncode(user.Name)}' required>
                </div>

                <div class='form-group'>
                    <label for='bio'>Bio:</label>
                    <textarea id='bio' name='Bio' maxlength='500'>{HtmlEncode(user.Bio)}</textarea>
                </div>

                <button type='submit'>Save Changes</button>
            </form>

            <form method='get' action='/inventory' class='inline-form'>
                <button type='submit' class='back-button'>Back to Inventory</button>
            </form>

            <script nonce='{nonce}'>
                document.addEventListener('DOMContentLoaded', function() {{
                    const profileForm = document.getElementById('profileForm');

                    profileForm.addEventListener('submit', async function(event) {{
                        event.preventDefault();

                        const formData = new FormData(profileForm);

                        const data = {{
                            Name: formData.get('Name'),
                            Bio: formData.get('Bio')
                        }};

                        try {{
                            const response = await fetch('/users/{HtmlEncode(user.Username)}', {{
                                method: 'PUT',
                                headers: {{
                                    'Content-Type': 'application/json'
                                }},
                                body: JSON.stringify(data),
                                credentials: 'include'
                            }});

                            if (response.ok) {{
                                alert('Profile updated successfully!');
                                window.location.href = '/profile';
                            }} else {{
                                const errorText = await response.text();
                                alert('Failed to update profile: ' + errorText);
                            }}
                        }} catch (error) {{
                            console.error('Error:', error);
                            alert('An error occurred while updating profile');
                        }}
                    }});
                }});
            </script>
        </body>
        </html>";

            return (html, nonce);
        }

        public void SendResponse(StreamWriter stream, string content, string contentType, string nonce = null)
        {
            string csp = nonce != null
                ? $"default-src 'self'; style-src 'self' 'nonce-{nonce}'; script-src 'self' 'nonce-{nonce}'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'; connect-src 'self'"
                : "default-src 'self'; style-src 'self'; script-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'self'; connect-src 'self'";

            string response = $"HTTP/1.1 200 OK\r\n" +
                            $"Content-Type: {contentType}\r\n" +
                            $"Content-Length: {content.Length}\r\n" +
                            $"X-Content-Type-Options: nosniff\r\n" +
                            $"X-Frame-Options: DENY\r\n" +
                            $"X-XSS-Protection: 1; mode=block\r\n" +
                            $"Content-Security-Policy: {csp}\r\n" +
                            $"Strict-Transport-Security: max-age=31536000; includeSubDomains; preload\r\n" +
                            $"Referrer-Policy: strict-origin-when-cross-origin\r\n" +
                            $"Permissions-Policy: geolocation=(), microphone=(), camera=()\r\n" +
                            $"\r\n{content}";
            stream.Write(response);
            stream.Flush();
        }
    }
}