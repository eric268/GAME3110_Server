using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class NetworkedServerProcessing : MonoBehaviour
{
    static public void ReceivedMessageFromClient(string msg, int clientConnectionID)
    {
        if (msg == null)
        {
            Debug.Log("Found error");
            int val = 1009;
            val--;
        }

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToSeverSignifiers.CreateAccount)
        {
            string n = csv[1];
            string p = csv[2];

            bool isUnique = true;
            foreach (PlayerAccount pa in GameLogic.accountInfo)
            {
                if (pa.userName == n)
                {
                    isUnique = false;
                    break;
                }
            }
            if (!isUnique)
            {
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureNameInUse, clientConnectionID);
                return;
            }
            else
            {
                GameLogic.accountInfo.AddLast(new PlayerAccount(n, p, 0));
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.Success + "," + n, clientConnectionID);
                GameLogic.SavePlayerAccounts();
            }
        }
        else if (signifier == ClientToSeverSignifiers.Login)
        {
            string n = csv[1];
            string p = csv[2];
            bool hasBeenFound = false;

            foreach (PlayerAccount pa in GameLogic.accountInfo)
            {
                if (pa.userName == n)
                {
                    if (pa.password == p)
                    {
                        SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.Success + "," + n, clientConnectionID);
                        return;
                    }
                    else
                    {
                        SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureIncorrectPassword, clientConnectionID);
                        return;
                    }
                }

            }
            if (!hasBeenFound)
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureNameNotFound, clientConnectionID);
        }
        else if (signifier == ClientToSeverSignifiers.AddToGameSessionQueue)
        {
            if (gameLogic.playerWaitingForMatch == -1)
            {
                gameLogic.playerWaitingForMatch = clientConnectionID;
            }
            else
            {
                int gameRoomID = GameSessionManager.GetGameSessionIDNumber();
                GameSession gs = new GameSession(gameLogic.playerWaitingForMatch, clientConnectionID, gameRoomID);
                gameLogic.gameSessionManager.allGameSessions.Add(gs);

                int randomNumberForGameSymbol = Random.Range(0, 2);
                string playerWaitingForMatchSymbol = (randomNumberForGameSymbol == 0) ? "X" : "O";
                string currentPlayersSymbol = (playerWaitingForMatchSymbol == "X") ? "O" : "X";

                int playerWaitingForMatchMovesFirst = Random.Range(0, 2);
                int currentPlayersMove = (playerWaitingForMatchMovesFirst == 1) ? 0 : 1;

                SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionStarted.ToString(), playerWaitingForMatchSymbol, playerWaitingForMatchMovesFirst, gameRoomID.ToString()), gameLogic.playerWaitingForMatch);
                SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionStarted, currentPlayersSymbol, currentPlayersMove, gameRoomID.ToString()) + "", clientConnectionID);

                gameLogic.playerWaitingForMatch = -1;
            }

        }
        else if (signifier == ClientToSeverSignifiers.TicTacToePlay)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);

            if (gs.playerID1 == clientConnectionID)
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentTicTacToePlay + "", gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentTicTacToePlay + "", gs.playerID1);
            }
        }
        else if (signifier == ClientToSeverSignifiers.TicTacToeMoveMade)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);

            if (gs != null)
            {
                if (gs.playerID1 == clientConnectionID)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.OpponentPlayedAMove.ToString(), csv[1]), gs.playerID2);
                }
                else
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.OpponentPlayedAMove.ToString(), csv[1]), gs.playerID1);
                }
                foreach (int observerID in gs.observerIDs)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.UpdateObserverOnMoveMade.ToString(), csv[1], csv[2]), observerID);
                }
            }
        }
        else if (signifier == ClientToSeverSignifiers.GameOver)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);

            if (gs.playerID1 == clientConnectionID)
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString() + "," + csv[1], gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString() + "," + csv[1], gs.playerID1);
            }
            foreach (PlayerAccount p in GameLogic.accountInfo)
            {
                if (p.userName == csv[1])
                {
                    p.totalNumberOfWins++;
                    GameLogic.SavePlayerAccounts();
                }
            }
        }
        else if (signifier == ClientToSeverSignifiers.GameDrawn)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);

            if (gs.playerID1 == clientConnectionID)
            {
                SendMessageToClient(ServertoClientSignifiers.GameDrawn.ToString(), gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.GameDrawn.ToString(), gs.playerID1);
            }
        }
        else if (signifier == ClientToSeverSignifiers.RestartGame)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);

            if (gs.playerID1 == clientConnectionID)
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentRestartedGame.ToString(), gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.GameDrawn.ToString(), gs.playerID1);
            }
        }
        else if (signifier == ClientToSeverSignifiers.ShowLeaderboard)
        {
            IEnumerable<PlayerAccount> sortedAccountInfo = GameLogic.accountInfo.OrderByDescending(item => item.totalNumberOfWins);

            int playersOnLeaderboardSignifier = (sortedAccountInfo.Count() >= 10) ? 10 : sortedAccountInfo.Count();
            string streamOfPlayersOnLeaderboard = "," + playersOnLeaderboardSignifier.ToString();
            foreach (PlayerAccount p in sortedAccountInfo)
            {
                streamOfPlayersOnLeaderboard += ("," + p.userName + "," + p.totalNumberOfWins);
            }
            SendMessageToClient(ServertoClientSignifiers.LeaderboardShowRequest.ToString() + streamOfPlayersOnLeaderboard, clientConnectionID);

        }
        else if (signifier == ClientToSeverSignifiers.PlayerSentMessageInChat)
        {
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);
            if (gs != null)
            {
                if (gs.playerID1 == clientConnectionID)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID2);
                }
                else
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID1);
                }

                foreach (int observerNum in gs.observerIDs)
                {
                    if (observerNum != clientConnectionID)
                        SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), observerNum);
                }
            }
            else
            {
                gs = gameLogic.FindGameSessionWithObserverID(clientConnectionID);
                if (gs != null)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID1);
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID2);

                    foreach (int observerNum in gs.observerIDs)
                    {
                        if (observerNum != clientConnectionID)
                            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), observerNum);

                    }
                }

            }
        }
        else if (signifier == ClientToSeverSignifiers.SearchGameRoomRequestMade)
        {
            //bool gameSessionFound = false;
            int searchedGameID = int.Parse(csv[1]);
            foreach (GameSession gs in gameLogic.gameSessionManager.allGameSessions)
            {
                if (searchedGameID == gs.gameRoomID)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.GetCellsOfTicTacToeBoard.ToString(), clientConnectionID.ToString()), gs.playerID1);
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionSearchResponse, GameRoomSearchResponse.SearchSucceeded), clientConnectionID);
                    gs.observerIDs.Add(clientConnectionID);
                    return;
                }
            }
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionSearchResponse, GameRoomSearchResponse.SearchFailed), clientConnectionID);
        }
        else if (signifier == ClientToSeverSignifiers.SendCellsOfTicTacToeBoardToServer)
        {
            int requesterID = int.Parse(csv[1]);
            string boardResults = csv[2];
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendTicTacToeCellsToObserver.ToString(), boardResults), requesterID);
        }
        else if (signifier == ClientToSeverSignifiers.RequestNumberOfSavedRecordings)
        {
            string userName = csv[1];
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendNumberOfSavedRecordings.ToString(), gameLogic.LoadRecordingsWithFiltering(RecordingSearchCriteria.INCLUDE_ONLY_USER, userName).Count), clientConnectionID);
        }
        else if (signifier == ClientToSeverSignifiers.ClearRecordingOnServer)
        {
            string userName = csv[1];
            gameLogic.DeleteRecordingsBelongingToUser(userName);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendNumberOfSavedRecordings.ToString(), 0), clientConnectionID);
        }
        else if (signifier == ClientToSeverSignifiers.PlayerLeftGameRoom)
        {
            Debug.Log("Player left game room");
            string sender = "System";
            string message = "Player left room";
            GameSession gs = gameLogic.FindGameSessionWithPlayerID(clientConnectionID);
            if (gs != null)
            {
                if (clientConnectionID == gs.playerID1)
                    gs.player1InRoom = false;
                else
                    gs.player2InRoom = false;

                if (!gs.player1InRoom && !gs.player2InRoom)
                {
                    foreach (int observerNum in gs.observerIDs)
                    {
                        if (observerNum != clientConnectionID)
                            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), observerNum);
                    }
                    gameLogic.gameSessionManager.allGameSessions.Remove(gs);
                }
                else
                {
                    if (clientConnectionID == gs.playerID1)
                        SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), gs.playerID2);
                    else
                        SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), gs.playerID1);
                    foreach (int observerNum in gs.observerIDs)
                    {
                        if (observerNum != clientConnectionID)
                            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), observerNum);
                    }
                }
            }
        }
        else if (signifier == ClientToSeverSignifiers.PlayerHasLeftGameQueue)
        {
            gameLogic.playerWaitingForMatch = -1;
        }
        else if (signifier == ClientToSeverSignifiers.BeginSendingRecording)
        {
            gameLogic.currentReplay = new ReplayRecorder();
        }
        else if (signifier == ClientToSeverSignifiers.SendRecordedPlayersUserName)
        {
            gameLogic.currentReplay.username = csv[1];
        }
        else if (signifier == ClientToSeverSignifiers.SendRecordedNumberOfTurns)
        {
            gameLogic.currentReplay.numberOfTurns =int.Parse(csv[1]);
        }
        else if (signifier == ClientToSeverSignifiers.SendRecordedGamesStartingSymbol)
        {
            gameLogic.currentReplay.startingSymbol = csv[1];
        }
        else if (signifier == ClientToSeverSignifiers.SendRecordedGamesTimeBetweenTurns)
        {
            //Starting at 1 cause of course signifier is 0
            for (int i = 1; i < csv.Length; i++)
            {
                gameLogic.currentReplay.timeBetweenTurnsArray.Add(float.Parse(csv[i]));
            }
        }
        else if (signifier == ClientToSeverSignifiers.SendRecordedGamesIndexOfMoveLocation)
        {
            for (int i = 1; i < csv.Length; i++)
            {
                gameLogic.currentReplay.cellNumberOfTurn.Add(int.Parse(csv[i]));
            }
        }
        else if (signifier == ClientToSeverSignifiers.FinishedSendingRecordingToServer)
        {
            string serializedRecording = gameLogic.SerializeReplayRecorder(gameLogic.currentReplay);
            gameLogic.replayManager.Add(serializedRecording);
            gameLogic.SaveRecordings();
        }
        else if (signifier == ClientToSeverSignifiers.RecordingRequestedFromServer)
        {
            string userName = csv[1];
            int recordingNumber = int.Parse(csv[2]);
            string recordingInfo = gameLogic.LoadRecordingsWithFiltering(RecordingSearchCriteria.INCLUDE_ONLY_USER, userName)[recordingNumber];

            gameLogic.currentReplay = gameLogic.DeserializeReplayRecording(recordingInfo);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.RecordingStartingToBeSentToClient.ToString()), clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.ServerSentRecordingUserName.ToString() + "," + gameLogic.currentReplay.username), clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.ServerSentRecordedStartingSymbol.ToString() + "," + gameLogic.currentReplay.startingSymbol), clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.ServerSentRecordedNumberOfTurns.ToString() + "," + gameLogic.currentReplay.numberOfTurns) , clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.ServerSentRecordedTimeBetweenTurns.ToString() + gameLogic.currentReplay.SerializeReplayTimes()) , clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.ServerSentRecordedIndexOfMoveLocation.ToString() + gameLogic.currentReplay.SerializeReplayMoveIndex()), clientConnectionID);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.RecordingFinishedSendingToClient.ToString()), clientConnectionID);
        }
    }

    
    static public void SendMessageToClient(string msg, int clientConnectionID)
    {
        networkedServer.SendMessageToClient(msg, clientConnectionID);
    }

    static public void ConnectionEvent(int clientConnectionID)
    {
        Debug.Log("New Connection, ID == " + clientConnectionID);
        gameLogic.AddConnectedClient(clientConnectionID);
    }
    static public void DisconnectionEvent(int clientConnectionID)
    {
        Debug.Log("New Disconnection, ID == " + clientConnectionID);
        gameLogic.PlayerDisconnectedFromGameSession(clientConnectionID);
    }

    static NetworkedServer networkedServer;
    static GameLogic gameLogic;

    static public void SetNetworkedServer(NetworkedServer NetworkedServer)
    {
        networkedServer = NetworkedServer;
    }
    static public NetworkedServer GetNetworkedServer()
    {
        return networkedServer;
    }
    static public void SetGameLogic(GameLogic GameLogic)
    {
        gameLogic = GameLogic;
    }
    public static class ClientToSeverSignifiers
    {
        public const int Login = 1;
        public const int CreateAccount = 2;
        public const int AddToGameSessionQueue = 3;
        public const int TicTacToePlay = 4;
        public const int TicTacToeMoveMade = 5;
        public const int GameOver = 6;
        public const int GameDrawn = 7;
        public const int RestartGame = 8;
        public const int ShowLeaderboard = 9;
        public const int PlayerSentMessageInChat = 10;
        public const int SearchGameRoomRequestMade = 11;
        public const int SendCellsOfTicTacToeBoardToServer = 12;

        public const int RequestNumberOfSavedRecordings = 13;
        public const int ClearRecordingOnServer = 14;
        public const int PlayerLeftGameRoom = 15;
        public const int PlayerHasLeftGameQueue = 16;

        public const int RecordingRequestedFromServer = 17;

        public const int BeginSendingRecording = 18;
        public const int SendRecordedPlayersUserName = 19;
        public const int SendRecordedNumberOfTurns = 20;
        public const int SendRecordedGamesStartingSymbol = 21;
        public const int SendRecordedGamesTimeBetweenTurns = 22;
        public const int SendRecordedGamesIndexOfMoveLocation = 23;

        public const int FinishedSendingRecordingToServer = 24;
    }

    public static class ServertoClientSignifiers
    {
        public const int LoginResponse = 1;
        public const int GameSessionStarted = 2;
        public const int OpponentTicTacToePlay = 3;
        public const int OpponentPlayedAMove = 4;
        public const int OpponentWon = 5;
        public const int GameDrawn = 6;
        public const int OpponentRestartedGame = 7;
        public const int LeaderboardShowRequest = 8;
        public const int SendPlayerChatToOpponent = 9;
        public const int SearchFoundValidGameRoom = 10;
        public const int GetCellsOfTicTacToeBoard = 11;
        public const int SendTicTacToeCellsToObserver = 12;
        public const int UpdateObserverOnMoveMade = 13;

        public const int SendNumberOfSavedRecordings = 14;
        public const int ReloadDropDownMenu = 15;
        public const int GameSessionSearchResponse = 16;

        public const int RecordingStartingToBeSentToClient = 17;

        public const int ServerSentRecordingUserName = 18;
        public const int ServerSentRecordedNumberOfTurns = 19;
        public const int ServerSentRecordedStartingSymbol = 20;
        public const int ServerSentRecordedTimeBetweenTurns = 21;
        public const int ServerSentRecordedIndexOfMoveLocation = 22;

        public const int RecordingFinishedSendingToClient = 23;
    }

    public static class LoginResponse
    {
        public const int Success = 1;

        public const int FailureNameInUse = 2;

        public const int FailureNameNotFound = 3;

        public const int FailureIncorrectPassword = 4;
    }

    public static class GameRoomSearchResponse
    {
        public const int SearchSucceeded = 1;
        public const int SearchFailed = 2;
    }
}