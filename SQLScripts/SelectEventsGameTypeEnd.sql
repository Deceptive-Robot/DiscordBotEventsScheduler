SELECT gametypevotes.GameTypeVoteID, gametypevotes.GameType, gameevents.GameEventID, gametypevotes.VoteCount, serverconfig.OutputChannelDiscordID, 
gameevents.Title, gameevents.Description, gameevents.GameTypeStartVote, gameevents.GameTypeEndVote, gameevents.GameTimeStartVote
FROM gameevents
INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID
LEFT JOIN gametypevotes ON gameevents.GameEventID = gametypevotes.GameEventID
WHERE gameevents.Completed = 0 AND gameevents.GameTypeEndVotePosted = 0
AND gameevents.GameTypeEndVote <= now();