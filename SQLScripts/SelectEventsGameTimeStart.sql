SELECT gametimevotes.GameTimeVoteID, gametimevotes.GameTime, gameevents.GameEventID, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTimeStartVote, gameevents.GameTimeEndVote
FROM gameevents
INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID
LEFT JOIN gametimevotes ON gameevents.GameEventID = gametimevotes.GameEventID
WHERE gameevents.Completed = 0 AND gameevents.GameTimeStartVotePosted = 0
AND gameevents.GameTimeStartVote <= now();