START TRANSACTION;

INSERT INTO gameevents(ServerConfigID, Title, Description, Type, AnnouncementDateTime)
SELECT serverconfig.ServerConfigID, "Test Title 5", "Test Description 5", 'Test Type 5', "2019-10-30"
FROM serverconfig WHERE serverconfig.DiscordID = '339213216947109888' LIMIT 1;

SET @newGameEventID = LAST_INSERT_ID();

INSERT INTO eventdatetimes(GameEventID, DateTime, DateTimeVoteCount)
VALUES (@newGameEventID, '2019-12-30', 0);

INSERT INTO eventdatetimes(GameEventID, DateTime, DateTimeVoteCount)
VALUES (@newGameEventID, '2019-12-10', 1);

COMMIT;