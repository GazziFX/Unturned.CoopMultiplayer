using System;
using UnityEngine;
using SDG.Unturned;
using SDG.NetTransport;
using Steamworks;
using SDG.NetPak;
using System.Collections.Generic;
using SDG.Provider.Services.Multiplayer;

namespace Unturned.CoopMultiplayer
{
	public class CoopProvider : MonoBehaviour
	{
		public bool Hosting;
		public string Password = string.Empty;

		public void OnEnable()
        {
			Provider.onServerHosted += OnServerHosted;
			Provider.onServerShutdown += OnServerShutdown;
		}

		public void OnDisable()
		{
			Provider.onServerHosted -= OnServerHosted;
			Provider.onServerShutdown -= OnServerShutdown;
		}

		private void OnServerHosted()
		{
			try
			{
				Provider.provider.multiplayerService.serverMultiplayerService.open(Provider.ip, Provider.port, ESecurityMode.LAN);
			}
			catch (Exception ex)
			{
				UnturnedLog.info("Quit due to provider exception: " + ex.Message);
				Provider.disconnect();
				return;
			}

			MasterBundleValidation.eligibleBundleNames = new List<string>();
			MasterBundleValidation.eligibleBundleHashes = new List<MasterBundleValidation.MasterBundleHash>();

			Provider._maxPlayers = 255;
			Provider._serverPassword = Password;
			Provider._serverPasswordHash = Hash.SHA1(Provider.serverPassword);

			Provider.serverTransport = new ServerTransport_CoopSteamNetworking();
			UnturnedLog.info("Initializing {0}", new object[] { Provider.serverTransport.GetType().Name });
			Provider.serverTransport.Initialize(new Action<ITransportConnection>(Provider.OnServerTransportConnectionFailure));
			Provider.backendRealtimeSeconds = SteamGameServerUtils.GetServerRealTime();

			Provider.p2pSessionConnectFail = Callback<P2PSessionConnectFail_t>.CreateGameServer(new Callback<P2PSessionConnectFail_t>.DispatchDelegate(Provider.onP2PSessionConnectFail));
			Provider.validateAuthTicketResponse = Callback<ValidateAuthTicketResponse_t>.CreateGameServer(new Callback<ValidateAuthTicketResponse_t>.DispatchDelegate(Provider.onValidateAuthTicketResponse));
			Provider.clientGroupStatus = Callback<GSClientGroupStatus_t>.CreateGameServer(new Callback<GSClientGroupStatus_t>.DispatchDelegate(Provider.onClientGroupStatus));

			Dedicator.offlineOnly.value = true;

			Hosting = true;
		}

		private void OnServerShutdown()
		{
			Provider.p2pSessionConnectFail.Dispose();
			Provider.validateAuthTicketResponse.Dispose();
			Provider.clientGroupStatus.Dispose();

			Provider.p2pSessionConnectFail = null;
			Provider.validateAuthTicketResponse = null;
			Provider.clientGroupStatus = null;

			Provider.provider.multiplayerService.serverMultiplayerService.close();

			Hosting = false;
        }

