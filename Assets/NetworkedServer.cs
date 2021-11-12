using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using System.Linq;
using UnityEngine.UI;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 25565;
    const string fileName = "AccountInfoSaveFile.txt";
    const string recordingFileName = "RecordingInfoSaveFile.txt";

    LinkedList<GameSession> gameSessions;
    List<string> replayManager;
    GameSessionManager gameSessionManager;
    int playerWaitingForMatch = -1;
    const int PlayerAccountIdentifyer = 1;

    private static LinkedList<PlayerAccount> accountInfo;

    // Start is called before the first frame update
    void Start()
    {
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        accountInfo = new LinkedList<PlayerAccount>();
        gameSessionManager = new GameSessionManager();
        replayManager = new List<string>();
        LoadPlayerAccounts();
        LoadAllRecordings();
    }

    // Update is called once per frame
    void Update()
    {

        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }

    }
  
    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }
    
    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg received = " + msg + ".  connection id = " + id);

        string[] csv = msg.Split(',');
        int signifier = int.Parse(csv[0]);

        if (signifier == ClientToSeverSignifiers.CreateAccount)
        {
            string n = csv[1];
            string p = csv[2];

            bool isUnique = true;
            foreach(PlayerAccount pa in accountInfo)
            {
                if (pa.userName == n)
                {
                    isUnique = false;
                    break;
                }
            }
            if (!isUnique)
            {
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureNameInUse, id);
            }
            else
            {
                accountInfo.AddLast(new PlayerAccount(n, p,0));
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.Success + "," + n, id);
                SavePlayerAccounts();
            }
        }
        else if (signifier == ClientToSeverSignifiers.Login)
        {
            string n = csv[1];
            string p = csv[2];
            bool hasBeenFound = false;
           

            foreach (PlayerAccount pa in accountInfo)
            {
                if (pa.userName == n)
                {
                    if(pa.password == p)
                    {
                        SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.Success + "," + n, id);
                    }
                    else
                    {
                        SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureIncorrectPassword, id);
                        
                        break;
                    }
                    hasBeenFound = true;
                    break;
                }

            }
            if (!hasBeenFound)
                SendMessageToClient(ServertoClientSignifiers.LoginResponse + "," + LoginResponse.FailureNameNotFound, id);
            }
        else if (signifier == ClientToSeverSignifiers.AddToGameSessionQueue)
        {
            if (playerWaitingForMatch == -1)
            {
                playerWaitingForMatch = id;
            }
            else
            {
                int gameRoomID = GameSessionManager.GetGameSessionIDNumber();
                GameSession gs = new GameSession(playerWaitingForMatch, id, gameRoomID);
                gameSessionManager.allGameSessions.Add(gs);

                int randomNumberForGameSymbol = Random.Range(0, 2);
                string playerWaitingForMatchSymbol = (randomNumberForGameSymbol == 0) ? "X" : "O";
                string currentPlayersSymbol = (playerWaitingForMatchSymbol == "X") ? "O" : "X";

                

                int playerWaitingForMatchMovesFirst = Random.Range(0, 2);
                int currentPlayersMove = (playerWaitingForMatchMovesFirst == 1) ? 0 : 1;

                SendMessageToClient(string.Join(",",ServertoClientSignifiers.GameSessionStarted.ToString(), playerWaitingForMatchSymbol, playerWaitingForMatchMovesFirst, gameRoomID.ToString()), playerWaitingForMatch);
                SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionStarted, currentPlayersSymbol, currentPlayersMove, gameRoomID.ToString()) + "", id);

                playerWaitingForMatch = -1;
            }

        }
        else if (signifier == ClientToSeverSignifiers.TicTacToePlay)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs.playerID1 == id)
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
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs != null)
            {
                if (gs.playerID1 == id)
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
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs.playerID1 == id)
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString() + "," + csv[1], gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString() + "," + csv[1], gs.playerID1);
            }
            foreach(PlayerAccount p in accountInfo)
            {
                if (p.userName == csv[1])
                {
                    p.totalNumberOfWins++;
                    SavePlayerAccounts();
                }
            }
        }
        else if (signifier == ClientToSeverSignifiers.GameDrawn)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs.playerID1 == id)
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
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs.playerID1 == id)
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
            IEnumerable<PlayerAccount> sortedAccountInfo = accountInfo.OrderByDescending(item => item.totalNumberOfWins);

            int playersOnLeaderboardSignifier = (sortedAccountInfo.Count() >= 10) ? 10 : sortedAccountInfo.Count();
            string streamOfPlayersOnLeaderboard =  "," + playersOnLeaderboardSignifier.ToString();
            foreach (PlayerAccount p in sortedAccountInfo)
            {
                streamOfPlayersOnLeaderboard += ("," + p.userName + "," + p.totalNumberOfWins);
            }
            SendMessageToClient(ServertoClientSignifiers.LeaderboardShowRequest.ToString() + streamOfPlayersOnLeaderboard, id);

        }
        else if (signifier == ClientToSeverSignifiers.PlayerSentMessageInChat)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);
            if (gs != null)
            {
                if (gs.playerID1 == id)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID2);
                }
                else
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID1);
                }

                foreach (int observerNum in gs.observerIDs)
                {
                    if (observerNum != id)
                        SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), observerNum);
                }
            }
            else
            {
                gs = FindGameSessionWithObserverID(id);
                if (gs != null)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID1);
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), gs.playerID2);

                    foreach(int observerNum in gs.observerIDs)
                    {
                        if (observerNum != id)
                            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), csv[1], csv[2]), observerNum);

                    }
                }

            }
        }
        else if (signifier == ClientToSeverSignifiers.SearchGameRoomRequestMade)
        {
            //bool gameSessionFound = false;
            int searchedGameID = int.Parse(csv[1]);
            foreach (GameSession gs in gameSessionManager.allGameSessions)
            {
                if (searchedGameID == gs.gameRoomID)
                {
                    SendMessageToClient(string.Join(",", ServertoClientSignifiers.GetCellsOfTicTacToeBoard.ToString(), id.ToString()), gs.playerID1);
                    gs.observerIDs.Add(id);
                }
                break;
            }
        }
        else if (signifier == ClientToSeverSignifiers.SendCellsOfTicTacToeBoardToServer)
        {
            int requesterID = int.Parse(csv[1]);
            string boardResults = csv[2];
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendTicTacToeCellsToObserver.ToString(), boardResults), requesterID);
        }
        else if (signifier == ClientToSeverSignifiers.RecordingSentToServer)
        {
            //Want to remove the signifier and , then save it all as its already formatted
            int lengthOfSubString = msg.Length - 3;
            string trimmedMessage = msg.Substring(3, lengthOfSubString);
            replayManager.Add(trimmedMessage);
            SaveRecordings();
        }
        else if (signifier == ClientToSeverSignifiers.RecordingRequestedFromServer)
        {
            string userName = csv[1];
            int recordingNumber = int.Parse(csv[2]);
            string recordingInfo = LoadRecordings(userName)[recordingNumber];
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.RecordingSentToClient.ToString(), recordingInfo), id);
        }
        else if (signifier == ClientToSeverSignifiers.RequestNumberOfSavedRecordings)
        {
            string userName = csv[1];
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendNumberOfSavedRecordings.ToString(), LoadRecordings(userName).Count), id);
        }
        else if (signifier == ClientToSeverSignifiers.ClearRecordingOnServer)
        {
            string userName = csv[1];
            DeleteRecordingsBelongingToUser(userName);
            SendMessageToClient(string.Join(",", ServertoClientSignifiers.SendNumberOfSavedRecordings.ToString(), LoadRecordings(userName).Count), id);
        }
    }

    static public void LoadPlayerAccounts()
    {
        string path = Application.dataPath + Path.DirectorySeparatorChar + fileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while((line = sr.ReadLine()) != null)
            {
                string[] arr = line.Split(',');
                int saveDataIdentifyer = int.Parse(arr[0]);
                if (saveDataIdentifyer == PlayerAccountIdentifyer)
                {
                    PlayerAccount player = new PlayerAccount(arr[1], arr[2], int.Parse(arr[3]));
                    accountInfo.AddLast(player);
                }
            }
            sr.Close();
        }
    }

    static public void SavePlayerAccounts()
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + fileName);
        foreach (PlayerAccount player in accountInfo)
        {
            sw.WriteLine(PlayerAccountIdentifyer + "," + player.userName + "," + player.password + "," + player.totalNumberOfWins);
        }
        sw.Close();
    }
    private GameSession FindGameSessionWithPlayerID(int id)
    {
        foreach (GameSession gs in gameSessionManager.allGameSessions)
        {
            if (id == gs.playerID1 || id == gs.playerID2)
                return gs;
        }
        return null;
    }
    private GameSession FindGameSessionWithObserverID(int id)
    {
        foreach (GameSession gs in gameSessionManager.allGameSessions)
        {
            foreach(int observerID in gs.observerIDs)
            {
                if (observerID == id)
                    return gs;
            }
        }
        return null;
    }


    public void SaveRecordings()
    {
        StreamWriter sw = new StreamWriter(Application.dataPath + Path.DirectorySeparatorChar + recordingFileName);
        foreach (var recording in replayManager)
        {
            sw.WriteLine(recording);
            Debug.Log("Saved Recording: " + recording);
        }
        sw.Close();
    }
    public static List<string> LoadRecordings(string userName)
    {
        List<string> playerReplayManager = new List<string>();
        string path = Application.dataPath + Path.DirectorySeparatorChar + recordingFileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');
                if (csv[0] == userName)
                {
                    playerReplayManager.Add(line);
                    Debug.Log("Loaded Recording: " + line);
                }
            }
            sr.Close();
        }
        return playerReplayManager;
    }

    public void LoadAllRecordings()
    {
        string path = Application.dataPath + Path.DirectorySeparatorChar + recordingFileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                    replayManager.Add(line);   
            }
            sr.Close();
        }
    }

    public void DeleteRecordingsBelongingToUser(string userName)
    {
        List<string> recordingsToNotDelete = new List<string>();
        string path = Application.dataPath + Path.DirectorySeparatorChar + recordingFileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');
                if (csv[0] != userName)
                {
                    recordingsToNotDelete.Add(line);
                }
                replayManager.Add(line);
            }

            replayManager = recordingsToNotDelete;
            SaveRecordings();
            sr.Close();
        }
    }
}
public class PlayerAccount
{
    public string userName, password;
    public int totalNumberOfWins;
    public PlayerAccount(string n, string p, int totalWins)
    {
        userName = n;
        password = p;
        totalNumberOfWins = totalWins;
    }
    static public bool operator==(PlayerAccount p1, PlayerAccount p2)
    {
        return (p1.userName == p2.userName && p1.password == p2.password);
    }
    static public bool operator !=(PlayerAccount p1, PlayerAccount p2)
    {
        return (p1.userName != p2.userName || p1.password != p2.password);
    }
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
    public const int RecordingSentToServer = 13;
    public const int RecordingRequestedFromServer = 14;
    public const int RequestNumberOfSavedRecordings = 15;
    public const int ClearRecordingOnServer = 16;

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
    public const int RecordingSentToClient = 14;
    public const int SendNumberOfSavedRecordings = 15;
    public const int ReloadDropDownMenu = 16;
}

