# SemesterProjekt1

## Overview
SemesterProjekt1 is a C# project developed as part of a semester project. The project aims to create an application in C# to spawn a REST-based (HTTP) server that acts as an API for console. The frontend is an additional part of this project

## Features
- A user is a registered player with credentials (unique username, password).
- A user can manage his cards.
- A card consists of: a name and multiple attributes (damage, element type).
- A card is either a spell-card or a monster-card.
- The damage of a card is constant and does not change.
- A user has multiple cards in his stack.
- A stack is the collection of all his current cards.
- A user can buy cards by acquiring packages.
- A package consists of cards and can be acquired from the server by paying 5 virtual coins.
- Every user has 20 coins to buy 4 packages.
- The best 4 cards are selected by the user to be used in the deck.
- The deck is used in the battles against other players.
- A battle is a request to the server to compete against another user with your currently defined deck.

Users can:
- register and login to the server,
- acquire some cards,
- define a deck of monsters/spells,
- battle against another user
- compare their stats in the score-board.
- trade cards to have better chances to win (see detail description below).

Further Features:
- display a scoreboard (= sorted list of ELO values)
- (FUTURE FEATURE)  editable profile page
- user stats – especially ELO calculation (+3 points for win, -5 for loss, starting value: 100;
higher sophisticated ELO system welcome)
- security check (using the token that is retrieved at login on the server-side, so that user
actions can only be performed by the corresponding user itself)


## Installation
To clone and run this application, you'll need Visual Studio:
•	Visual Studio 2022 oder höher
•	.NET 8 SDK

Abhängigkeiten installieren
1.	Öffnen Sie das Projekt in Visual Studio.
2.	Visual Studio wird automatisch die Abhängigkeiten aus der .csproj-Datei herunterladen und installieren. Dies umfasst:
•	MSTest.TestAdapter
•	MSTest.TestFramework
•	Newtonsoft.Json
Projekt ausführen
1.	Stellen Sie sicher, dass das Projekt erfolgreich geladen wurde und alle Abhängigkeiten installiert sind.
2.	Wählen Sie im Visual Studio die SemesterProjekt1-Projektmappe aus.
3.	Klicken Sie auf Start oder drücken Sie F5, um das Projekt auszuführen.


# Clone this repository
git clone https://github.com/MarkoEMW/SemesterProjekt1.git

# Go into the repository
cd SemesterProjekt1

# Install dependencies (if any)
Json.NET Newtonsoft

# License
This project is licensed under the MIT License - see the LICENSE file for details.

# Contact
Created by MarkoEMW - feel free to contact me!
