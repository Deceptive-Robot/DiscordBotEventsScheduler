# Discord Events Scheduling Bot

This repository creates a C# based discord bot that allows users to create custom possible game events and vote on them.
It also allows users the ability to vote on possible times that they would like the game event to happen.
Votes happen via emoji reactions in discord.

## Usage / Commands
Two text channels should be setup in discord:
- An output channel where the bot will announce voting messages as well as reminders about scheduled events.
- A command channel where users can create events and check currently scheduled events.

Possible commands are:
- Create New Event			: Creates a new event to fill out.
- List Events				: List all the current events that are scheduled for this server (be aware, depending on how many events there are to list, it may take some time to reply with all the messages).
- Set Output Channel = ...	: Sets the channel where the bot will output messages, prompt for votes, and announce events
- Help						: Shows this message

Possible commands while creating a new event are:
- Title						: Sets the title of the event (NOTE: you can add @here or @everyone and discord will understand)
- Description				: Sets the description of the event

- Game Type, Emoji			: Add a possible game type to the voting list. Also specify the emoji that will be displayed on the voting button (must be unique)
- Game Type Vote Start		: The date and time when voting for the game type should begin
- Game Type Vote End		: The date and time when voting for the game type should end. This will tally up the votes and announce the winner at this time.
- Load Default Game Types	: Add a list of default game types to the list (based upon previous recommendations and only for Rainbox Six Siege)
- Clear Game Types			: Clears the current list of game types

- Game Time, Emoji			: Add a possible game time to the voting list. Also specify the emoji that will be displayed on the voting button (must be unique)
- Game Time Vote Start		: The date and time when voting for the game time should begin
- Game Time Vote End		: The date and time when voting for the game time should end
- Clear Game Times			: Clears the current list of game times

- Summary					: Prints out a summary of the event so far

- Save						: Save the event you've created
- Quit						: Discard the event and stop creating an event

## Database setup
A MYSQL database is ran in the background to handle data storage for all these events.
The schema is provided in the ./SQLScripts/Schema.sql file.

## .auth file setup
In order to provide login information for the discord bot as well as the MYSQL server, a '.auth' file must be created in the root directory manually.
It should provide the following values:
token=...		[The authentication token for the discord bot goes here]
mysql_connection=Server=localhost; database=***; UID=***; password=***; Allow User Variable=True

## Actually building the damn thing and getting it to run on a raspberry pi:
====Visual Studio Command Prompt====
dotnet publish -r linux-arm

scp $Project/bin/Debug/netcoreapp2.2/linux-arm/publish -> RaspberryPi

sudo apt-get update
sudo apt-get upgrade
sudo apt-get install mysql-server
sudo mysql_secure_installation
	Y to all
sudo mysql -u root -p
	Create root password
	[Copying and pasting from ./SQLScripts/Schema.sql, create the database structure for the bot]
	[Also watch out for utf8 vs utf8mb4 encoding. utf8mb4 MUST BE USED]
	CREATE USER 'discordbot'@'localhost' IDENTIFIED BY 'pw';
	GRANT ALL PRIVILEGES ON discordbot.* TO 'discordbot'@'localhost';
	FLUSH PRIVILEGES;
sudo apt-get install curl libunwind8 gettext apt-transport-https
chmod +x DiscordBotEventsScheduler
./DiscordBotEventsScheduler