public static class LoginResponse
{
    public const int Success = 1;

    public const int FailureNameInUse = 2;

    public const int FailureNameNotFound = 3;

    public const int FailureIncorrectPassword = 4;
}

public class GameSession
{
    public int playerID1, playerID2;
    public int gameRoomID;
    public List<int> observerIDs;
    public bool player1InRoom, player2InRoom;

    public GameSession(int PlayerID1, int PlayerID2, int roomID)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
        gameRoomID = roomID;

        player1InRoom = true;
        player2InRoom = true;

        observerIDs = new List<int>();
    }
}

public class GameSessionManager
{
    public static int nextGameSessionIDNumber = 100;
    public static int GetGameSessionIDNumber()
    {
        nextGameSessionIDNumber++;
        return nextGameSessionIDNumber;
    }
    public List<GameSession> allGameSessions;
    public GameSessionManager() 
    {
        allGameSessions = new List<GameSession>();
    }
}

//public class ReplayRecorder
//{
//    public static int turnNumber = 0;
//    public string name;
//    public int numberOfTurns;
//    public string startingSymbol;
//    public float[] timeBetweenTurnsArray;
//    public int[] cellNumberOfTurn;
//    public float gameID;

//    public ReplayRecorder()
//    {
//        name = "";
//        numberOfTurns = 0;
//        startingSymbol = "";
//        timeBetweenTurnsArray = new float[9];
//        cellNumberOfTurn = new int[9];
//    }
//}