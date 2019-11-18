using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Discord.Rest;
using MySql.Data;
using MySql.Data.MySqlClient;

namespace DiscordBot2 {
    class Program {
        /// <summary>
        /// Helper function to output a DateTime into a string of the format: 2019/01/01 - 06:21:00
        /// </summary>
        /// <param name="dt">DateTime object to convert to a string</param>
        /// <returns>Formatted string representing the DateTime object</returns>
        public static string DateTimeOutputString(DateTime dt) {
            return dt.ToShortDateString() + " - " + dt.ToLongTimeString();
        }

        /// <summary>
        /// Class to hold a possible GameTime. Usually this data is directly read/written from the database,
        /// so it will closely mirror the structure of the 'gametimevotes' database table.
        /// In addition, since the 'gametimevotes' table has a N-1 relationship with the 'gameevents' table,
        /// The GameEvent class will contain a list of these VotedGameTime objects.
        /// </summary>
        class VotedGameTime {
            public ulong dbTimeID;      //The database ID number for this particular gametime option (GameTimeVoteID field). Used for SELECT/UPDATE SQL queries.
            public DateTime dateTime;   //The actual datetime for this possible gametime option
            public string messageEmoji; //The emoji that is used for the discord reaction button (and used for voting purposes)

            /// <summary>
            /// Create a new possible GameTime Object
            /// </summary>
            /// <param name="ID">The database ID number for this particular gametime option (GameTimeVoteID field)</param>
            /// <param name="dateTime">The DateTime object for this particular gametime option (GameTime field)</param>
            /// <param name="messageEmoji">The emoji displayed on the discord reaction button for voting purposes</param>
            public VotedGameTime(ulong ID, DateTime dateTime, string messageEmoji) {
                dbTimeID = ID;
                this.dateTime = dateTime;
                this.messageEmoji = messageEmoji;
            }
        }

        /// <summary>
        /// Class to hold a possible GameType. Usually this data is directly read/written from the database,
        /// so it will closely mirror the structure of the 'gametypevotes' database table.
        /// In addition, since the 'gametypevotes' table has a N-1 relationship with the 'gameevents' table,
        /// The GameEvent class will contain a list of these VotedGameType objects.
        /// </summary>
        class VotedGameType {
            public ulong dbTypeID;      //The database ID number for this particular gametime option (GameTypeVoteID field). Used for SELECT/UPDATE SQL queries.
            public string type;         //The actual game type for this possible gametype option.
            public string messageEmoji; //The emoji that is used for the discord reaction button (and used for voting purposes)

            /// <summary>
            /// Create a new possible GameType Object
            /// </summary>
            /// <param name="ID">The database ID number for this particular gametype option (GameTypeVoteID field)</param>
            /// <param name="type">The string object for this particular gametype option</param>
            /// <param name="messageEmoji">The emoji displayed on the discord reaction button for voting purposes</param>
            public VotedGameType(ulong ID, string type, string messageEmoji) {
                dbTypeID = ID;
                this.type = type;
                this.messageEmoji = messageEmoji;
            }
        }

        /// <summary>
        /// The class to hold a GameEvent. Usually this data is directly read/written from the database,
        /// so it will closely mirror the structure of the 'gameevents' database table.
        /// </summary>
        private class GameEvent {
            public ulong eventID;               //The database ID number for this particular gameevent (GameEventID field). Used for SELECT/UPDATE SQL queries.
            public ulong AnnouncementChannel;   //The discord channel ID number where messages are posted when scheduled. (serverconfig.OutputChannelDiscordID)
            public string Title;                //The human readable title of this game event (Title field)
            public string Description;          //The human readable description of this game event (Description field)

            //=====Game TYPE stuff=====
            public List<VotedGameType> gameTypes;       //List of all possible game types. 1-N relationship with the 'gametypevotes' table.

            public DateTime gameTypeStartVoteDateTime;  //When we need to announce voting STARTS for deciding the game type
            public int gameTypeStartPosted;             //Flag to know when we've sucessfully posted the 'Game Type Start Vote' announcement

            public DateTime gameTypeEndVoteDateTime;    //When we need to announce voting ENDS for deciding the game type
            public int gameTypeEndPosted;               //Flag to know when we've sucessfully posted the 'Game Type End Vote' announcement (and also announce the winning game type)

            public string finalGameType;                //The winner of the game type voting goes here
            public ulong gameTypeDiscordMessageID;      //The discord ID number for the message with the voting buttons on it. We save it so later we can lookup the vote counts.

            //=====Game TIME stuff=====
            public List<VotedGameTime> gameTimes;       //List of all possible game times. 1-N relationship with the 'gametimevotes' table.

            public DateTime gameTimeStartVoteDateTime;  //When we need to announce voting STARTS for deciding the game time
            public int gameTimeStartPosted;             //Flag to know when we've sucessfully posted the 'Game Time Start Vote' announcement

            public DateTime gameTimeEndVoteDateTime;    //When we need to announce voting ENDS for deciding the game type
            public int gameTimeEndPosted;               //Flag to know when we've sucessfully posted the 'Game Time End Vote' announcement (and also announce the winning game time)

            public DateTime finalGameTime;              //The winner of the game time voting goes here
            public ulong gameTimeDiscordMessageID;      //The discord ID number for the message with the voting buttons on it. We save it so later we can lookup the vote counts.

            //=====Functions=====
            /// <summary>
            /// Generic constructor to initialize all objects to their default values.
            /// </summary>
            public GameEvent() {
                eventID = 0;
                AnnouncementChannel = 0;
                Title = "";
                Description = "";

                gameTypes = new List<VotedGameType>();
                gameTypeStartVoteDateTime = new DateTime();
                gameTypeStartPosted = 0;
                gameTypeEndVoteDateTime = new DateTime();
                gameTypeEndPosted = 0;
                finalGameType = "";
                gameTypeDiscordMessageID = 0;

                gameTimes = new List<VotedGameTime>();
                gameTimeStartVoteDateTime = new DateTime();
                gameTimeStartPosted = 0;
                gameTimeEndVoteDateTime = new DateTime();
                gameTimeEndPosted = 0;
                finalGameTime = new DateTime();
                gameTimeDiscordMessageID = 0;
            }
        }

        /// <summary>
        /// Class to keep track of a current user's SESSION. These sessions are used to make it easier to create/edit/delete game events
        /// By making the communication with the bot more like a conversation. Also used to keep track of temporary data between messages
        /// Before finally comitting valid data to the database.
        /// </summary>
        private class UserSession {
            public SocketUser user;             //The discord user this session belongs to
            public SocketTextChannel channel;   //The discord channel this session is happening in

            public DateTime startTime;          //Used to keep track of when the session started (updated whenever a new valid command is entered)
            public TimeSpan sessionLength;      //Used to determine how long the session lasts before timing out.

            //The type of session (determines how we save/read the sessionData later)
            public enum SessionType_e { UNKNOWN, CREATE_EVENT };
            public SessionType_e type;
            public object sessionData;      //Object storage that gets passed around from ongoing sessions across messages

            public UserSession() {
                user = null;
                channel = null;
                startTime = new DateTime();
                sessionLength = new TimeSpan(0, 0, 30);     //How long the session will last before timing out. 30 seconds??
                type = SessionType_e.UNKNOWN;
                sessionData = null;
            }
        }

        //List of ongoing user sessions and the semaphore to ensure thread safety.
        private SemaphoreSlim userSessionsSemaphore;
        private List<UserSession> userSessions;
        
        private readonly DiscordSocketClient dClient;   //The main discord network object (pretty much everything Discord related goes through this object)
        private MySqlConnection dbConnection;           //The main MYSQL database network object (pretty much everything db related goes through this object)

        //Keep track of the current system state if needed
        //TODO: add better checking to all function to ensure things don't break if the bot goes offline here
        private enum systemState_e { UNKNOWN, DISCONNECTED, CONNECTED, READY, EXITING };
        private systemState_e systemState;

        private const string AuthFileName = ".auth";    //File name of the text file that stores authentication information such as the bot's token, and MYSQL login info
        private const string TokenItemKey = "token=";   //Discord Bot Token
        private const string MysqlConnectionKey = "mysql_connection=";  //MYSQL connection string. Should include: Server=, datebase, UID, password, Allow User Variables
        private string MysqlConnectionString;           //Place to store the MYSQL connection string once read from the .auth file

        private const int BOT_MESSAGE_DELAY = 1200;     //How quickly the Discord Bot can send out messages. Limited by API rate limits.
        
        //Top level HELP message
        private const string HELP_MESSAGE = ":\n" +
            "Custom Games scheduling bot. Valid commands are:\n" +
            "============================================\n" +
            "Create new event = creates a new event to fill out.\n" +
            "[TODO] Edit event = opens a current event to edit.\n" +
            "[TODO] Delete event = deletes a current event.\n" +
            "List events = lists all the current events for this server (be aware, depending on how many events there are to list, it may take some time to reply with all the messages).\n" +
            "Set output channel = [Text Channel Name Here] Sets where the bot will output event messages and prompt for votes.\n" +
            "Help = show this message."
            ;
        //Help message that shows when creating a new event
        private const string CREATE_NEW_EVENT_HELP_MESSAGE = ":\n" +
            "Creating a new event. Please enter the following fields by typing the command, an equals sign '=', then the value you wish to enter.\n\n" +
            "When entering the 'Game Type' or 'Game Time' you need to also specify an emoji for the voting button. For example: Game Type = GT1, :smile:\n\n" +
            "Here's a summary of the fields to enter:\n" +
            "Title = ...\n" +
            "Description = ...\n\n" +
            "Game Type, Emoji (Possible game types to vote on) = ...\n" +
            "Game Type Vote Start (When to announce that voting has begun to choose a game type) = [2000-06-21 21:06:00] ...\n" +
            "Game Type Vote End (When to close voting for a game type) = ...\n" +
            "Load Default Game Types (loads the common list of game types automatically)\n" +
            "Clear Game Types (clears the list of current game types)\n" +
            "\n" +
            "Game Time, Emoji (Possible game times to vote on) = ...\n" +
            "Game Time Vote Start (When to announce that voting has begun to choose a game time) = ...\n" +
            "Game Time Vote End (When to close voting for a game time)= ...\n" +
            "Clear Game Times (clears the list of current game times)\n" +
            "\n" +
            "Save event with [Save], Discard with [Quit]"
            ;

        private Thread DateTimeSchedulerThread;     //The thread object that does all of the scheduling checks
        private Thread UserSessionUpdateThread;     //The thread object that checks for timed out user sessions

        #region Main Startup Functions

