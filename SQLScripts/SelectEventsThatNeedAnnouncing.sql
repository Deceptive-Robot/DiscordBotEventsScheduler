SELECT gameevents.GameEventID, serverconfig.OutputChannelDiscordID, gameevents.Title, gameevents.Description, gameevents.Type, gameevents.AnnouncementDateTime, eventdatetimes.DateTime, eventdatetimes.DateTimeVoteCount FROM serverconfig
	INNER JOIN gameevents ON serverconfig.ServerConfigID = gameevents.ServerConfigID
	LEFT JOIN eventdatetimes ON gameevents.GameEventID = eventdatetimes.GameEventID
	WHERE gameevents.Completed = 0
	AND gameevents.AnnouncementPosted = 0
	AND gameevents.AnnouncementDateTime >= '2019-10-21 00:00:00'
	AND gameevents.AnnouncementDateTime < '2019-10-22 00:00:00'
	ORDER BY gameevents.GameEventID, gameevents.AnnouncementDateTime, eventdatetimes.DateTime;