using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace BearlyStanding
{
    /// <summary>
    /// Host / join-by-code flow for Bearly Standing. The host's LAN IPv4 + port is packed into a
    /// short room code (see <see cref="RoomCodeUtil"/>) that friends paste to join. When the lobby
    /// stops filling with real players, the server tops the arena up to <see cref="targetBearCount"/>
    /// with bots and starts the match. Host mode only (no dedicated server).
    /// </summary>
    public class BearlyNetworkManager : NetworkManager
    {
        [Header("Bearly")]
        [Tooltip("Networked bear prefab used for both players and bots. Defaults to playerPrefab.")]
        public GameObject botBearPrefab;
        public int targetBearCount = 6;
        [Tooltip("Seconds of no new player before the server fills empty slots with bots and starts.")]
        public float botFillDelay = 4f;

        [Header("Bear colours (index 0 = first to join / host)")]
        public Material[] bearMaterials;

        public static BearlyNetworkManager Bearly => singleton as BearlyNetworkManager;

        public string RoomCode { get; private set; }
        public bool MatchStarted { get; private set; }

        /// <summary>(players, target) — raised on the server as the lobby fills.</summary>
        public event Action<int, int> OnLobbyChanged;

        private readonly List<NetworkBear> serverBears = new();

        public Material BearMaterial(int index)
        {
            if (bearMaterials == null || bearMaterials.Length == 0) return null;
            return bearMaterials[((index % bearMaterials.Length) + bearMaterials.Length) % bearMaterials.Length];
        }

        // ---------------------------------------------------------------- host / join API

        public void HostGame()
        {
            SetPort(RoomCodeUtil.DefaultPort);
            StartHost();
        }

        /// <summary>Accepts a room code, "ip", or "ip:port". Returns false if it can't be parsed.</summary>
        public bool JoinGame(string codeOrAddress)
        {
            if (!RoomCodeUtil.TryResolve(codeOrAddress, out string ip, out ushort port))
                return false;

            networkAddress = ip;
            SetPort(port);
            StartClient();
            return true;
        }

        public void LeaveGame()
        {
            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkClient.isConnected || NetworkClient.active) StopClient();
            else if (NetworkServer.active) StopServer();
        }

        private void SetPort(ushort port)
        {
            if (transport is PortTransport portTransport) portTransport.Port = port;
        }

        private ushort CurrentPort() => transport is PortTransport pt ? pt.Port : RoomCodeUtil.DefaultPort;

        // ---------------------------------------------------------------- server lifecycle

        public override void OnStartHost()
        {
            base.OnStartHost();
            MatchStarted = false;
            serverBears.Clear();
            RoomCode = RoomCodeUtil.Encode(RoomCodeUtil.LocalIPv4(), CurrentPort());
            Debug.Log($"[Bearly] Hosting — room code: {RoomCode}  ({RoomCodeUtil.LocalIPv4()}:{CurrentPort()})");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            CancelInvoke(nameof(FillWithBots));
            serverBears.Clear();
            MatchStarted = false;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Transform start = GetStartPosition();
            GameObject bear = start != null
                ? Instantiate(playerPrefab, start.position, start.rotation)
                : Instantiate(playerPrefab);

            var nb = bear.GetComponent<NetworkBear>();
            if (nb != null)
            {
                nb.isBot = false;
                nb.colorIndex = serverBears.Count;
            }

            NetworkServer.AddPlayerForConnection(conn, bear);
            if (nb != null) serverBears.Add(nb);

            OnLobbyChanged?.Invoke(CountAlive(), targetBearCount);

            if (!MatchStarted)
            {
                CancelInvoke(nameof(FillWithBots));
                Invoke(nameof(FillWithBots), botFillDelay);
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            serverBears.RemoveAll(b => b == null);
            OnLobbyChanged?.Invoke(CountAlive(), targetBearCount);
        }

        private void FillWithBots()
        {
            if (!NetworkServer.active || MatchStarted) return;
            MatchStarted = true;

            serverBears.RemoveAll(b => b == null);
            GameObject prefab = botBearPrefab != null ? botBearPrefab : playerPrefab;
            NetworkConnectionToClient hostConn = NetworkServer.localConnection;

            int need = Mathf.Max(0, targetBearCount - serverBears.Count);
            for (int i = 0; i < need; i++)
            {
                Transform start = GetStartPosition();
                GameObject bot = start != null
                    ? Instantiate(prefab, start.position, start.rotation)
                    : Instantiate(prefab);

                var nb = bot.GetComponent<NetworkBear>();
                if (nb != null)
                {
                    nb.isBot = true;
                    nb.colorIndex = serverBears.Count;
                }

                NetworkServer.Spawn(bot, hostConn); // host owns bots → host simulates them
                if (nb != null) serverBears.Add(nb);
            }

            OnLobbyChanged?.Invoke(CountAlive(), targetBearCount);

            var match = FindAnyObjectByType<NetworkMatchController>();
            if (match != null) match.ServerBeginMatch();
            else if (MatchManager.Instance != null) MatchManager.Instance.StartMatch();

            Debug.Log($"[Bearly] Match starting with {serverBears.Count} bears ({need} bots).");
        }

        private int CountAlive()
        {
            serverBears.RemoveAll(b => b == null);
            return serverBears.Count;
        }
    }
}
