SELECT gameevents.GameEventID, gameevents.Title, gameevents.Description,
	gameevents.GameTypeStartVote, gameevents.GameTypeStartVotePosted, gameevents.GameTypeEndVote, gameevents.GameTypeEndVotePosted,
    gameevents.GameTimeStartVote, gameevents.GameTimeStartVotePosted, gameevents.GameTimeEndVote, gameevents.GameTypeEndVotePosted,
    gameevents.FinalGameType, gameevents.FinalGameTime
    FROM
	gameevents INNER JOIN serverconfig ON serverconfig.DiscordID = '339213216947109888' AND gameevents.Completed = '0'
    ORDER BY gameevents.GameEventID;