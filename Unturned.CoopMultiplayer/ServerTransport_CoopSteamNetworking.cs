using SDG.NetTransport;
using SDG.NetTransport.SteamNetworking;
using SDG.Unturned;
using Steamworks;
using System;

namespace Unturned.CoopMultiplayer
{
	public class ServerTransport_CoopSteamNetworking : TransportBase_SteamNetworking, IServerTransport
	{
		public void Initialize(Action<ITransportConnection> connectionClosedCallback)
		{
			this.p2pSessionRequest = Callback<P2PSessionRequest_t>.Create(new Callback<P2PSessionRequest_t>.DispatchDelegate(this.OnP2PSessionRequest));
		}

		public void TearDown()
		{
			this.p2pSessionRequest.Dispose();
		}

		public bool Receive(byte[] buffer, out long size, out ITransportConnection transportConnection)
		{
			transportConnection = null;
			size = 0L;
			int nChannel = 0;
			uint num;
			CSteamID steamId;
			if (!SteamNetworking.ReadP2PPacket(buffer, (uint)buffer.Length, out num, out steamId, nChannel))
			{
				return false;
			}
			if (num > (uint)buffer.Length)
			{
				num = (uint)buffer.Length;
			}
			size = num;
			transportConnection = new TransportConnection_CoopSteamNetworking(steamId);
			return true;
		}

		private void OnP2PSessionRequest(P2PSessionRequest_t callback)
		{
			CSteamID steamIDRemote = callback.m_steamIDRemote;
			if (Provider.shouldNetIgnoreSteamId(steamIDRemote))
			{
				return;
			}
			if (!steamIDRemote.BIndividualAccount())
			{
				return;
			}
			SteamNetworking.AcceptP2PSessionWithUser(steamIDRemote);
		}

		private Callback<P2PSessionRequest_t> p2pSessionRequest;
	}
}
