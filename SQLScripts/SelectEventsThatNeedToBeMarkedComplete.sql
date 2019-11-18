UPDATE gameevents
SET Completed = '1'
WHERE Completed = '0' AND GameTypeStartVotePosted > '0' AND GameTypeEndVotePosted > '0' AND GameTimeStartVotePosted > '0' AND GameTimeEndVotePosted > '0'
AND ADDTIME(FinalGameTime, '01:00:00') < now()