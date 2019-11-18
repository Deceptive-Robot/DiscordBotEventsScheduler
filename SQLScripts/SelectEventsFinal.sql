SELECT serverconfig.OutputChannelDiscordID,
gameevents.GameEventID, gameevents.Title, gameevents.Description, gameevents.FinalGameType, gameevents.FinalGameTime
FROM discordbot.gameevents
INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID
WHERE gameevents.Completed = 0 AND gameevents.FinalGameTime <= now();