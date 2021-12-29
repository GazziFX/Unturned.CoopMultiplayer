using System;
using UnityEngine;
using Steamworks;
using SDG.Unturned;
using SDG.Framework.Modules;
using SDG.Framework.Debug;

namespace Unturned.CoopMultiplayer
{
    public class CoopModule : IModuleNexus
    {
        public static CoopProvider CoProvider;

        public void initialize()
        {
            var go = new GameObject(nameof(CoopProvider));
            CoProvider = go.AddComponent<CoopProvider>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            Patches.Patch();
        }

        public void shutdown()
        {
            if (CoProvider)
                UnityEngine.Object.Destroy(CoProvider.gameObject);

            Patches.Unpatch();
        }

        [TerminalCommandMethod("coop.connect", "Connect to P2P game")]
        public static void ConnectToCoop(
            [TerminalCommandParameter("SteamID", "Hoster ID")] ulong steamID,
            [TerminalCommandParameter("Map", "Hoster Map")] string map,
            [TerminalCommandParameter("Password", "")] string password = ""
            )
        {
            var info = new SteamServerInfo("Coop", EGameMode.NORMAL, false, false, false)
            {
                _steamID = new CSteamID(steamID),
                _map = map,
                _isPvP = true,
                _hasCheats = false,
                _cameraMode = ECameraMode.BOTH,
                _maxPlayers = 255,
                _isPassworded = string.IsNullOrEmpty(password),
                networkTransport = "def"
            };

            Provider.connect(info, password, new System.Collections.Generic.List<PublishedFileId_t>());
        }
    }
}
