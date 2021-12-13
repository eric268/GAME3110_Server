using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public enum RecordingSearchCriteria
{
    INCLUDE_ONLY_USER,
    EXCLUDE_USER,
    INCLUDE_ALL,
}

public class GameLogic : MonoBehaviour
{
    const string fileName = "AccountInfoSaveFile.txt";
    const string recordingFileName = "RecordingInfoSaveFile.txt";
    List<int> connectedClientIDs;
    public LinkedList<GameSession> gameSessions;
    public List<string> replayManager;
    public GameSessionManager gameSessionManager;
    public int playerWaitingForMatch = -1;
    public const int PlayerAccountIdentifyer = 1;
    public ReplayRecorder currentReplay;
    public static LinkedList<PlayerAccount> accountInfo;
    // Start is called before the first frame update
    void Start()
    {
        NetworkedServerProcessing.SetGameLogic(this);
        accountInfo = new LinkedList<PlayerAccount>();
        gameSessionManager = new GameSessionManager();
        replayManager = new List<string>();
        currentReplay = new ReplayRecorder();
        connectedClientIDs = new List<int>();
        LoadPlayerAccounts();
        replayManager = LoadRecordingsWithFiltering(RecordingSearchCriteria.INCLUDE_ALL);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void AddConnectedClient(int clientID)
    {
        connectedClientIDs.Add(clientID);
    }
    static public void LoadPlayerAccounts()
    {
        string path = Application.dataPath + Path.DirectorySeparatorChar + fileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while ((line = sr.ReadLine()) != null)
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

    public GameSession FindGameSessionWithPlayerID(int id)
    {
        foreach (GameSession gs in gameSessionManager.allGameSessions)
        {
            if (id == gs.playerID1 || id == gs.playerID2)
                return gs;
        }
        return null;
    }

    public GameSession FindGameSessionWithObserverID(int id)
    {
        foreach (GameSession gs in gameSessionManager.allGameSessions)
        {
            foreach (int observerID in gs.observerIDs)
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

    public List<string> LoadRecordingsWithFiltering(RecordingSearchCriteria searchCriteria, string userNameToFilter = "")
    {
        List<string> recordings = new List<string>();
        string path = Application.dataPath + Path.DirectorySeparatorChar + recordingFileName;
        if (File.Exists(path))
        {
            StreamReader sr = new StreamReader(path);
            string line = "";
            while ((line = sr.ReadLine()) != null)
            {
                string[] csv = line.Split(',');

                if (searchCriteria == RecordingSearchCriteria.INCLUDE_ALL)
                {
                    recordings.Add(line);
                }
                else if (searchCriteria == RecordingSearchCriteria.INCLUDE_ONLY_USER)
                {
                    if (csv[0] == userNameToFilter)
                        recordings.Add(line);
                }
                else if (searchCriteria == RecordingSearchCriteria.EXCLUDE_USER)
                {
                    if (csv[0] != userNameToFilter)
                        recordings.Add(line);
                }
            }
            sr.Close();
        }
        return recordings;
    }

    public void DeleteRecordingsBelongingToUser(string userName)
    {
        List<string> recordingsToNotDelete = LoadRecordingsWithFiltering(RecordingSearchCriteria.EXCLUDE_USER, userName);
        replayManager.Clear();
        replayManager = recordingsToNotDelete;
        SaveRecordings();
    }

    public void PlayerDisconnectedFromGameSession(int id)
    {
        GameSession gs = FindGameSessionWithPlayerID(id);
        if (gs != null)
        {
            if (id == gs.playerID1)
                gs.player1InRoom = false;
            else
                gs.player2InRoom = false;

            string sender = "System";
            string message = "Player disconnected from room";

            NetworkedServerProcessing.SendMessageToClient(string.Join(",", NetworkedServerProcessing.ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), gs.playerID1);
            NetworkedServerProcessing.SendMessageToClient(string.Join(",", NetworkedServerProcessing.ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), gs.playerID2);

            foreach (int observerNum in gs.observerIDs)
            {
                if (observerNum != id)
                    NetworkedServerProcessing.SendMessageToClient(string.Join(",", NetworkedServerProcessing.ServertoClientSignifiers.SendPlayerChatToOpponent.ToString(), sender, message), observerNum);
            }

            if (!gs.player1InRoom && !gs.player2InRoom)
                gameSessionManager.allGameSessions.Remove(gs);
        }
    }

    public string SerializeReplayRecorder(ReplayRecorder recording)
    {
        string recordingPacket = string.Join(",", recording.username, recording.startingSymbol, recording.numberOfTurns);

        foreach (float time in recording.timeBetweenTurnsArray)
        {
            recordingPacket += "," + time;
        }

        foreach (int turnNumber in recording.cellNumberOfTurn)
        {
            recordingPacket += "," + turnNumber;
        }

        return recordingPacket;
    }

    public ReplayRecorder DeserializeReplayRecording(string recording)
    {
        ReplayRecorder replayRecording = new ReplayRecorder();
        string[] csv = recording.Split(',');

        int index = 0;
        replayRecording.username = csv[index++];
        replayRecording.startingSymbol = csv[index++];
        replayRecording.numberOfTurns = int.Parse(csv[index++]);

        for (int i = 0; i < replayRecording.numberOfTurns; i++)
        {
            replayRecording.timeBetweenTurnsArray.Add(float.Parse(csv[index++]));
        }

        for (int i = 0; i < replayRecording.numberOfTurns; i++)
        {
            replayRecording.cellNumberOfTurn.Add(int.Parse(csv[index++]));
        }
        return replayRecording;
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
    static public bool operator ==(PlayerAccount p1, PlayerAccount p2)
    {
        return (p1.userName == p2.userName && p1.password == p2.password);
    }
    static public bool operator !=(PlayerAccount p1, PlayerAccount p2)
    {
        return (p1.userName != p2.userName || p1.password != p2.password);
    }
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
    public static int nextGameSessionIDNumber = 0;
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

public class ReplayRecorder
{
    public string username;
    public static int turnNumber = 0;
    public int numberOfTurns;
    public string startingSymbol;
    public List<float> timeBetweenTurnsArray;
    public List<int> cellNumberOfTurn;


    public ReplayRecorder()
    {
        numberOfTurns = 0;
        startingSymbol = "";
        timeBetweenTurnsArray = new List<float>();
        cellNumberOfTurn = new List<int>();
        username = "";
    }
    public string SerializeReplayTimes()
    {
        string timeSerialized = "";
        foreach (float time in timeBetweenTurnsArray)
        {
            timeSerialized += "," + time;
        }
        return timeSerialized;
    }

    public string SerializeReplayMoveIndex()
    {
        string moveIndexSerialized = "";
        foreach (int index in cellNumberOfTurn)
        {
            moveIndexSerialized += "," + index;
        }
        return moveIndexSerialized;
    }
}
