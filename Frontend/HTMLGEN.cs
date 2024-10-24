using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SemesterProjekt1
{
    public class HTMLGEN 
    {
        public UserServiceHandler _userServiceHandler;

        public HTMLGEN(UserServiceHandler _userServiceHandler)
        {
            this._userServiceHandler = _userServiceHandler;
        }

        public void SendLoginPage(HttpListenerResponse response)
        {
            string loginForm = @"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <title>Login</title>
            </head>
            <body>
                <h1>Login</h1>
                <form method='post' action='/login'>
                    <label for='username'>Username:</label>
                    <input type='text' id='username' name='username'><br>
                    <label for='password'>Password:</label>
                    <input type='password' id='password' name='password'><br>
                    <input type='submit' value='Login'>
                </form>
            </body>
            </html>";

            SendResponse(response, loginForm, "text/html");
        }



        public string GenerateOptionsPage(int size)
        {
            string htmlResponse = @"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <title>Options</title>
            </head>
            <body>
                <h1>Options</h1>
                <button onclick='window.location.href=""/users"";'>Show All Users</button>
                <button onclick='window.location.href=""/login"";'>Login</button>";

            for (int i = 1; i <= size; i++)
            {
                htmlResponse += $@"<button onclick='window.location.href=""/user/{i}"";'>Show User with ID {i}</button>";
            }

            htmlResponse += "</body></html>";
            return htmlResponse;
        }

        public string GenerateInventoryHtml(Inventory inventory)
        {
            string html = $@"  
           <!DOCTYPE html>  
           <html lang='en'>  
           <head>  
               <meta charset='UTF-8'>  
               <title>Inventory</title>  
           </head>  
           <body>  
               <h1>Inventory</h1>  
               <h2>Owned Cards</h2>  
               <form method='post' action='/add-card-to-deck'>  
                   <ul>";

            foreach (var card in inventory.OwnedCards)
            {
                int cardIndex = inventory.OwnedCards.IndexOf(card);
                html += $@"  
                   <li>  
                       <input type='checkbox' name='cardIndices' value='{cardIndex}'>  
                       {cardIndex} : {card.Name} - {card.Damage} Damage - {card.Element} - {card.Type}  
                   </li>";
            }

            html += $@"  
                   </ul>  
                   <input type='hidden' name='userID' value='{inventory.UserID}' />  
                   <input type='submit' value='Save to Deck'>  
               </form>  
               <h2>Cards in Deck</h2>  
               <ul>";

            foreach (var card in inventory.Deck.Cards)
            {
                html += $@"  
                   <li>  
                       {card.Name} - {card.Damage} Damage - {card.Element} - {card.Type}  
                   </li>";
            }

            html += $@"  
               </ul>  
               <h2>Money: {inventory.Money}</h2>  
               <form method='post' action='/openpack'>  
                   <input type='hidden' name='userID' value='{inventory.UserID}' />  
                   <input type='submit' value='Open Card Pack'>  
               </form>  
               <form method='post' action='/inventory'>  
                   <input type='hidden' name='userID' value='{inventory.UserID}' />  
                   <label for='Amount'>Amount:</label>  
                   <input type='number' id='Amount' name='Amount' required><br>  
                   <input type='submit' value='Buy Card Pack'>  
               </form>  
               <form id='joinLobbyForm' method='post' action='/join-lobby'>  
                   <input type='hidden' name='userID' value='{inventory.UserID}' />  
                   <input type='submit' value='Lobby beitreten'>  
               </form>  
               <form method='post' action='/logout'>  
                   <input type='submit' value='Logout'>  
               </form>  
           </body>  
           </html>";
            return html;
        }

        public void SendLobbyPage(HttpListenerRequest request, HttpListenerResponse response)
        {
            var userDataCookie = request.Cookies["userData"]?.Value;
            if (userDataCookie != null)
            {
                var userData = System.Web.HttpUtility.ParseQueryString(userDataCookie);
                string username = userData["username"];
                string password = userData["password"];
                string userIdString = userData["userid"];
                int userId = int.Parse(userIdString);

                var user = _userServiceHandler.GetUserById(userId); ;
                if (user != null)
                {
                    string lobbyPage = $@"
                    <!DOCTYPE html>
                    <html lang='de'>
                    <head>
                        <meta charset='UTF-8'>
                        <title>Lobby</title>
                    </head>
                    <body>
                        <h1>Lobby</h1>
                        <p>ID: {user.Inventory.UserID}</p>
                        <form id='joinLobbyForm' method='post' action='/join-lobby'>
                            <input type='hidden' name='userID' value='{userId}' />
                            <input type='submit' value='Lobby beitreten'>
                        </form>
                    </body>
                    </html>";

                    SendResponse(response, lobbyPage, "text/html");
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.Unauthorized;
                    SendResponse(response, "Unauthorized access. Please log in.", "text/plain");
                }
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.Unauthorized;
                SendResponse(response, "Unauthorized access. Please log in.", "text/plain");
            }
        }

        public void SendResponse(HttpListenerResponse response, string content, string contentType)
        {
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(content);
            response.ContentLength64 = buffer.Length;
            response.ContentType = contentType;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }

    }







}
