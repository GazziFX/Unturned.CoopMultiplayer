using SDG.NetTransport;
using SDG.Unturned;
using Steamworks;
using System;
using System.Net;

namespace Unturned.CoopMultiplayer
{
	internal struct TransportConnection_CoopSteamNetworking : ITransportConnection, IEquatable<ITransportConnection>
	{
		public TransportConnection_CoopSteamNetworking(CSteamID steamId)
		{
			this.steamId = steamId;
		}

		public bool TryGetIPv4Address(out uint address)
		{
			P2PSessionState_t p2PSessionState_t;
			if (SteamNetworking.GetP2PSessionState(this.steamId, out p2PSessionState_t) && p2PSessionState_t.m_bUsingRelay == 0)
			{
				address = p2PSessionState_t.m_nRemoteIP;
				return true;
			}
			address = 0U;
			return false;
		}

		public bool TryGetPort(out ushort port)
		{
			P2PSessionState_t p2PSessionState_t;
			if (SteamNetworking.GetP2PSessionState(this.steamId, out p2PSessionState_t) && p2PSessionState_t.m_bUsingRelay == 0)
			{
				port = p2PSessionState_t.m_nRemotePort;
				return true;
			}
			port = 0;
			return false;
		}

		public IPAddress GetAddress()
		{
			P2PSessionState_t p2PSessionState_t;
			if (SteamNetworking.GetP2PSessionState(this.steamId, out p2PSessionState_t) && p2PSessionState_t.m_bUsingRelay == 0)
			{
				return new IPAddress((long)((ulong)p2PSessionState_t.m_nRemoteIP));
			}
			return null;
		}

		public string GetAddressString(bool withPort)
		{
			P2PSessionState_t p2PSessionState_t;
			if (SteamNetworking.GetP2PSessionState(this.steamId, out p2PSessionState_t) && p2PSessionState_t.m_bUsingRelay == 0)
			{
				string text = Parser.getIPFromUInt32(p2PSessionState_t.m_nRemoteIP);
				if (withPort)
				{
					text += ":";
					text += p2PSessionState_t.m_nRemotePort;
				}
				return text;
			}
			return null;
		}

		public void CloseConnection()
		{
			SteamNetworking.CloseP2PSessionWithUser(this.steamId);
		}

		public void Send(byte[] buffer, long size, ENetReliability reliability)
		{
			if (Provider.shouldNetIgnoreSteamId(this.steamId))
			{
				return;
			}
			EP2PSend eP2PSendType;
			if (reliability != ENetReliability.Reliable)
			{
				if (reliability != ENetReliability.Unreliable)
				{
				}
				eP2PSendType = EP2PSend.k_EP2PSendUnreliable;
			}
			else
			{
				eP2PSendType = EP2PSend.k_EP2PSendReliableWithBuffering;
			}
			SteamNetworking.SendP2PPacket(this.steamId, buffer, (uint)size, eP2PSendType, 0);
		}

		public override bool Equals(object obj)
		{
			return obj is TransportConnection_CoopSteamNetworking && this.steamId == ((TransportConnection_CoopSteamNetworking)obj).steamId;
		}

		public bool Equals(TransportConnection_CoopSteamNetworking other)
		{
			return this.steamId == other.steamId;
		}

		public bool Equals(ITransportConnection other)
		{
			return other is TransportConnection_CoopSteamNetworking && this.steamId == ((TransportConnection_CoopSteamNetworking)other).steamId;
		}

		public override int GetHashCode()
		{
			return this.steamId.GetHashCode();
		}

		public override string ToString()
		{
			return this.steamId.ToString();
		}

		public static implicit operator CSteamID(TransportConnection_CoopSteamNetworking clientId)
		{
			return clientId.steamId;
		}

		public static bool operator ==(TransportConnection_CoopSteamNetworking lhs, TransportConnection_CoopSteamNetworking rhs)
		{
			return lhs.Equals(rhs);
		}

		public static bool operator !=(TransportConnection_CoopSteamNetworking lhs, TransportConnection_CoopSteamNetworking rhs)
		{
			return !(lhs == rhs);
		}

		public CSteamID steamId;
	}
}
