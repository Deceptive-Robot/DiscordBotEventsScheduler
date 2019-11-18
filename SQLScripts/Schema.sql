CREATE DATABASE  IF NOT EXISTS `discordbot` /*!40100 DEFAULT CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci */ /*!80016 DEFAULT ENCRYPTION='N' */;
USE `discordbot`;
-- MySQL dump 10.13  Distrib 8.0.18, for Win64 (x86_64)
--
-- Host: localhost    Database: discordbot
-- ------------------------------------------------------
-- Server version	8.0.18

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET @OLD_CHARACTER_SET_RESULTS=@@CHARACTER_SET_RESULTS */;
/*!40101 SET @OLD_COLLATION_CONNECTION=@@COLLATION_CONNECTION */;
/*!50503 SET NAMES utf8 */;
/*!40103 SET @OLD_TIME_ZONE=@@TIME_ZONE */;
/*!40103 SET TIME_ZONE='+00:00' */;
/*!40014 SET @OLD_UNIQUE_CHECKS=@@UNIQUE_CHECKS, UNIQUE_CHECKS=0 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;
/*!40111 SET @OLD_SQL_NOTES=@@SQL_NOTES, SQL_NOTES=0 */;

--
-- Table structure for table `gameevents`
--

DROP TABLE IF EXISTS `gameevents`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `gameevents` (
  `GameEventID` bigint(11) NOT NULL AUTO_INCREMENT,
  `ServerConfigID` bigint(11) NOT NULL,
  `Title` text,
  `Description` text,
  `FinalGameType` text,
  `FinalGameTime` datetime DEFAULT NULL,
  `Completed` int(1) unsigned zerofill DEFAULT '0',
  `GameTypeStartVote` datetime DEFAULT NULL,
  `GameTypeStartVotePosted` int(1) unsigned zerofill DEFAULT '0',
  `GameTypeEndVote` datetime DEFAULT NULL,
  `GameTypeEndVotePosted` int(1) unsigned zerofill DEFAULT '0',
  `GameTimeStartVote` datetime DEFAULT NULL,
  `GameTimeStartVotePosted` int(1) unsigned zerofill DEFAULT '0',
  `GameTimeEndVote` datetime DEFAULT NULL,
  `GameTimeEndVotePosted` int(1) unsigned zerofill DEFAULT '0',
  `GameTypeDiscordMessageID` bigint(11) unsigned DEFAULT NULL,
  `GameTimeDiscordMessageID` bigint(11) unsigned DEFAULT NULL,
  PRIMARY KEY (`GameEventID`),
  UNIQUE KEY `GameEventID_UNIQUE` (`GameEventID`),
  KEY `ServerConfigID_idx` (`ServerConfigID`),
  CONSTRAINT `ServerConfigID` FOREIGN KEY (`ServerConfigID`) REFERENCES `serverconfig` (`ServerConfigID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=28 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Contains all the events that are scheduled to take place and should be considered when informing users about them...';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `gametimevotes`
--

DROP TABLE IF EXISTS `gametimevotes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `gametimevotes` (
  `GameTimeVoteID` bigint(11) NOT NULL AUTO_INCREMENT,
  `GameEventID` bigint(11) DEFAULT NULL,
  `GameTime` datetime DEFAULT NULL,
  `VoteCount` int(11) DEFAULT NULL,
  `DiscordEmoji` text,
  PRIMARY KEY (`GameTimeVoteID`),
  UNIQUE KEY `GameTimeVoteID_UNIQUE` (`GameTimeVoteID`),
  KEY `EventIDIndex_idx` (`GameEventID`),
  CONSTRAINT `GameTimeForeignKey` FOREIGN KEY (`GameEventID`) REFERENCES `gameevents` (`GameEventID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=33 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='The possible date/times for events. There can be many date and times that can be voted upon.';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `gametypevotes`
--

DROP TABLE IF EXISTS `gametypevotes`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `gametypevotes` (
  `GameTypeVoteID` bigint(11) NOT NULL AUTO_INCREMENT,
  `GameEventID` bigint(11) DEFAULT NULL,
  `GameType` text,
  `VoteCount` int(1) DEFAULT NULL,
  `DiscordEmoji` text,
  PRIMARY KEY (`GameTypeVoteID`),
  UNIQUE KEY `GameTypeVoteID_UNIQUE` (`GameTypeVoteID`),
  KEY `GameTypeForeignKey_idx` (`GameEventID`),
  CONSTRAINT `GameTypeForeignKey` FOREIGN KEY (`GameEventID`) REFERENCES `gameevents` (`GameEventID`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB AUTO_INCREMENT=22 DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `serverconfig`
--

DROP TABLE IF EXISTS `serverconfig`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `serverconfig` (
  `ServerConfigID` bigint(11) NOT NULL,
  `DiscordID` bigint(11) unsigned DEFAULT NULL,
  `ConfigChannelDiscordID` bigint(11) unsigned DEFAULT NULL,
  `OutputChannelDiscordID` bigint(11) unsigned DEFAULT NULL,
  PRIMARY KEY (`ServerConfigID`),
  UNIQUE KEY `ServerConfigID_UNIQUE` (`ServerConfigID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci COMMENT='Stores all the known servers along with their config details';
/*!40101 SET character_set_client = @saved_cs_client */;

--
-- Table structure for table `users`
--

DROP TABLE IF EXISTS `users`;
/*!40101 SET @saved_cs_client     = @@character_set_client */;
/*!50503 SET character_set_client = utf8mb4 */;
CREATE TABLE `users` (
  `UsersID` bigint(11) NOT NULL AUTO_INCREMENT,
  `DiscordID` bigint(11) unsigned DEFAULT NULL,
  PRIMARY KEY (`UsersID`),
  UNIQUE KEY `UsersID_UNIQUE` (`UsersID`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
/*!40101 SET character_set_client = @saved_cs_client */;
/*!40103 SET TIME_ZONE=@OLD_TIME_ZONE */;

/*!40101 SET SQL_MODE=@OLD_SQL_MODE */;
/*!40014 SET FOREIGN_KEY_CHECKS=@OLD_FOREIGN_KEY_CHECKS */;
/*!40014 SET UNIQUE_CHECKS=@OLD_UNIQUE_CHECKS */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
/*!40101 SET CHARACTER_SET_RESULTS=@OLD_CHARACTER_SET_RESULTS */;
/*!40101 SET COLLATION_CONNECTION=@OLD_COLLATION_CONNECTION */;
/*!40111 SET SQL_NOTES=@OLD_SQL_NOTES */;

-- Dump completed on 2019-11-17 17:23:35