        /// <summary>
        /// MAIN STARTUP FUNCTION
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args) {
            new Program().MainAsync().GetAwaiter().GetResult();     //Since the discord bot socket needs to operate in a asynchronous manner, fire off a copy of the Program.
        }

        /// <summary>
        /// Main program start. Initialize necessary variables and subscribe to any Discord Bot events
        /// </summary>
        public Program() {
            dbConnection = new MySqlConnection();
            dClient = new DiscordSocketClient();

            userSessions = new List<UserSession>();
            userSessionsSemaphore = new SemaphoreSlim(1, 1);

            systemState = systemState_e.DISCONNECTED;
            
            //Discord Bot Events to subscribe to:
            dClient.Log += Log_Event;                           //If any log messages come through...
            dClient.Ready += Ready_Event;                       //Server data has finished downloading...
            dClient.Connected += Connected_Event;               //Connected to the gateway...
            dClient.Disconnected += Disconnected_Event;         //Disconnected from the gateway...
            dClient.MessageReceived += MessageReceived_Event;   //Whenever a message is received either through text chats or DMs...
            dClient.ReactionAdded += ReactionAdded_Event;       //Whenever a reaction is added to a message...
        }

        public async Task MainAsync() {
            //Fetch bot token from .auth file : token=***
            if (File.Exists(AuthFileName)) {
                string BotTokenString = "";
                string dbConnectionString = "";

                //Read the bot token and mysql connection string from the .auth file.
                StreamReader sr = new StreamReader(AuthFileName);
                while (!sr.EndOfStream) {
                    string s = sr.ReadLine();
                    if (s.Length > TokenItemKey.Length) {
                        if (string.Compare(TokenItemKey, s.Substring(0, TokenItemKey.Length), true) == 0) {
                            //Found the token entry, parse to end
                            BotTokenString = s.Substring(TokenItemKey.Length);
                        }
                    }

                    if (s.Length > MysqlConnectionKey.Length) {
                        if (string.Compare(MysqlConnectionKey, s.Substring(0, MysqlConnectionKey.Length), true) == 0) {
                            //Found the mysql connection entry, parse to end
                            dbConnectionString = s.Substring(MysqlConnectionKey.Length);
                        }
                    }
                }
                sr.Close();

                //Validate entries
                if (BotTokenString == "") {
                    LogToConsole("Error parsing Discord Bot Token. Please check the .auth file and try again. Exiting.");
                    return;
                }
                if (dbConnectionString == "") {
                    LogToConsole("Error parsing Mysql Database connection string. Please check the .auth file and try again. Exiting.");
                    return;
                }

                LogToConsole("Database string loaded...");
                MysqlConnectionString = dbConnectionString;
                LogToConsole("Discord Bot starting up. Token loaded. Logging in...");

                //Attempt to login as the bot
                //TODO: This probably needs better error handling or timeout checks
                await dClient.LoginAsync(TokenType.Bot, BotTokenString);
                await dClient.StartAsync();

                while (dClient.ConnectionState == ConnectionState.Connecting) ; //Wait to actually connect, or timeout

                //Startup other threading tasks
                DateTimeSchedulerThread = new Thread(DateTimeSchedulerThread_Func);
                DateTimeSchedulerThread.Start();

                UserSessionUpdateThread = new Thread(UserSessionUpdateThread_Func);
                UserSessionUpdateThread.Start();

                //Hang out here until something tells us to exit
                while (systemState != systemState_e.EXITING) {
                    //busy wait
                    Thread.Sleep(100);
                }
                LogToConsole("System exiting.");
            } else {
                LogToConsole("Missing .auth file with configuration information. Exiting.");
                return;
            }
        }

        #endregion

        #region Thread Functions

        /// <summary>
        /// The Thread object 'DateTimeSchedulerThread' starts this function in a seperate thread.
        /// Every so often it queries the database for any events that are scheduled to occur.
        /// It then performs these appropriate actions.
        /// </summary>
        private void DateTimeSchedulerThread_Func() {
            while (systemState != systemState_e.EXITING) {
                //Make sure the discord bot is connected and ready before we start doing anything
                if (systemState != systemState_e.READY) {
                    Thread.Sleep(1000);
                    continue;
                }

                Thread.Sleep(1 * 60 * 1000);    //Every minute
                LogToConsole("DateTime Scheduler Check.");

                //Check for any Game Events where the GameTypeStartVote is due
                List<GameEvent> eventsToAnnounce = new List<GameEvent>();
                QueryGameTypeStartEventsToAnnounce(ref eventsToAnnounce);
                foreach (GameEvent annEvent in eventsToAnnounce) {
                    LogToConsole("Game Type Start Announcement event due. Sending announcement message to channel: " + annEvent.AnnouncementChannel.ToString());
                    SocketTextChannel outputChannel = dClient.GetChannel(annEvent.AnnouncementChannel) as SocketTextChannel;
                    if (outputChannel != null) {
                        OutputGameTypeStartAnnouncementMessage(outputChannel, annEvent);
                    }
                }

                //Check for any Game Events where the  GameTypeEndVote is due
                eventsToAnnounce = new List<GameEvent>();
                QueryGameTypeEndEventsToAnnounce(ref eventsToAnnounce);
                foreach (GameEvent annEvent in eventsToAnnounce) {
                    LogToConsole("Game Type End Announcement event due. Sending announcement message to channel: " + annEvent.AnnouncementChannel.ToString());
                    SocketTextChannel outputChannel = dClient.GetChannel(annEvent.AnnouncementChannel) as SocketTextChannel;
                    if (outputChannel != null) {
                        OutputGameTypeEndAnnouncementMessage(outputChannel, annEvent);
                    }
                }

                //Check for any Game Events where the GameTimeStartVote is due
                eventsToAnnounce = new List<GameEvent>();
                QueryGameTimeStartEventsToAnnounce(ref eventsToAnnounce);
                foreach (GameEvent annEvent in eventsToAnnounce) {
                    LogToConsole("Game Time Start Announcement event due. Sending announcement message to channel: " + annEvent.AnnouncementChannel.ToString());
                    SocketTextChannel outputChannel = dClient.GetChannel(annEvent.AnnouncementChannel) as SocketTextChannel;
                    if (outputChannel != null) {
                        OutputGameTimeStartAnnouncementMessage(outputChannel, annEvent);
                    }
                }

                //Check for any Game Events where the GameTimeEndVote is due
                eventsToAnnounce = new List<GameEvent>();
                QueryGameTimeEndEventsToAnnounce(ref eventsToAnnounce);
                foreach (GameEvent annEvent in eventsToAnnounce) {
                    LogToConsole("Game Time End Announcement event due. Sending announcement message to channel: " + annEvent.AnnouncementChannel.ToString());
                    SocketTextChannel outputChannel = dClient.GetChannel(annEvent.AnnouncementChannel) as SocketTextChannel;
                    if (outputChannel != null) {
                        OutputGameTimeEndAnnouncementMessage(outputChannel, annEvent);
                    }
                }

                //Check for any Game Events where the FinalGameTime Announcement is due
                eventsToAnnounce = new List<GameEvent>();
                QueryFinalGameTimeEventsToAnnounce(ref eventsToAnnounce);
                foreach (GameEvent annEvent in eventsToAnnounce) {
                    LogToConsole("Final Game Time Announcement event due. Sending announcement message to channel: " + annEvent.AnnouncementChannel.ToString());
                    SocketTextChannel outputChannel = dClient.GetChannel(annEvent.AnnouncementChannel) as SocketTextChannel;
                    if (outputChannel != null) {
                        OutputFinalGameTimeAnnouncementMessage(outputChannel, annEvent);
                    }
                }

                //Check for any completed events that haven't been marked yet and prune the database as needed
                TrimCompletedEvents();
            }
            LogToConsole("DateTime Scheduler Thread Exit.");
        }

        //=====Game TYPE Functions=====

        /// <summary>
        /// Query the database for any game events where the GameTypeStartVote date is passed.
        /// </summary>
        /// <param name="eventsToAnnounce">A list of game events that need to have their GameTypeStartVote annoucement message posted.</param>
        private void QueryGameTypeStartEventsToAnnounce(ref List<GameEvent> eventsToAnnounce) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT gametypevotes.GameTypeVoteID, gametypevotes.GameType, gameevents.GameEventID, gametypevotes.DiscordEmoji, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTypeStartVote, gameevents.GameTypeEndVote " +
                        "FROM gameevents " +
                        "INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID " +
                        "LEFT JOIN gametypevotes ON gameevents.GameEventID = gametypevotes.GameEventID " +
                        "WHERE gameevents.Completed = 0 AND gameevents.GameTypeStartVotePosted = 0 " +
                        "AND gameevents.GameTypeStartVote <= now() " +
                        "ORDER BY gameevents.GameEventID;";
                    using (MySqlDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            //First get the event ID
                            ulong eventID = 0;
                            if (!reader.IsDBNull(2)) {
                                eventID = reader.GetUInt64(2);
                            }
                            if (eventsToAnnounce.Count > 0 && eventsToAnnounce.Last().eventID == eventID) {
                                //There are multiple game types for this one game event, so add them to the last event's inner list as needed
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    eventsToAnnounce.Last().gameTypes.Add(new VotedGameType(reader.GetUInt64(0), reader.GetString(1), reader.GetString(3)));
                                }
                            } else {
                                GameEvent newEvent = new GameEvent();
                                newEvent.eventID = eventID;
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    newEvent.gameTypes.Add(new VotedGameType(reader.GetUInt64(0), reader.GetString(1), reader.GetString(3)));
                                }
                                if (!reader.IsDBNull(4)) {
                                    newEvent.AnnouncementChannel = reader.GetUInt64(4);
                                }
                                if (!reader.IsDBNull(5)) {
                                    newEvent.Title = reader.GetString(5);
                                }
                                if (!reader.IsDBNull(6)) {
                                    newEvent.Description = reader.GetString(6);
                                }
                                if (!reader.IsDBNull(8)) {
                                    newEvent.gameTypeEndVoteDateTime = reader.GetDateTime(8);
                                }

