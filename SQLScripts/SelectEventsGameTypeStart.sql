SELECT gametypevotes.GameTypeVoteID, gametypevotes.GameType, 
gameevents.GameEventID, serverconfig.OutputChannelDiscordID, 
gameevents.Title, gameevents.Description, gameevents.GameTypeStartVote, gameevents.GameTypeEndVote
FROM gameevents
INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID
LEFT JOIN gametypevotes ON gameevents.GameEventID = gametypevotes.GameEventID
WHERE gameevents.Completed = 0 AND gameevents.GameTypeStartVotePosted = 0
AND gameevents.GameTypeStartVote <= now()
ORDER BY gameevents.GameEventID;