		public void Update()
		{
			if (!Hosting)
				return;

			if (!Level.isLoaded)
				return;


			Provider.listenServer();

			if (Time.realtimeSinceStartup - Provider.lastPingRequestTime > Provider.PING_REQUEST_INTERVAL)
			{
				Provider.lastPingRequestTime = Time.realtimeSinceStartup;
				for (int i = 0; i < Provider.clients.Count; i++)
				{
					if (Provider.clients[i].IsLocalPlayer)
						continue;

					if (Time.realtimeSinceStartup - Provider.clients[i].timeLastPingRequestWasSentToClient > 1f || Provider.clients[i].timeLastPingRequestWasSentToClient < 0f)
					{
						Provider.clients[i].timeLastPingRequestWasSentToClient = Time.realtimeSinceStartup;
						NetMessages.SendMessageToClient(EClientMessage.PingRequest, ENetReliability.Unreliable, Provider.clients[i].transportConnection, delegate (NetPakWriter writer)
						{
						});
					}
				}
			}
			if (Time.realtimeSinceStartup - Provider.lastQueueNotificationTime > 6f)
			{
				Provider.lastQueueNotificationTime = Time.realtimeSinceStartup;
				int index2;
				int index;
				for (index = 0; index < Provider.pending.Count; index = index2)
				{
					if (Provider.pending[index].lastNotifiedQueuePosition != index)
					{
						Provider.pending[index].lastNotifiedQueuePosition = index;
						NetMessages.SendMessageToClient(EClientMessage.QueuePositionChanged, ENetReliability.Reliable, Provider.pending[index].transportConnection, delegate (NetPakWriter writer)
						{
							writer.WriteUInt8((byte)Mathf.Clamp(index, 0, 255));
						});
					}
					index2 = index + 1;
				}
			}
			for (int j = Provider.clients.Count - 1; j >= 0; j--)
			{
				if (!Provider.clients[j].IsLocalPlayer)
				{
					if (Time.realtimeSinceStartup - Provider.clients[j].timeLastPacketWasReceivedFromClient > Provider.configData.Server.Timeout_Game_Seconds)
					{
						if (CommandWindow.shouldLogJoinLeave)
						{
							SteamPlayerID playerID = Provider.clients[j].playerID;
							CommandWindow.Log(Provider.localization.format("Dismiss_Timeout", new object[] { playerID.steamID, playerID.playerName, playerID.characterName }));
						}
						Provider.dismiss(Provider.clients[j].playerID.steamID);
						break;
					}
					if (Time.realtimeSinceStartup - Provider.clients[j].joined > Provider.configData.Server.Timeout_Game_Seconds)
					{
						int num = Mathf.FloorToInt(Provider.clients[j].ping * 1000f);
						if (num > (int)Provider.configData.Server.Max_Ping_Milliseconds)
						{
							if (CommandWindow.shouldLogJoinLeave)
							{
								SteamPlayerID playerID2 = Provider.clients[j].playerID;
								CommandWindow.Log(Provider.localization.format("Dismiss_Ping", new object[]
								{
									num,
									Provider.configData.Server.Max_Ping_Milliseconds,
									playerID2.steamID,
									playerID2.playerName,
									playerID2.characterName
								}));
							}
							Provider.dismiss(Provider.clients[j].playerID.steamID);
							break;
						}
					}
				}

				Provider.clients[j].rpcCredits -= Time.deltaTime;
				if (Provider.clients[j].rpcCredits < 0f)
				{
					Provider.clients[j].rpcCredits = 0f;
				}
			}
			if (Provider.pending.Count > 0 && Provider.pending[0].hasSentVerifyPacket && Provider.pending[0].realtimeSinceSentVerifyPacket > Provider.configData.Server.Timeout_Queue_Seconds)
			{
				SteamPending steamPending = Provider.pending[0];
				UnturnedLog.info("Front of queue player timed out: {0} Ready: {1} Auth: {2} Econ: {3} Group: {4}", new object[]
				{
					steamPending.playerID.steamID,
					steamPending.canAcceptYet,
					steamPending.hasAuthentication,
					steamPending.hasProof,
					steamPending.hasGroup
				});
				ESteamRejection rejection;
				if (!steamPending.hasAuthentication && steamPending.hasProof && steamPending.hasGroup)
				{
					rejection = ESteamRejection.LATE_PENDING_STEAM_AUTH;
				}
				else if (steamPending.hasAuthentication && !steamPending.hasProof && steamPending.hasGroup)
				{
					rejection = ESteamRejection.LATE_PENDING_STEAM_ECON;
				}
				else if (steamPending.hasAuthentication && steamPending.hasProof && !steamPending.hasGroup)
				{
					rejection = ESteamRejection.LATE_PENDING_STEAM_GROUPS;
				}
				else
				{
					rejection = ESteamRejection.LATE_PENDING;
				}
				Provider.reject(steamPending.playerID.steamID, rejection);
			}
			else if (Provider.pending.Count > 1)
			{
				for (int k = Provider.pending.Count - 1; k > 0; k--)
				{
					if (Time.realtimeSinceStartup - Provider.pending[k].lastReceivedPingRequestRealtime > Provider.configData.Server.Timeout_Queue_Seconds)
					{
						SteamPending steamPending2 = Provider.pending[k];
						UnturnedLog.info("Queued player timed out: {0}", new object[] { steamPending2.playerID.steamID });
						Provider.reject(steamPending2.playerID.steamID, ESteamRejection.LATE_PENDING);
						break;
					}
				}
			}

			Provider.dswUpdateMonitor?.tick(Time.deltaTime);
		}
    }
}
