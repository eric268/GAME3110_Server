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

    LinkedList<GameSession> gameSessions;
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
        gameSessions = new LinkedList<GameSession>();
        LoadPlayerAccounts();
        
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
                GameSession gs = new GameSession(playerWaitingForMatch, id);
                gameSessions.AddLast(gs);

                int randomNumberForGameSymbol = Random.Range(0, 2);
                string playerWaitingForMatchSymbol = (randomNumberForGameSymbol == 0) ? "X" : "O";
                string currentPlayersSymbol = (playerWaitingForMatchSymbol == "X") ? "O" : "X";

                

                int playerWaitingForMatchMovesFirst = Random.Range(0, 2);
                int currentPlayersMove = (playerWaitingForMatchMovesFirst == 1) ? 0 : 1;

                SendMessageToClient(string.Join(",",ServertoClientSignifiers.GameSessionStarted.ToString(), playerWaitingForMatchSymbol, playerWaitingForMatchMovesFirst), playerWaitingForMatch);
                SendMessageToClient(string.Join(",", ServertoClientSignifiers.GameSessionStarted, currentPlayersSymbol, currentPlayersMove) + "", id);

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

            if (gs.playerID1 == id)
            {
                SendMessageToClient(string.Join(",",ServertoClientSignifiers.OpponentPlayedAMove.ToString(),csv[1]), gs.playerID2);
            }
            else
            {
                SendMessageToClient(string.Join(",", ServertoClientSignifiers.OpponentPlayedAMove.ToString(), csv[1]), gs.playerID1);
            }
        }
        else if (signifier == ClientToSeverSignifiers.GameOver)
        {
            GameSession gs = FindGameSessionWithPlayerID(id);

            if (gs.playerID1 == id)
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString(), gs.playerID2);
            }
            else
            {
                SendMessageToClient(ServertoClientSignifiers.OpponentWon.ToString(), gs.playerID1);
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
        foreach (GameSession gs in gameSessions)
        {
            if (id == gs.playerID1 || id == gs.playerID2)
                return gs;
        }
        return null;
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
    public GameSession(int PlayerID1, int PlayerID2)
    {
        playerID1 = PlayerID1;
        playerID2 = PlayerID2;
    }
    //Hold two clients
    //to do work list
}