                                eventsToAnnounce.Add(newEvent);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Output the GameTypeStartVote annoucement message to a discord channel for a particular game event
        /// </summary>
        /// <param name="outputChannel">The discord text channel where the annoucement message should be posted.</param>
        /// <param name="announcementEvent">The game event that needs to be announced.</param>
        private void OutputGameTypeStartAnnouncementMessage(SocketTextChannel outputChannel, GameEvent announcementEvent) {
            using (outputChannel.EnterTypingState()) {
                string outputMessage = ":\n" +
                    " **NEW CUSTOM GAME EVENT TO VOTE ON!**\n" +
                    " " + announcementEvent.Title + "\n" +
                    " " + announcementEvent.Description + "\n" +
                    "========================================\n" +
                    " Vote on your preferred game type by clicking on the emoji below: \n" +
                    " Voting will close on: " + DateTimeOutputString(announcementEvent.gameTypeEndVoteDateTime) + " PST (" 
                        + announcementEvent.gameTypeEndVoteDateTime.AddHours(3).ToShortTimeString() + " EST)\n";

                //List each game type and their corresponding emoji
                foreach (VotedGameType vgt in announcementEvent.gameTypes) {
                    //Can't have a reaction button without an emoji, so just skip it (this should never happen).
                    if (vgt.messageEmoji == "") {
                        continue;
                    }
                    outputMessage += vgt.type + " - " + vgt.messageEmoji + "\n";
                }

                //Output the annoucement message and save the resultant discord message ID.
                //  The message ID will be saved and used to add the voting buttons (emoji reactions) and used to tally up the votes later.
                IUserMessage msg = OutputMessage(outputChannel, outputMessage);
                announcementEvent.gameTypeDiscordMessageID = msg.Id;
                foreach (VotedGameType vgt in announcementEvent.gameTypes) {
                    if (vgt.messageEmoji == "") {
                        continue;
                    }
                    OutputReaction(msg, new Emoji(vgt.messageEmoji));
                }
            }

            //Now that the event has been announced, mark the appropriate Game Type Start Vote Posted flag in the database.
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET GameTypeStartVotePosted = '1', GameTypeDiscordMessageID = @p1 WHERE GameEventID = @p2;";
                    cmd.Parameters.AddWithValue("@p1", announcementEvent.gameTypeDiscordMessageID);
                    cmd.Parameters.AddWithValue("@p2", announcementEvent.eventID);
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Annoucement " + announcementEvent.eventID.ToString() + " has been made.");
                    } else {
                        LogToConsole("Error updating announcement " + announcementEvent.eventID.ToString() + ".");
                    }
                }
            }
        }

        /// <summary>
        /// Query the database for any game events where the GameTypeEndVote date is passed.
        /// </summary>
        /// <param name="eventsToAnnounce">A list of game events that need to have their GameTypeEndVote annoucement message posted.</param>
        private void QueryGameTypeEndEventsToAnnounce(ref List<GameEvent> eventsToAnnounce) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT gametypevotes.GameTypeVoteID, gametypevotes.GameType, gameevents.GameEventID, gametypevotes.DiscordEmoji, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTypeStartVote, gameevents.GameTypeEndVote, gameevents.GameTimeStartVote, gameevents.GameTypeDiscordMessageID " +
                        "FROM gameevents " +
                        "INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID " +
                        "LEFT JOIN gametypevotes ON gameevents.GameEventID = gametypevotes.GameEventID " +
                        "WHERE gameevents.Completed = 0 AND gameevents.GameTypeEndVotePosted = 0 " +
                        "AND gameevents.GameTypeEndVote <= now() " +
                        "ORDER BY gameevents.GameEventID;";
                    using (MySqlDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            ulong eventID = 0;
                            if (!reader.IsDBNull(2)) {
                                eventID = reader.GetUInt64(2);
                            }
                            if (eventsToAnnounce.Count > 0 && eventsToAnnounce.Last().eventID == eventID) {
                                //Multiple game types
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    eventsToAnnounce.Last().gameTypes.Add(new VotedGameType(reader.GetUInt64(0), reader.GetString(1), reader.GetString(3)));
                                }
                            } else {
                                GameEvent newEvent = new GameEvent();
                                newEvent.eventID = eventID;
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    newEvent.gameTypes.Add(new VotedGameType(reader.GetUInt64(0), reader.GetString(1), reader.GetString(3)));
                                }
                                if (!reader.IsDBNull(4)) {
                                    newEvent.AnnouncementChannel = reader.GetUInt64(4);
                                }
                                if (!reader.IsDBNull(5)) {
                                    newEvent.Title = reader.GetString(5);
                                }
                                if (!reader.IsDBNull(6)) {
                                    newEvent.Description = reader.GetString(6);
                                }
                                if (!reader.IsDBNull(8)) {
                                    newEvent.gameTypeEndVoteDateTime = reader.GetDateTime(8);
                                }
                                if (!reader.IsDBNull(9)) {
                                    newEvent.gameTimeStartVoteDateTime = reader.GetDateTime(9);
                                }
                                if (!reader.IsDBNull(10)) {
                                    newEvent.gameTypeDiscordMessageID = reader.GetUInt64(10);
                                }

                                eventsToAnnounce.Add(newEvent);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Since voting is now over for the game type, look up the message with all the voting buttons and tally them up to decide the 'finalGameType'.
        /// Then output the GameTypeEndVote annoucement message to a discord channel for this particular game event
        /// </summary>
        /// <param name="outputChannel">The discord text channel where the annoucement message should be posted.</param>
        /// <param name="announcementEvent">The game event that needs to be announced.</param>
        private void OutputGameTypeEndAnnouncementMessage(SocketTextChannel outputChannel, GameEvent announcementEvent) {
            //First double check to see if the channel where the voting message was posted still exists
            SocketTextChannel stc = dClient.GetChannel(announcementEvent.AnnouncementChannel) as SocketTextChannel;
            if (stc == null) {
                //Looks like the announcement channel has been deleted, mark the event as completed in the database and exit
                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE gameevents.GameEventID = @p1;";
                        cmd.Parameters.AddWithValue("@p1", announcementEvent.eventID);
                        if (cmd.ExecuteNonQuery() > 0) {
                            LogToConsole("Dangling game type end announcement event cleared.");
                        } else {
                            LogToConsole("Dangling game type end announcement error clearing event.");
                        }
                    }
                }
                return;
            }

            //Now lookup the actual message with the voting buttons
            IMessage gameTypeVoteMessage = stc.GetMessageAsync(announcementEvent.gameTypeDiscordMessageID).Result;
            if (gameTypeVoteMessage == null || gameTypeVoteMessage.Reactions.Count == 0) {
                //The annoucement message was deleted, mark the event as completed in the database and exit
                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE gameevents.GameEventID = @p1;";
                        cmd.Parameters.AddWithValue("@p1", announcementEvent.eventID);
                        if (cmd.ExecuteNonQuery() > 0) {
                            LogToConsole("Dangling game type end announcement event cleared.");
                        } else {
                            LogToConsole("Dangling game type end announcement error clearing event.");
                        }
                    }
                }
                return;
            }

            //Go through each reaction button, and gather up which ones have the most votes.
            List<IEmote> topEmotes = new List<IEmote>();
            IEmote topEmote = null;
            int topVote = 0;
            //Find the top voted item(s)
            foreach (KeyValuePair<IEmote, ReactionMetadata> r in gameTypeVoteMessage.Reactions) {
                if (r.Value.ReactionCount >= topVote) {
                    topVote = r.Value.ReactionCount;
                }
            }
            //Add all the reactions with the top vote count to the list
            foreach (KeyValuePair<IEmote, ReactionMetadata> r in gameTypeVoteMessage.Reactions) {
                if (r.Value.ReactionCount == topVote) {
                    topEmotes.Add(r.Key);
                }
            }

            //Randomly pick one from the list of top voted answers
            if (topEmotes.Count > 1) {
                Random r = new Random();
                int rVal = r.Next(0, topEmotes.Count - 1);
                using (outputChannel.EnterTypingState()) {
                    OutputMessage(outputChannel, "Looks like we have a tie! I'll roll the (totally random) dice and pick one at random!\n");
                }
                topEmote = topEmotes[rVal];
            } else {
                topEmote = topEmotes[0];
            }

            //Now find the game type that the top voted emoji corresponds to, and set it as the final game type
            foreach (VotedGameType vgt in announcementEvent.gameTypes) {
                if (string.Compare(vgt.messageEmoji, topEmote.Name) == 0) {
                    announcementEvent.finalGameType = vgt.type;
                }
            }

            //Make the annoucement about which game type won the vote, as well as a note about when voting for the game TIME will take place
            using (outputChannel.EnterTypingState()) {
                string outputMessage = ":\n" +
                    " **NEW CUSTOM GAME EVENT CHOSEN!**\n" +
                    " " + announcementEvent.Title + "\n" +
                    " " + announcementEvent.Description + "\n" +
                    "=======================================\n" +
                    " After tallying up the votes, this event will be: " + announcementEvent.finalGameType + "!\n" +
                    " Stay tuned, as we will vote on when this event will take place on: " + DateTimeOutputString(announcementEvent.gameTimeStartVoteDateTime) +
                        " PST (" + announcementEvent.gameTimeStartVoteDateTime.AddHours(3).ToShortTimeString() + " EST)";
                OutputMessage(outputChannel, outputMessage);
            }

            //Now that the event has been announced, mark the appropriate Game Type End Vote Posted flag in the database. And also save the Final Game Type
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET GameTypeEndVotePosted = '1', FinalGameType = @p1 WHERE GameEventID = @p2;";
                    cmd.Parameters.AddWithValue("@p1", announcementEvent.finalGameType);
                    cmd.Parameters.AddWithValue("@p2", announcementEvent.eventID);
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Annoucement " + announcementEvent.eventID.ToString() + " has been made.");
                    } else {
                        LogToConsole("Error updating announcement " + announcementEvent.eventID.ToString() + ".");
                    }
                }
            }
        }

        //=====Game TIME Functions=====

        /// <summary>
        /// Query the database for any game events where the GameTimeStartVote date is passed.
        /// </summary>
        /// <param name="eventsToAnnounce">A list of game events that need to have their GameTimeStartVote annoucement message posted.</param>
        private void QueryGameTimeStartEventsToAnnounce(ref List<GameEvent> eventsToAnnounce) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT gametimevotes.GameTimeVoteID, gametimevotes.GameTime, gameevents.GameEventID, gametimevotes.DiscordEmoji, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTimeStartVote, gameevents.GameTimeEndVote " +
                        "FROM gameevents " +
                        "INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID " +
                        "LEFT JOIN gametimevotes ON gameevents.GameEventID = gametimevotes.GameEventID " +
                        "WHERE gameevents.Completed = 0 AND gameevents.GameTimeStartVotePosted = 0 " +
                        "AND gameevents.GameTimeStartVote <= now() " +
                        "ORDER BY gameevents.GameEventID;";
                    using (MySqlDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            ulong eventID = 0;
                            if (!reader.IsDBNull(2)) {
                                eventID = reader.GetUInt64(2);
                            }
                            if (eventsToAnnounce.Count > 0 && eventsToAnnounce.Last().eventID == eventID) {
                                //Multiple game times
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    eventsToAnnounce.Last().gameTimes.Add(new VotedGameTime(reader.GetUInt64(0), reader.GetDateTime(1), reader.GetString(3)));
                                }
                            } else {
                                GameEvent newEvent = new GameEvent();
                                newEvent.eventID = eventID;
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    newEvent.gameTimes.Add(new VotedGameTime(reader.GetUInt64(0), reader.GetDateTime(1), reader.GetString(3)));
                                }
                                if (!reader.IsDBNull(4)) {
                                    newEvent.AnnouncementChannel = reader.GetUInt64(4);
                                }
                                if (!reader.IsDBNull(5)) {
                                    newEvent.Title = reader.GetString(5);
                                }
                                if (!reader.IsDBNull(6)) {
                                    newEvent.Description = reader.GetString(6);
                                }
                                if (!reader.IsDBNull(8)) {
                                    newEvent.gameTimeEndVoteDateTime = reader.GetDateTime(8);
                                }

                                eventsToAnnounce.Add(newEvent);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Output the GameTimeStartVote announcement message to a discord channel for a particular game event
        /// </summary>
        /// <param name="outputChannel">The discord text channel where the annoucement message should be posted.</param>
        /// <param name="announcementEvent">The game event that needs to be announced.</param>
        private void OutputGameTimeStartAnnouncementMessage(SocketTextChannel outputChannel, GameEvent announcementEvent) {
            using (outputChannel.EnterTypingState()) {
                string outputMessage = ":\n" +
                    " **NEW CUSTOM GAME EVENT TO VOTE ON!**\n" +
                    " " + announcementEvent.Title + "\n" +
                    " " + announcementEvent.Description + "\n" +
                    "=======================================\n" +
                    " Vote on your preferred game time by clicking on the emoji below: \n" +
                    " Voting will close on: " + DateTimeOutputString(announcementEvent.gameTimeEndVoteDateTime) + 
                        " PST (" + announcementEvent.gameTimeEndVoteDateTime.AddHours(3).ToShortTimeString() + " EST)\n";

                //List each game time and their corresponding emoji
                foreach (VotedGameTime vgt in announcementEvent.gameTimes) {
                    //Can't have a reaction button without an emoji, so just skip it (this should never happen)
                    if (vgt.messageEmoji == "") {
                        continue;
                    }
                    outputMessage += DateTimeOutputString(vgt.dateTime) + " PST - " + vgt.messageEmoji + "\n";
                }
                
                //Output the annoucement message and save the resultant discord message ID.
                //  The message ID will be saved and used to add the voting buttons (emoji reactions) and used to tally up the votes later.
                IUserMessage msg = OutputMessage(outputChannel, outputMessage);
                announcementEvent.gameTimeDiscordMessageID = msg.Id;
                foreach(VotedGameTime vgt in announcementEvent.gameTimes) {
                    if (vgt.messageEmoji == "") {
                        continue;
                    }
                    OutputReaction(msg, new Emoji(vgt.messageEmoji));
                }
            }

            //Now that the event has been announced, mark the appropriate Game Time Start Vote Posted flag in the database.
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET GameTimeStartVotePosted = '1', GameTimeDiscordMessageID = @p1 WHERE GameEventID = @p2;";
                    cmd.Parameters.AddWithValue("@p1", announcementEvent.gameTimeDiscordMessageID);
                    cmd.Parameters.AddWithValue("@p2", announcementEvent.eventID);
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Announcement " + announcementEvent.eventID.ToString() + " has been made.");
                    } else {
                        LogToConsole("Error updating announcement " + announcementEvent.eventID.ToString() + ".");
                    }
                }
            }
        }

        /// <summary>
        /// Query the database for any game events where the GameTimeEndVote date is passed.
        /// </summary>
        /// <param name="eventsToAnnounce">A list of game events that need to have their GameTimeEndVote annoucement message posted.</param>
        private void QueryGameTimeEndEventsToAnnounce(ref List<GameEvent> eventsToAnnounce) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT gametimevotes.GameTimeVoteID, gametimevotes.GameTime, gameevents.GameEventID, gametimevotes.DiscordEmoji, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTimeStartVote, gameevents.GameTimeEndVote, gameevents.GameTimeDiscordMessageID " +
                        "FROM gameevents " +
                        "INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID " +
                        "LEFT JOIN gametimevotes ON gameevents.GameEventID = gametimevotes.GameEventID " +
                        "WHERE gameevents.Completed = 0 AND gameevents.GameTimeEndVotePosted = 0 " +
                        "AND gameevents.GameTimeEndVote <= now() " +
                        "ORDER BY gameevents.GameEventID;";
                    using (MySqlDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            ulong eventID = 0;
                            if (!reader.IsDBNull(2)) {
                                eventID = reader.GetUInt64(2);
                            }
                            if (eventsToAnnounce.Count > 0 && eventsToAnnounce.Last().eventID == eventID) {
                                //Multiple game types
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    eventsToAnnounce.Last().gameTimes.Add(new VotedGameTime(reader.GetUInt64(0), reader.GetDateTime(1), reader.GetString(3)));
                                }
                            } else {
                                GameEvent newEvent = new GameEvent();
                                newEvent.eventID = eventID;
                                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(3)) {
                                    newEvent.gameTimes.Add(new VotedGameTime(reader.GetUInt64(0), reader.GetDateTime(1), reader.GetString(3)));
                                }
                                if (!reader.IsDBNull(4)) {
                                    newEvent.AnnouncementChannel = reader.GetUInt64(4);
                                }
                                if (!reader.IsDBNull(5)) {
                                    newEvent.Title = reader.GetString(5);
                                }
                                if (!reader.IsDBNull(6)) {
                                    newEvent.Description = reader.GetString(6);
                                }
                                if (!reader.IsDBNull(8)) {
                                    newEvent.gameTimeEndVoteDateTime = reader.GetDateTime(8);
                                }
                                if (!reader.IsDBNull(9)) {
                                    newEvent.gameTimeDiscordMessageID = reader.GetUInt64(9);
                                }

                                eventsToAnnounce.Add(newEvent);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Since voting is now over for the game type, look up the message with all the voting buttons and tally them up to decide the 'finalGameTime'.
        /// Then output the GameTimeEndVote annoucement message to a discord channel for this particular game event.
        /// </summary>
        /// <param name="outputChannel">The discord text channel where the annoucement message should be posted.</param>
        /// <param name="announcementEvent">The game event that needs to be announced.</param>
        private void OutputGameTimeEndAnnouncementMessage(SocketTextChannel outputChannel, GameEvent announcementEvent) {
            //First double check to see if the channel where the voting message was posted still exists
            SocketTextChannel stc = dClient.GetChannel(announcementEvent.AnnouncementChannel) as SocketTextChannel;
            if (stc == null) {
                //Looks like the annoucement channel has been deleted, mark the event as completed in the database and exit
                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE gameevents.GameEventID = @p1;";
                        cmd.Parameters.AddWithValue("@p1", announcementEvent.eventID);
                        if (cmd.ExecuteNonQuery() > 0) {
                            LogToConsole("Dangling game time end announcement event cleared.");
                        } else {
                            LogToConsole("Dangling game time end announcement error clearing event.");
                        }
                    }
                }
                return;
            }

            //Now lookup the actual message with the voting buttos
            IMessage gameTimeVoteMessage = stc.GetMessageAsync(announcementEvent.gameTimeDiscordMessageID).Result;
            if (gameTimeVoteMessage == null) {
                //The annoucement message was delete, mark the event as completed in the database and exit
                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE gameevents.GameEventID = @p1;";
                        cmd.Parameters.AddWithValue("@p1", announcementEvent.eventID);
                        if (cmd.ExecuteNonQuery() > 0) {
                            LogToConsole("Dangling game time end announcement event cleared.");
                        } else {
                            LogToConsole("Dangling game time end announcement error clearing event.");
                        }
                    }
                }
                return;
            }

            //Go through each reaction button, and tally up which one has the most votes
            List<IEmote> topEmotes = new List<IEmote>();
            IEmote topEmote = null;
            int topVote = 0;
            foreach (KeyValuePair<IEmote, ReactionMetadata> r in gameTimeVoteMessage.Reactions) {
                if (r.Value.ReactionCount >= topVote) {
                    topVote = r.Value.ReactionCount;
                }
            }
            //Add all the reactions with the top vote count to the list
            foreach (KeyValuePair<IEmote, ReactionMetadata> r in gameTimeVoteMessage.Reactions) {
                if (r.Value.ReactionCount == topVote) {
                    topEmotes.Add(r.Key);
                }
            }

            //Randomly pick one from the list of top voted answers
            if (topEmotes.Count > 1) {
                Random r = new Random();
                int rVal = r.Next(0, topEmotes.Count - 1);
                using (outputChannel.EnterTypingState()) {
                    OutputMessage(outputChannel, "Looks like we have a tie! I'll roll the (totally random) dice and pick one at random!\n");
                }
                topEmote = topEmotes[rVal];
            } else {
                topEmote = topEmotes[0];
            }

            //Now find the game time that the top voted emoji corresponds to, and set it as the final game time
            foreach (VotedGameTime vgt in announcementEvent.gameTimes) {
                if (string.Compare(vgt.messageEmoji, topEmote.Name) == 0) {
                    announcementEvent.finalGameTime = vgt.dateTime;
                }
            }

            //Make the annoucement about which game time won the vote
            using (outputChannel.EnterTypingState()) {
                string outputMessage = ":\n" +
                    " **NEW CUSTOM GAME TIME CHOSEN!**\n" +
                    " " + announcementEvent.Title + "\n" +
                    " " + announcementEvent.Description + "\n" +
                    "=======================================\n" +
                    " After tallying up the votes, this event will take place on: " + DateTimeOutputString(announcementEvent.finalGameTime) + 
                        " PST (" + announcementEvent.finalGameTime.AddHours(3).ToShortTimeString() + " EST)\n";
                OutputMessage(outputChannel, outputMessage);
            }

            //Now that the event has been announced, mark the appropriate Game Time End Vote Posted flag in the database. And also save the Final Game Time
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET GameTimeEndVotePosted = '1', FinalGameTime = @p1 WHERE GameEventID = @p2;";
                    cmd.Parameters.AddWithValue("@p1", announcementEvent.finalGameTime);
                    cmd.Parameters.AddWithValue("@p2", announcementEvent.eventID);
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Announcement " + announcementEvent.eventID.ToString() + " has been made.");
                    } else {
                        LogToConsole("Error updating announcement " + announcementEvent.eventID.ToString() + ".");
                    }
                }
            }
        }

        //=====FINAL Functions=====

        /// <summary>
        /// Query the database for any game events where the final GameTime date is bassed.
        /// </summary>
        /// <param name="eventsToAnnounce">A list of game events that need to have their final GameTime annoucement message posted.</param>
        private void QueryFinalGameTimeEventsToAnnounce(ref List<GameEvent> eventsToAnnounce) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT serverconfig.OutputChannelDiscordID, gameevents.GameEventID, gameevents.Title, gameevents.Description, gameevents.FinalGameType, gameevents.FinalGameTime " +
                        "FROM gameevents " +
                        "INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID " +
                        "WHERE gameevents.Completed = 0 AND gameevents.FinalGameTime <= now();";
                    using (MySqlDataReader reader = cmd.ExecuteReader()) {
                        while (reader.Read()) {
                            GameEvent newEvent = new GameEvent();
                            if (!reader.IsDBNull(0)) {
                                newEvent.AnnouncementChannel = reader.GetUInt64(0);
                            }
                            if (!reader.IsDBNull(1)) {
                                newEvent.eventID = reader.GetUInt64(1);
                            }
                            if (!reader.IsDBNull(2)) {
                                newEvent.Title = reader.GetString(2);
                            }
                            if (!reader.IsDBNull(3)) {
                                newEvent.Description = reader.GetString(3);
                            }
                            if (!reader.IsDBNull(4)) {
                                newEvent.finalGameType = reader.GetString(4);
                            }
                            if (!reader.IsDBNull(5)) {
                                newEvent.finalGameTime = reader.GetDateTime(5);
                            }

                            eventsToAnnounce.Add(newEvent);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Output the Final game event annoucement message to a discord channel for a particular game event
        /// </summary>
        /// <param name="outputChannel">The discord text channel where the annoucement message should be posted.</param>
        /// <param name="announcementEvent">The game event that needs to be announced.</param>
        private void OutputFinalGameTimeAnnouncementMessage(SocketTextChannel outputChannel, GameEvent announcementEvent) {
            using (outputChannel.EnterTypingState()) {
                string outputMessage = ":\n" +
                    "**ATTENTION EVERYONE! IT'S TIME FOR A CUSTOM GAME!**\n" +
                    " GET YOUR FLUFFY BUTTS IN THE CUSTOM GAME CHANNELS!\n" +
                    " " + announcementEvent.Title + "\n" +
                    " " + announcementEvent.Description + "\n" +
                    "=======================================\n" +
                    " We're playing: " + announcementEvent.finalGameType + ", good luck everyone!\n";
                OutputMessage(outputChannel, outputMessage);
            }

            //Now that the event has been announced, mark the appropriate Completed flag in the database
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE GameEventID = @p1;";
                    cmd.Parameters.AddWithValue("@p1", announcementEvent.eventID);
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Announcement " + announcementEvent.eventID.ToString() + " has been made.");
                    } else {
                        LogToConsole("Error updating announcement " + announcementEvent.eventID.ToString() + ".");
                    }
                }
            }
        }

        /// <summary>
        /// Basically a cleanup functions that goes through and ensures any game events that somehow got skipped will get marked as completed.
        /// This function may be completely unecessary.
        /// </summary>
        private void TrimCompletedEvents() {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "UPDATE gameevents SET Completed = '1' WHERE Completed = '0' AND " +
                        "GameTypeStartVotePosted > '0' AND GameTypeEndVotePosted > '0' AND GameTimeStartVotePosted > '0' AND GameTimeEndVotePosted > '0' " +
                        "AND ADDTIME(FinalGameTime, '01:00:00') < now();";
                    if (cmd.ExecuteNonQuery() > 0) {
                        LogToConsole("Event marked as completed.");
                    } else {
                        //Suppress console output as this will return zero when there are no records to edit
                        //LogToConsole("Error marking event as completed.");
                    }
                }
            }
        }

        /// <summary>
        /// The Thread object 'UserSessionUpdateThread' starts this function in a seperate thread.
        /// Every so often it queries the database for any user sesions that have timed out and clears them from the list
        /// </summary>
        private void UserSessionUpdateThread_Func() {
            while (systemState != systemState_e.EXITING) {
                LogToConsole("User Session Update Check.");

                userSessionsSemaphore.Wait();
                for (int i = 0; i < userSessions.Count; i++) {
                    if (userSessions[i].startTime + userSessions[i].sessionLength < DateTime.Now) {
                        LogToConsole("Removing expired session, type = " + userSessions[i].type.ToString());
                        //OutputDMMessage(userSessions[i].user, userSessions[i].type.ToString() + " session ended.");
                        userSessions.RemoveAt(i);
                        i--;
                    }
                }
                userSessionsSemaphore.Release();

                Thread.Sleep(5 * 60 * 1000);    //Every 5 minutes
            }
            LogToConsole("User Session Thread Exit.");
        }

        #endregion

        #region dClient EVENTS

        /// <summary>
        /// Gets called when the bot has finished downloading all the information needed from the discord hub and is ready to operate
        /// </summary>
        /// <returns></returns>
        private Task Ready_Event() {
            LogToConsole(dClient.CurrentUser.Username + " is now ready.");

            systemState = systemState_e.READY;

            //List all the servers we're connected to and update the 'serverconfig' table in the database
            foreach (SocketGuild sg in dClient.Guilds) {
                LogToConsole("Connected to server: " + sg.Name);
                LogToConsole("   Text channels: ");

                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "SELECT EXISTS(SELECT 1 FROM serverconfig WHERE DiscordID = @p1 LIMIT 1)";
                        cmd.Parameters.AddWithValue("@p1", sg.Id);
                        if (Convert.ToInt32(cmd.ExecuteScalar()) == 1) {
                            LogToConsole("Guild: " + sg.Name + " : " + sg.Id.ToString() + " is found in the database");
                            //TODO: Check the Output and Config ID fields to see if those channels are still valid for this server
                        }
                        else {
                            LogToConsole("Guild: " + sg.Id.ToString() + " is not in the database...adding.");
                            cmd.CommandText = "INSERT INTO serverconfig(DiscordID) VALUES (@p1);";
                            if (cmd.ExecuteNonQuery() == 0) {
                                LogToConsole("Problem adding new server to database.");
                            }
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Gets called when the bot connects to the discord hub, but before its ready to operate
        /// </summary>
        /// <returns></returns>
        private Task Connected_Event() {
            LogToConsole("Connected_Event fired.");

            systemState = systemState_e.CONNECTED;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the bot disconnects from the discord hub. Pause any tasks until we reconnect
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        private Task Disconnected_Event(Exception e) {
            LogToConsole("Disconnected_Event fired. Message: " + e.Message);

            systemState = systemState_e.DISCONNECTED;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called when the bot receives a log message from the discord API or service
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private Task Log_Event(LogMessage log) {
            LogToConsole("Log message: " + log.Message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Called whenever a message is received from ANYWHERE
        /// DMs, text channels, pretty much every message that the bot can see fires this event
        /// So, after some quick checks to determine if the bot is being directly addressed, fire off separate threads to handle the messages.
        /// We want to handle the messages in a separate thread so this message doesn't block for very long (The discord API starts getting unhappy)
        /// </summary>
        /// <param name="message">The message recived</param>
        /// <returns></returns>
        private Task MessageReceived_Event(SocketMessage message) {
            //First make sure we aren't replying to our own bot messages
            if (message.Author.Id == dClient.CurrentUser.Id) {
                return Task.CompletedTask;
            }

            if (message.MentionedUsers.Count == 1) {
                SocketUser su = message.MentionedUsers.ElementAt(0);
                if (su.Id == dClient.CurrentUser.Id) {
                    //Bot is mentioned in message, perform special actions
                    LogToConsole("@me message received: " + message.Content + "\n  Source Channel: " + message.Channel.Id.ToString());
                    Task.Run(() => ProcessAtMeMessage(message));    //Fire and forget
                }
            }

            Task.Run(() => ProcessNormalMessage(message));  //Fire and forget

            LogToConsole("Message received: " + message.Content + "\n   Source Channel: " + message.Channel.Name);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Called whenever a emoji reaction is added to a message.
        /// Here we can validate a user voting on a particular message if needed.
        /// </summary>
        /// <param name="cachedMessage">The cached message that the reaction was added to. Must ensure to download the message before use.</param>
        /// <param name="channel">The channel the message resides in</param>
        /// <param name="reaction">The actual reaction that was added</param>
        /// <returns></returns>
        private Task ReactionAdded_Event(Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel channel, SocketReaction reaction) {
            IUserMessage message = cachedMessage.GetOrDownloadAsync().Result;

            Task.Run(() => ProcessReactionMessage(message, channel, reaction));     //Fire and forget

            LogToConsole("Reaction: " + reaction.Emote.Name + " has been added to message: " + message.Id);

            return Task.CompletedTask;
        }

        #endregion

        /// <summary>
        /// We received a message with the format: @DiscordBotName "COMMAND"
        /// Decide what to do with it depending on the context and the actual command
        /// </summary>
        /// <param name="message"></param>
        private void ProcessAtMeMessage(SocketMessage message) {
            //Set the bot configuration channel in the database to the channel we received the message from
            if (message.Content.Contains("Set Config Channel", StringComparison.CurrentCultureIgnoreCase) == true) {
                SocketTextChannel sourceChannel = message.Channel as SocketTextChannel;
                if (sourceChannel == null) {
                    LogToConsole("Error updating config channel for server: " + message.Channel.Id.ToString());
                    OutputMessage(message.Channel, "Invalid channel. The bot configuration channel must be a text channel.");
                    return;
                }

                using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        dbConnection.Open();
                        cmd.CommandText = "UPDATE serverconfig SET ConfigChannelDiscordID = @p1 WHERE (DiscordID = @p2);";
                        cmd.Parameters.AddWithValue("@p1", sourceChannel.Id);
                        cmd.Parameters.AddWithValue("@p2", sourceChannel.Guild.Id);
                        if (cmd.ExecuteNonQuery() > 0) {
                            LogToConsole("Updated Config channel for server: " + sourceChannel.Guild.Id.ToString());
                            OutputMessage(sourceChannel, "Bot configuration channel has been set.");
                        }
                        else {
                            LogToConsole("Error updating config channel for server: " + sourceChannel.Guild.Id.ToString());
                            OutputMessage(message.Channel, "Problem setting bot configuration channel.");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// We received a message from a variety of sources, now determine what to actually do with the message
        /// </summary>
        /// <param name="message">The message in question</param>
        private void ProcessNormalMessage(SocketMessage message) {
            SocketTextChannel sourceChannel = message.Channel as SocketTextChannel;
            if (sourceChannel == null) {
                //TODO: May be a DM, check
                LogToConsole("Normal message received on a non text channel.");
                OutputMessage(message.Channel, "I'm sorry, I currently don't know how to handle this type of communication.");
                return;
            }

            //Query the database for (serverconfig.ConfigChannelDiscordID) to see if this message came from a configuration channel
            if (IsConfigChannel(message.Channel.Id, sourceChannel.Guild.Id) == true) {
                UserSession currentSession = GetActiveSession(message);     //Check to see if there's an ongoing user session

                //Check for top level commands
                if (message.Content.Contains("Set Output Channel", StringComparison.CurrentCultureIgnoreCase) == true) {
                    if (ProcessSetOutputChannel(message, sourceChannel) == true) {
                        return;
                    }
                } else if (message.Content.Contains("List Events", StringComparison.CurrentCultureIgnoreCase) == true) {
                    if (ProcessListAllEvents(message, sourceChannel) == true) {
                        return;
                    }
                } else if (message.Content.Contains("Create New Event", StringComparison.CurrentCultureIgnoreCase) == true) {
                    if (currentSession == null) {
                        if (ProcessCreateNewEvent(message, sourceChannel) == true) {
                            return;
                        }
                    } else if (currentSession.type == UserSession.SessionType_e.CREATE_EVENT) {
                        OutputMessage(sourceChannel, "You are currently already creating a new event. Please save/quit before creating a new event.");
                        return;
                    }
                }
                
                //If there's an ongoing user session, handle those commands differently
                if (currentSession != null) {
                    switch (currentSession.type) {
                        default:
                        case UserSession.SessionType_e.UNKNOWN:
                            LogToConsole("WARNING: Invalid session type specified.");
                            break;
                        case UserSession.SessionType_e.CREATE_EVENT:
                            if (ProcessSessionCreateEvent(currentSession, message, sourceChannel) == true) {
                                return;
                            }
                            break;
                    }
                }

                if (message.Content.Contains("Help", StringComparison.CurrentCultureIgnoreCase) == true) {
                    OutputMessage(sourceChannel, HELP_MESSAGE);
                    return;
                }
            }

            //Check to see if this message came from an output channel
            if (IsOutputChannel(message.Channel.Id, sourceChannel.Guild.Id) == true) {

            }
        }

        /// <summary>
        /// Determine if a text channel ID matches a server's set configuration channel by querying the database 'serverconfig' table
        /// </summary>
        /// <param name="channelID">The discord channel ID to test</param>
        /// <param name="serverID">The discord server ID to lookup in the database</param>
        /// <returns></returns>
        private bool IsConfigChannel(ulong channelID, ulong serverID) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT EXISTS (SELECT ConfigChannelDiscordID FROM serverconfig WHERE DiscordID = @p1 AND ConfigChannelDiscordID = @p2);";
                    cmd.Parameters.AddWithValue("@p1", serverID);
                    cmd.Parameters.AddWithValue("@p2", channelID);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) >= 1) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Determine if a text channel ID matches a server's set output channel by querying the database 'serverconfig' table
        /// </summary>
        /// <param name="channelID">The discord channel ID to test</param>
        /// <param name="serverID">The discord server ID to lookup in the database</param>
        /// <returns></returns>
        private bool IsOutputChannel(ulong channelID, ulong serverID) {
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                    dbConnection.Open();
                    cmd.CommandText = "SELECT EXISTS (SELECT OutputChanelDiscordID FROM serverconfig WHERE DiscordID = @p1 AND OutputChannelDiscordID = @p2);";
                    cmd.Parameters.AddWithValue("@p1", serverID);
                    cmd.Parameters.AddWithValue("@p2", channelID);
                    if (Convert.ToInt32(cmd.ExecuteScalar()) >= 1) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Helper function that outputs a message to the console with the date time in front
        /// </summary>
        /// <param name="s">Log message to output</param>
        private void LogToConsole(string s) {
            Console.WriteLine(DateTime.Now.ToLongTimeString() + " === " + s);
        }

        /// <summary>
        /// Output a message string to a Discord channel
        /// </summary>
        /// <param name="c">The discord channel to output the text message to</param>
        /// <param name="s">The message to output</param>
        /// <returns></returns>
        private IUserMessage OutputMessage(ISocketMessageChannel c, string s) {
            if (s.Length == 0) {
                return null;
            }
            var ret = c.SendMessageAsync(s);    //Send the message
            ret.Wait();                         //Wait until the message has actually been posted
            Thread.Sleep(BOT_MESSAGE_DELAY);    //Rate limits
            return ret.Result;
        }

        /// <summary>
        /// Output a reaction to a particular discord message
        /// </summary>
        /// <param name="msg">The discord message to add the reaction to</param>
        /// <param name="e">The reaction to add</param>
        private void OutputReaction(IUserMessage msg, Emoji e) {
            var ret = msg.AddReactionAsync(e);  //Add the reaction
            ret.Wait();                         //Wait until the reaction has actually been posted
            Thread.Sleep(BOT_MESSAGE_DELAY);    //Rate limits
        }

        /// <summary>
        /// Output a message string to a specified user via direct message
        /// </summary>
        /// <param name="user">The user to message directly</param>
        /// <param name="s">The message to send</param>
        private void OutputDMMessage(IUser user, string s) {
            var ret = user.SendMessageAsync(s); //Send the direct message
            ret.Wait();                         //Wait until the message has actually been sent
            Thread.Sleep(BOT_MESSAGE_DELAY);    //Rate limits
        }

        /// <summary>
        /// Create a new user session of a particular type and add it to the list of active sessions
        /// </summary>
        /// <param name="user">The user that is creating the session</param>
        /// <param name="channel">The channel that the session is valid in</param>
        /// <param name="type">The type of session to create</param>
        private void CreateNewUserSession(SocketUser user, SocketTextChannel channel, UserSession.SessionType_e type) {
            UserSession newSession = new UserSession();
            newSession.startTime = DateTime.Now;
            newSession.type = type;
            newSession.user = user;
            newSession.channel = channel;

            //The type of session pretty much determines what type of data will be stored in the 'sessionData' field.
            switch (type) {
                default:
                case UserSession.SessionType_e.UNKNOWN:
                    newSession.sessionData = null;
                    break;
                case UserSession.SessionType_e.CREATE_EVENT:
                    newSession.sessionData = new GameEvent();
                    break;
            }

            //Add this new session to the list of active sessions
            userSessionsSemaphore.Wait();
            userSessions.Add(newSession);
            userSessionsSemaphore.Release();
        }

        /// <summary>
        /// Close the current session out and remove it from the list of active sessions
        /// </summary>
        /// <param name="session">The session to close</param>
        private void CloseCurrentSession(UserSession session) {
            userSessionsSemaphore.Wait();
            userSessions.Remove(session);
            userSessionsSemaphore.Release();
        }

        /// <summary>
        /// Go through the list of active sessions and return the active session if it exists
        /// </summary>
        /// <param name="message">The message used to pull out the author and channel ID to test for the active user session</param>
        /// <returns>The active user session if it exists. Returns null if it does not.</returns>
        private UserSession GetActiveSession(SocketMessage message) {
            UserSession retVal = null;

            userSessionsSemaphore.Wait();
            foreach (UserSession us in userSessions) {
                if (us.user.Id == message.Author.Id && us.channel.Id == message.Channel.Id) {
                    us.startTime = DateTime.Now;
                    retVal = us;
                    break;
                }
            }
            userSessionsSemaphore.Release();

            return retVal;
        }

        #region Text Chat Message Handlers

        /// <summary>
        /// Sets the server config's text channel entry for where the bot should broadcast messages about upcoming events
        /// </summary>
        /// <param name="message">The 'SetOutputChannel' message</param>
        /// <param name="sourceChannel">The source of the message so we can figure out which server we need to update</param>
        /// <returns>True if the message should be consumed after execution</returns>
        private bool ProcessSetOutputChannel(SocketMessage message, SocketTextChannel sourceChannel) {
            bool retVal = true;

            string[] data = message.Content.Split('=');
            if (data.Length < 2) {
                OutputMessage(sourceChannel, "Please specify which channel you would like to set for the output channel.");
                return retVal;
            }

            bool channelSet = false;
            foreach (SocketTextChannel stc in sourceChannel.Guild.TextChannels) {
                if (stc.Name == data[1]) {
                    using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                        using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                            dbConnection.Open();
                            cmd.CommandText = "UPDATE serverconfig SET OutputChannelDiscordID = @p1 WHERE (DiscordID = @p2);";
                            cmd.Parameters.AddWithValue("@p1", stc.Id);
                            cmd.Parameters.AddWithValue("@p2", sourceChannel.Guild.Id);
                            if (cmd.ExecuteNonQuery() > 0) {
                                LogToConsole("Update Output channel for server: " + sourceChannel.Guild.Id.ToString());
                                OutputMessage(sourceChannel, "Bot output channel has been set to: " + stc.Name + ".");
                                channelSet = true;
                            }
                            else {
                                LogToConsole("ERROR updating output channel for server: " + sourceChannel.Guild.Id.ToString());
                                OutputMessage(message.Channel, "Datebase problems setting bot output channel. Please notify the system administrator.");
                                channelSet = true;
                            }
                        }
                    }
                }
            }

            //Haven't informed the user that no channel could be found
            if (channelSet == false) {
                OutputMessage(sourceChannel, "Could not find the text channel named: " + data[1] + ". Please double check your entry and try again.");
                return retVal;
            }

            return retVal;
        }

        /// <summary>
        /// Queries all current and pending game events for a particular server, then outputs them into the chat
        /// </summary>
        /// <param name="message">The requesting message</param>
        /// <param name="sourceChannel">The channel the message originated from</param>
        /// <returns></returns>
        private bool ProcessListAllEvents(SocketMessage message, SocketTextChannel sourceChannel) {
            bool retVal = true;

            List<GameEvent> events = new List<GameEvent>();

            //Query the database for a crap ton of information, and then save that information in the 'events' list
            using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                dbConnection.Open();
                using (MySqlTransaction trans = dbConnection.BeginTransaction()) {
                    using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                        cmd.Transaction = trans;
                        cmd.CommandText = "SELECT " +
                            "gameevents.GameEventID, gameevents.Title, gameevents.Description, " +
                            "gameevents.GameTypeStartVote, gameevents.GameTypeStartVotePosted, gameevents.GameTypeEndVote, gameevents.GameTypeEndVotePosted, " +
                            "gameevents.GameTimeStartVote, gameevents.GameTimeStartVotePosted, gameevents.GameTimeEndVote, gameevents.GameTimeEndVotePosted, " +
                            "gameevents.FinalGameType, gameevents.FinalGameTime " +
                            "FROM " +
                            "gameevents INNER JOIN serverconfig ON serverconfig.DiscordID = @p1 AND gameevents.Completed = '0' " +
                            "ORDER BY gameevents.GameEventID";
                        cmd.Parameters.AddWithValue("@p1", sourceChannel.Guild.Id);
                        using (MySqlDataReader reader = cmd.ExecuteReader()) {
                            while (reader.Read()) {
                                GameEvent newEvent = new GameEvent();
                                if (!reader.IsDBNull(0)) {
                                    newEvent.eventID = reader.GetUInt64(0);
                                }
                                if (!reader.IsDBNull(1)) {
                                    newEvent.Title = reader.GetString(1);
                                }
                                if (!reader.IsDBNull(2)) {
                                    newEvent.Description = reader.GetString(2);
                                }

                                if (!reader.IsDBNull(3)) {
                                    newEvent.gameTypeStartVoteDateTime = reader.GetDateTime(3);
                                }
                                if (!reader.IsDBNull(4)) {
                                    newEvent.gameTypeStartPosted = reader.GetInt32(4);
                                }
                                if (!reader.IsDBNull(5)) {
                                    newEvent.gameTypeEndVoteDateTime = reader.GetDateTime(5);
                                }
                                if (!reader.IsDBNull(6)) {
                                    newEvent.gameTypeEndPosted = reader.GetInt32(6);
                                }

                                if (!reader.IsDBNull(7)) {
                                    newEvent.gameTimeStartVoteDateTime = reader.GetDateTime(7);
                                }
                                if (!reader.IsDBNull(8)) {
                                    newEvent.gameTimeStartPosted = reader.GetInt32(8);
                                }
                                if (!reader.IsDBNull(9)) {
                                    newEvent.gameTimeEndVoteDateTime = reader.GetDateTime(9);
                                }
                                if (!reader.IsDBNull(10)) {
                                    newEvent.gameTimeEndPosted = reader.GetInt32(10);
                                }

                                if (!reader.IsDBNull(11)) {
                                    newEvent.finalGameType = reader.GetString(11);
                                }
                                if (!reader.IsDBNull(12)) {
                                    newEvent.finalGameTime = reader.GetDateTime(12);
                                }
                                events.Add(newEvent);
                            }
                        }

                        foreach (GameEvent ge in events) {
                            cmd.CommandText = "SELECT gametypevotes.GameType, gametypevotes.DiscordEmoji FROM gametypevotes WHERE gametypevotes.GameEventID = @p1;";
                            cmd.Parameters.Clear();
                            cmd.Parameters.AddWithValue("@p1", ge.eventID);
                            using (MySqlDataReader reader = cmd.ExecuteReader()) {
                                while (reader.Read()) {
                                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1)) {
                                        ge.gameTypes.Add(new VotedGameType(0, reader.GetString(0), reader.GetString(1)));
                                    }
                                }
                            }

                            cmd.CommandText = "SELECT gametimevotes.GameTime, gametimevotes.DiscordEmoji FROM gametimevotes WHERE gametimevotes.GameEventID = @p1;";
                            using (MySqlDataReader reader = cmd.ExecuteReader()) {
                                while (reader.Read()) {
                                    if (!reader.IsDBNull(0) && !reader.IsDBNull(1)) {
                                        ge.gameTimes.Add(new VotedGameTime(0, reader.GetDateTime(0), reader.GetString(1)));
                                    }
                                }
                            }
                        }
                    }
                    trans.Commit();
                }
            }

            //Now that we have a list of all the current and pending events for this particular server, output them into the same channel that we received the message from
            using (sourceChannel.EnterTypingState()) {
                string outputString = "=== ALL EVENTS FOR THIS SERVER ===\n";
                OutputMessage(sourceChannel, outputString);

                for (int i = 0; i < events.Count; i++) {
                    outputString = ":\nEvent: #" + (i + 1).ToString() + " : " + events[i].Title + "\n";
                    outputString += "   Description: " + events[i].Description + "\n";
                    outputString += "\n   Possible Game Types:\n";
                    foreach (VotedGameType vgt in events[i].gameTypes) {
                        if (vgt.messageEmoji.Length > 0) {
                            outputString += "   " + vgt.type + " - " + vgt.messageEmoji + "\n";
                        } else {
                            outputString += "   " + vgt.type + "\n";
                        }
                    }
                    outputString += "   Game type voting start time: " + DateTimeOutputString(events[i].gameTypeStartVoteDateTime) + " PST\n";
                    outputString += "   Game type voting end time: " + DateTimeOutputString(events[i].gameTypeEndVoteDateTime) + " PST\n";

                    outputString += "\n   Possible Game Times:\n";
                    foreach (VotedGameTime vgt in events[i].gameTimes) {
                        if (vgt.messageEmoji.Length > 0) {
                            outputString += "   " + DateTimeOutputString(vgt.dateTime) + " PST - " + vgt.messageEmoji + "\n";
                        } else {
                            outputString += "   " + DateTimeOutputString(vgt.dateTime) + "\n";
                        }
                    }
                    outputString += "   Game time voting start time: " + DateTimeOutputString(events[i].gameTimeStartVoteDateTime) + " PST\n";
                    outputString += "   Game time voting end time: " + DateTimeOutputString(events[i].gameTimeEndVoteDateTime) + " PST\n";

                    outputString += "\n";
                    OutputMessage(sourceChannel, outputString);
                }

                if (events.Count == 0) {
                    OutputMessage(sourceChannel, "No events currently active for this server.");
                }
            }

            return retVal;
        }

        /// <summary>
        /// Create a session to allow the user to begin creating a new game event
        /// </summary>
        /// <param name="message">The source message</param>
        /// <param name="sourceChannel">The channel where the message came from</param>
        /// <returns></returns>
        private bool ProcessCreateNewEvent(SocketMessage message, SocketTextChannel sourceChannel) {
            bool retVal = true;
            //Create new event needs to create a new user session so create one
            CreateNewUserSession(message.Author, sourceChannel, UserSession.SessionType_e.CREATE_EVENT);
            OutputMessage(sourceChannel, CREATE_NEW_EVENT_HELP_MESSAGE);
            return retVal;
        }

        /// <summary>
        /// This big ugly function goes through all the possible commands for creating a new event.
        /// It splits the message from the data via the equals sign '=' and then tries to parse the input data for each command
        /// </summary>
        /// <param name="session">The user session to update information from.</param>
        /// <param name="message">The source message with the command data</param>
        /// <param name="sourceChannel">The discord channel where the message came from</param>
        /// <returns></returns>
        private bool ProcessSessionCreateEvent(UserSession session, SocketMessage message, SocketTextChannel sourceChannel) {
            bool retVal = true;

            GameEvent sessionNewEvent = session.sessionData as GameEvent;
            if (sessionNewEvent == null) {
                OutputMessage(sourceChannel, "Corrupted user session. Please try again after 1 minute.");
                return retVal;
            }
            //Handle current session messages here
            string[] data = message.Content.Split('=', 2);
            if (data.Length < 2) {
                //Check for commands without a value

                if (string.Compare(data[0].Trim(), "save", true) == 0) {        //Save the event to the database
                    if (ValidateNewEvent(sessionNewEvent, sourceChannel) == false) {
                        return retVal;
                    }

                    LogToConsole("INFO: Saving new event to the database.");

                    using (dbConnection = new MySqlConnection(MysqlConnectionString)) {
                        dbConnection.Open();
                        using (MySqlTransaction trans = dbConnection.BeginTransaction()) {
                            using (MySqlCommand cmd = dbConnection.CreateCommand()) {
                                cmd.Transaction = trans;
                                cmd.CommandText = "INSERT INTO gameevents(ServerConfigID, Title, Description, GameTypeStartVote, GameTypeEndVote, GameTimeStartVote, GameTimeEndVote) " +
                                    "SELECT serverconfig.ServerConfigID, @p1, @p2, @p3, @p4, @p5, @p6 " +
                                    "FROM serverconfig WHERE serverconfig.DiscordID = @p7 LIMIT 1;";
                                cmd.Parameters.AddWithValue("@p1", sessionNewEvent.Title);
                                cmd.Parameters.AddWithValue("@p2", sessionNewEvent.Description);
                                cmd.Parameters.AddWithValue("@p3", sessionNewEvent.gameTypeStartVoteDateTime);
                                cmd.Parameters.AddWithValue("@p4", sessionNewEvent.gameTypeEndVoteDateTime);
                                cmd.Parameters.AddWithValue("@p5", sessionNewEvent.gameTimeStartVoteDateTime);
                                cmd.Parameters.AddWithValue("@p6", sessionNewEvent.gameTimeEndVoteDateTime);
                                cmd.Parameters.AddWithValue("@p7", sourceChannel.Guild.Id);
                                if (cmd.ExecuteNonQuery() == 0) {
                                    LogToConsole("Problem adding new event to database.");
                                }

                                cmd.CommandText = MySqlHelper.EscapeString("SET @newGameEventID = LAST_INSERT_ID();");
                                if (cmd.ExecuteNonQuery() != 0) {
                                    LogToConsole("Problem setting last_insert_id.");
                                }

                                for (int i = 0; i < sessionNewEvent.gameTypes.Count; i++) {
                                    cmd.CommandText = "INSERT INTO gametypevotes(GameEventID, GameType, DiscordEmoji) " +
                                        "VALUES (@newGameEventID, @p1, @p2);";
                                    cmd.Parameters.Clear();
                                    cmd.Parameters.AddWithValue("@p1", sessionNewEvent.gameTypes[i].type);
                                    cmd.Parameters.AddWithValue("@p2", sessionNewEvent.gameTypes[i].messageEmoji);
                                    if (cmd.ExecuteNonQuery() == 0) {
                                        LogToConsole("Problem adding game type vote entry to database.");
                                    }
                                }

                                for (int i = 0; i < sessionNewEvent.gameTimes.Count; i++) {
                                    cmd.CommandText = "INSERT INTO gametimevotes(GameEventID, GameTime, DiscordEmoji) " +
                                        "VALUES (@newGameEventID, @p1, @p2);";
                                    cmd.Parameters.Clear();
                                    cmd.Parameters.AddWithValue("@p1", sessionNewEvent.gameTimes[i].dateTime);
                                    cmd.Parameters.AddWithValue("@p2", sessionNewEvent.gameTimes[i].messageEmoji);
                                    if (cmd.ExecuteNonQuery() == 0) {
                                        LogToConsole("Problem adding game time vote entry to database.");
                                    }
                                }
                            }
                            trans.Commit();
                        }
                    }

                    OutputMessage(sourceChannel, "New event created, and will be posted on the announcement day.");
                    CloseCurrentSession(session);
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "summary", true) == 0) {      //Output a summary of the current new event we're editing
                    string outputString = "Here's the event you've created so far:\n" +
                        "Title: " + sessionNewEvent.Title + "\n" +
                        "Description: " + sessionNewEvent.Description + "\n" +
                        "Possible Game Types:\n";
                    foreach (VotedGameType vgt in sessionNewEvent.gameTypes) {
                        if (vgt.messageEmoji.Length > 0) {
                            outputString += "   " + vgt.type.ToString() + " - " + vgt.messageEmoji + "\n";
                        } else {
                            outputString += "   " + vgt.type.ToString() + "\n";
                        }
                    }
                    outputString += "Game type voting start time: " + DateTimeOutputString(sessionNewEvent.gameTypeStartVoteDateTime) + " PST\n";
                    outputString += "Game type voting end time: " + DateTimeOutputString(sessionNewEvent.gameTypeEndVoteDateTime) + " PST\n";

                    outputString += "Possible Game Times:\n";
                    foreach (VotedGameTime vgt in sessionNewEvent.gameTimes) {
                        if (vgt.messageEmoji.Length > 0) {
                            outputString += "   " + DateTimeOutputString(vgt.dateTime) + " PST - " + vgt.messageEmoji + "\n";
                        } else {
                            outputString += "   " + DateTimeOutputString(vgt.dateTime) + "\n";
                        }
                    }
                    outputString += "Game time voting start time: " + DateTimeOutputString(sessionNewEvent.gameTimeStartVoteDateTime) + " PST\n";
                    outputString += "Game time voting end time: " + DateTimeOutputString(sessionNewEvent.gameTimeEndVoteDateTime) + " PST\n";

                    OutputMessage(sourceChannel, outputString);
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "load default game types", true) == 0) {
                    if (LoadDefaultGameTypes(sessionNewEvent, sourceChannel)) {
                        OutputMessage(sourceChannel, "Default game types sucessfully added.");
                    }
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "clear game types", true) == 0) {
                    sessionNewEvent.gameTypes.Clear();
                    OutputMessage(sourceChannel, "Game types have been cleared.");
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "clear game times", true) == 0) {
                    sessionNewEvent.gameTimes.Clear();
                    OutputMessage(sourceChannel, "Game times have been cleared.");
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "help", true) == 0) {
                    //Print help here
                    OutputMessage(sourceChannel, CREATE_NEW_EVENT_HELP_MESSAGE);
                    return retVal;
                } else if (string.Compare(data[0].Trim(), "quit", true) == 0 || string.Compare(data[0].Trim(), "exit", true) == 0 || string.Compare(data[0].Trim(), "cancel", true) == 0) {        //Close out the session
                    OutputMessage(sourceChannel, "New event has been discarded.");
                    CloseCurrentSession(session);
                    return retVal;
                } else {
                    OutputMessage(sourceChannel, "Invalid command. Please check your syntax and try again.");
                    return retVal;
                }
            }

            if (string.Compare(data[0].Trim(), "title", true) == 0) {
                sessionNewEvent.Title = data[1].Trim();
            } else if (string.Compare(data[0].Trim(), "description", true) == 0) {
                sessionNewEvent.Description = data[1].Trim();
            } else if (string.Compare(data[0].Trim(), "game type", true) == 0) {
                //Look for a comma to see if there's a custom emoji to use for this game type entry
                data[1] = data[1].Trim();
                if (data[1].Contains(',') == true) {
                    string[] d2 = data[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (d2.Length == 2) {
                        string newEmoji = d2[1].Trim();
                        //Check for duplicate emojis...
                        bool isDuplicate = false;
                        foreach (VotedGameType vgt in sessionNewEvent.gameTypes) {
                            if (string.Compare(newEmoji, vgt.messageEmoji) == 0) {
                                isDuplicate = true;
                                break;
                            }
                        }
                        if (isDuplicate == false) {
                            sessionNewEvent.gameTypes.Add(new VotedGameType(0, d2[0].Trim(), newEmoji));
                        } else {
                            OutputMessage(sourceChannel, "Sorry, game type emojis must be unique to be added. Please choose a different emoji and try again.");
                        }
                    } else {
                        OutputMessage(sourceChannel, "I didn't understand that game type format. Please check your syntax and try again.");
                    }
                } else {
                    OutputMessage(sourceChannel, "An emoji is required for the voting process. Please add one and try again.");
                }
            } else if (string.Compare(data[0].Trim(), "game type vote start", true) == 0) {
                DateTime parsedDT = new DateTime();
                if (DateTime.TryParse(data[1].Trim(), out parsedDT) == true) {
                    sessionNewEvent.gameTypeStartVoteDateTime = parsedDT;
                } else {
                    OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                }
            } else if (string.Compare(data[0].Trim(), "game type vote end", true) == 0) {
                DateTime parsedDT = new DateTime();
                if (DateTime.TryParse(data[1].Trim(), out parsedDT) == true) {

                    sessionNewEvent.gameTypeEndVoteDateTime = parsedDT;
                } else {
                    OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                }
            } else if (string.Compare(data[0].Trim(), "game time", true) == 0) {
                //Look for a comma to see if there's a custom emoji to use for this game time entry
                data[1] = data[1].Trim();
                if (data[1].Contains(',') == true) {
                    string[] d2 = data[1].Split(',', StringSplitOptions.RemoveEmptyEntries);
                    if (d2.Length == 2) {
                        DateTime parsedDT = new DateTime();
                        if (DateTime.TryParse(d2[0].Trim(), out parsedDT) == true) {
                            string newEmoji = d2[1].Trim();
                            //Check for duplicate emojis...
                            bool isDuplicate = false;
                            foreach (VotedGameTime vgt in sessionNewEvent.gameTimes) {
                                if (string.Compare(newEmoji, vgt.messageEmoji) == 0) {
                                    isDuplicate = true;
                                    break;
                                }
                            }
                            if (isDuplicate == false) {
                                sessionNewEvent.gameTimes.Add(new VotedGameTime(0, parsedDT, newEmoji));
                            } else {
                                OutputMessage(sourceChannel, "Sorry, game time emojis must be unique to be added. Please choose a different emoji and try again.");
                            }
                        } else {
                            OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                        }
                    } else {
                        OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                    }
                } else {
                    OutputMessage(sourceChannel, "An emoji is required for the voting process. Please add one and try again.");
                }
            } else if (string.Compare(data[0].Trim(), "game time vote start", true) == 0) {
                DateTime parsedDT = new DateTime();
                if (DateTime.TryParse(data[1].Trim(), out parsedDT) == true) {
                    sessionNewEvent.gameTimeStartVoteDateTime = parsedDT;
                } else {
                    OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                }
            } else if (string.Compare(data[0].Trim(), "game time vote end", true) == 0) {
                DateTime parsedDT = new DateTime();
                if (DateTime.TryParse(data[1].Trim(), out parsedDT) == true) {
                    sessionNewEvent.gameTimeEndVoteDateTime = parsedDT;
                } else {
                    OutputMessage(sourceChannel, "I didn't understand that date/time format. Please check your syntax and try again.");
                }
            } else {
                OutputMessage(sourceChannel, "Invalid command. Please check your syntax and try again.");
            }

            return retVal;
        }

        private bool LoadDefaultGameTypes(GameEvent newEvent, SocketTextChannel sourceChannel) {
            string[] defaultGameTypes = {
                "Hide and Seek",
                "Michael Myers",
                "Normal VS",
                "Tactical Realism VS",
                "Strat Roulette VS",
                "Clan VS Clan"
            };
            string[] defaultGameTypeEmojis = {
                "\U0001F3C5",   //:medal:
                "\U0001F52A",   //:knife:
                "\U0001F396",   //:military_medal:
                "\U0001F3C6",   //:trophy:
                "\U0001F3B0",   //:slot_machine:
                "\U0001F93C"    //:men_wrestling:
            };

            bool isDuplicate = false;
            foreach (string s in defaultGameTypeEmojis) {
                foreach (VotedGameType vgt in newEvent.gameTypes) {
                    if (string.Compare(s, vgt.messageEmoji) == 0) {
                        isDuplicate = true;
                        break;
                    }
                }
                if (isDuplicate == true) {
                    break;
                }
            }

            if (isDuplicate == false) {
                for (int i = 0; i < defaultGameTypes.Length; i++) {
                    newEvent.gameTypes.Add(new VotedGameType(0, defaultGameTypes[i], defaultGameTypeEmojis[i]));
                }
                return true;
            } else {
                OutputMessage(sourceChannel, "Sorry, game type emojis must be unique to be added. Please try removing the duplicate emojis and adding the default list again.");
            }

            return false;
        }

        /// <summary>
        /// Perform various validation checks when a new game event is trying to be saved.
        /// </summary>
        /// <param name="newEvent">The event to validate.</param>
        /// <param name="sourceChannel">The channel where we can output error messages if needed.</param>
        /// <returns>True if the event is valid. False otherwise.</returns>
        private bool ValidateNewEvent(GameEvent newEvent, SocketTextChannel sourceChannel) {
            if (newEvent.gameTypes.Count == 0) {
                OutputMessage(sourceChannel, "Please specify at least one game type and try again.");
                return false;
            }

            if (newEvent.gameTimes.Count == 0) {
                OutputMessage(sourceChannel, "Please specify at least one game time and try again.");
                return false;
            }

            if (newEvent.gameTypeEndVoteDateTime <= newEvent.gameTypeStartVoteDateTime) {
                OutputMessage(sourceChannel, "It looks like the voting times for the game type are messed up. Please check them and try again.");
                return false;
            }

            if (newEvent.gameTimeEndVoteDateTime <= newEvent.gameTimeStartVoteDateTime) {
                OutputMessage(sourceChannel, "It looks like the voting times for the game time are messed up. Please check them and try again.");
                return false;
            }

            return true;
        }

        #endregion

        #region Reaction Handlers

        private void ProcessReactionMessage(IUserMessage message, ISocketMessageChannel channel, SocketReaction reaction) {
            SocketTextChannel textChannel = channel as SocketTextChannel;
            if (textChannel == null) {
                return;
            }

            //Check to see if this message came from an output channel
            if (IsOutputChannel(message.Channel.Id, textChannel.Guild.Id) == true) {
                LogToConsole("Reaction added to output channel message!");

                //Check to see what kind of reaction, and if needed change vote values
                //reaction.Emote.
            }
        }

        #endregion
    }
}