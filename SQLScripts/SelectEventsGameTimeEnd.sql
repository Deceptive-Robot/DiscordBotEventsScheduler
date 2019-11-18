SELECT gametimevotes.GameTimeVoteID, gametimevotes.GameTime, gameevents.GameEventID, gametimevotes.VoteCount,
	serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.GameTimeStartVote, gameevents.GameTimeEndVote
FROM gameevents
INNER JOIN serverconfig ON serverconfig.ServerConfigID = gameevents.ServerConfigID
LEFT JOIN gametimevotes ON gameevents.GameEventID = gametimevotes.GameEventID
WHERE gameevents.Completed = 0 AND gameevents.GameTimeEndVotePosted = 0
AND gameevents.GameTimeEndVote <= now();