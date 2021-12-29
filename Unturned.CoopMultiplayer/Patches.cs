using HarmonyLib;
using SDG.Unturned;
using Steamworks;
using System.Reflection.Emit;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SDG.NetTransport;
using SDG.NetPak;
using SDG.NetTransport.Loopback;

namespace Unturned.CoopMultiplayer
{
    public static class Patches
    {
        public static Harmony HarmonyInstance = new Harmony(nameof(CoopModule));

        static Patches()
        {
            Dump(Resources.Load<GameObject>("Characters/Player_Client"));

            UnturnedLog.info("\n---\n");

            Dump(Resources.Load<GameObject>("Characters/Player_Dedicated"));

            PlayerClient = Resources.Load<GameObject>("Characters/Player_Client");
            PlayerClient.AddComponent<CharacterController>();
        }

        public static void Dump(GameObject go)
        {
            foreach (var comp in go.GetComponents<Component>())
                UnturnedLog.info(comp.GetType().Name);
        }

        public static void Patch()
        {
            //PatchGameServerMethod(nameof(GetPublicIP));
            //PatchGameServerMethod(nameof(BUpdateUserData));
            //PatchGameServerMethod(nameof(BeginAuthSession));
            //PatchGameServerMethod(nameof(EndAuthSession));

            HarmonyInstance.Patch(typeof(GameMode).GetMethod(nameof(getPlayerGameObject)),
                prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(getPlayerGameObject))));

            HarmonyInstance.Patch(typeof(StructureManager).GetMethod("onRegionUpdated", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(onRegionUpdatedStructure))));

            HarmonyInstance.Patch(typeof(BarricadeManager).GetMethod("onRegionUpdated", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(onRegionUpdatedBarricade))));

            HarmonyInstance.Patch(typeof(ResourceManager).GetMethod("onRegionUpdated", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(onRegionUpdatedResource))));

            HarmonyInstance.Patch(typeof(ObjectManager).GetMethod("onRegionUpdated", BindingFlags.Instance | BindingFlags.NonPublic),
                postfix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(onRegionUpdatedObject))));

            HarmonyInstance.Patch(typeof(ItemManager).GetMethod("onRegionUpdated", BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(Patches).GetMethod(nameof(onRegionUpdatedItem))));

            PatchClientMethodHandle("SendAndLoopbackIfLocal");
            PatchClientMethodHandle("SendAndLoopbackIfAnyAreLocal");
            PatchClientMethodHandle("SendAndLoopback");

            /*
            HarmonyInstance.Patch(
                typeof(PlayerMovement).GetMethod("InitializePlayer", BindingFlags.Instance | BindingFlags.NonPublic),
                transpiler: new HarmonyMethod(typeof(Patches).GetMethod(nameof(PlayerMovementTranspiler)))
                );
            */
        }

        public static void Unpatch()
        {
            HarmonyInstance.UnpatchAll();
        }

        private static void PatchClientMethodHandle(string methodName)
        {
            HarmonyInstance.Patch(
                typeof(ClientMethodHandle).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic),
                prefix: new HarmonyMethod(typeof(Patches).GetMethod(methodName))
                );
        }

        /*

        private static void PatchGameServerMethod(string methodName)
        {
            HarmonyInstance.Patch(
                typeof(SteamGameServer).GetMethod(methodName),
                prefix: new HarmonyMethod(typeof(Patches).GetMethod(methodName))
                );
        }

        public static bool GetPublicIP(ref SteamIPAddress_t __result)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            __result = default;
            return false;
        }

        public static bool BUpdateUserData(ref bool __result)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            __result = false;
            return false;
        }

        public static bool BeginAuthSession(byte[] pAuthTicket, int cbAuthTicket, CSteamID steamID, ref EBeginAuthSessionResult __result)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            __result = SteamUser.BeginAuthSession(pAuthTicket, cbAuthTicket, steamID);
            return false;
        }

        public static bool EndAuthSession(CSteamID steamID)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            SteamUser.EndAuthSession(steamID);
            return false;
        }

        */

        private static readonly GameObject PlayerClient;

        public static bool getPlayerGameObject(SteamPlayerID playerID, ref GameObject __result)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            if (playerID.steamID == Provider.client)
                __result = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Characters/Player_Server"));
            else
                __result = UnityEngine.Object.Instantiate(PlayerClient);

            return false;
        }

        public static bool SendAndLoopbackIfLocal(ClientMethodHandle __instance, ENetReliability reliability, ITransportConnection transportConnection, NetPakWriter writer)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            writer.Flush();
            if (transportConnection is TransportConnection_Loopback)
                __instance.InvokeLoopback(writer);
            else
                transportConnection.Send(writer.buffer, writer.writeByteIndex, reliability);

            return false;
        }

        public static bool SendAndLoopbackIfAnyAreLocal(ClientMethodHandle __instance, ENetReliability reliability, IEnumerable<ITransportConnection> transportConnections, NetPakWriter writer)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            writer.Flush();
            bool flag = false;
            foreach (ITransportConnection transportConnection in transportConnections)
            {
                if (transportConnection is TransportConnection_Loopback)
                {
                    flag = true;
                    continue;
                }
                transportConnection.Send(writer.buffer, writer.writeByteIndex, reliability);
            }
            if (flag)
            {
                __instance.InvokeLoopback(writer);
            }

            return false;
        }

        public static bool SendAndLoopback(ClientMethodHandle __instance, ENetReliability reliability, IEnumerable<ITransportConnection> transportConnections, NetPakWriter writer)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            writer.Flush();
            foreach (ITransportConnection transportConnection in transportConnections)
            {
                if (transportConnection is TransportConnection_Loopback)
                {
                    UnturnedLog.error("Local connection {0} passed to SendAndLoopback {1}", new object[] { transportConnection, __instance });
                    continue;
                }
                transportConnection.Send(writer.buffer, writer.writeByteIndex, reliability);
            }
            __instance.InvokeLoopback(writer);

            return false;
        }

        public static void onRegionUpdatedStructure(StructureManager __instance, Player player, byte new_x, byte new_y, byte step)
        {
            if (!CoopModule.CoProvider.Hosting)
                return;

            var sp = player.channel.owner;

            if (step == 1 && !sp.IsLocalPlayer && Regions.checkSafe(new_x, new_y))
            {
                UnturnedLog.info("SendStructure");

                Vector3 position = player.transform.position;
                for (int i = new_x - StructureManager.STRUCTURE_REGIONS; i <= new_x + StructureManager.STRUCTURE_REGIONS; i++)
                {
                    for (int j = new_y - StructureManager.STRUCTURE_REGIONS; j <= new_y + StructureManager.STRUCTURE_REGIONS; j++)
                    {
                        if (Regions.checkSafe((byte)i, (byte)j) && !player.movement.loadedRegions[i, j].isStructuresLoaded)
                        {
                            player.movement.loadedRegions[i, j].isStructuresLoaded = true;
                            float sortOrder = Regions.HorizontalDistanceFromCenterSquared(i, j, position);
                            __instance.askStructures(player.channel.owner.transportConnection, (byte)i, (byte)j, sortOrder);
                        }
                    }
                }
            }
        }

        public static void onRegionUpdatedBarricade(BarricadeManager __instance, Player player, byte new_x, byte new_y, byte step)
        {
            if (!CoopModule.CoProvider.Hosting)
                return;

            var sp = player.channel.owner;

            if (step == 2 && !sp.IsLocalPlayer && Regions.checkSafe(new_x, new_y))
            {
                UnturnedLog.info("SendBarricade");

                Vector3 position = player.transform.position;
                for (int i = new_x - BarricadeManager.BARRICADE_REGIONS; i <= new_x + BarricadeManager.BARRICADE_REGIONS; i++)
                {
                    for (int j = new_y - BarricadeManager.BARRICADE_REGIONS; j <= new_y + BarricadeManager.BARRICADE_REGIONS; j++)
                    {
                        if (Regions.checkSafe((byte)i, (byte)j) && !player.movement.loadedRegions[i, j].isBarricadesLoaded)
                        {
                            player.movement.loadedRegions[i, j].isBarricadesLoaded = true;
                            float sortOrder = Regions.HorizontalDistanceFromCenterSquared(i, j, position);
                            __instance.SendRegion(sp, BarricadeManager.regions[i, j], (byte)i, (byte)j, NetId.INVALID, sortOrder);
                        }
                    }
                }
            }
        }

        public static void onRegionUpdatedResource(ResourceManager __instance, Player player, byte new_x, byte new_y, byte step)
        {
            if (!CoopModule.CoProvider.Hosting)
                return;

            var sp = player.channel.owner;

            if (step == 3 && !sp.IsLocalPlayer && Regions.checkSafe(new_x, new_y))
            {
                UnturnedLog.info("SendResource");

                for (int i = new_x - ResourceManager.RESOURCE_REGIONS; i <= new_x + ResourceManager.RESOURCE_REGIONS; i++)
                {
                    for (int j = new_y - ResourceManager.RESOURCE_REGIONS; j <= new_y + ResourceManager.RESOURCE_REGIONS; j++)
                    {
                        if (Regions.checkSafe((byte)i, (byte)j) && !player.movement.loadedRegions[i, j].isResourcesLoaded)
                        {
                            player.movement.loadedRegions[i, j].isResourcesLoaded = true;
                            __instance.SendRegion(player.channel.owner, (byte)i, (byte)j);
                        }
                    }
                }
            }
        }

        public static void onRegionUpdatedObject(ObjectManager __instance, Player player, byte new_x, byte new_y, byte step)
        {
            if (!CoopModule.CoProvider.Hosting)
                return;

            var sp = player.channel.owner;

            if (step == 4 && !sp.IsLocalPlayer && Regions.checkSafe(new_x, new_y))
            {
                UnturnedLog.info("SendObject");

                for (int i = new_x - ObjectManager.OBJECT_REGIONS; i <= new_x + ObjectManager.OBJECT_REGIONS; i++)
                {
                    for (int j = new_y - ObjectManager.OBJECT_REGIONS; j <= new_y + ObjectManager.OBJECT_REGIONS; j++)
                    {
                        if (Regions.checkSafe((byte)i, (byte)j) && !player.movement.loadedRegions[i, j].isObjectsLoaded)
                        {
                            player.movement.loadedRegions[i, j].isObjectsLoaded = true;
                            __instance.askObjects(player.channel.owner.transportConnection, (byte)i, (byte)j);
                        }
                    }
                }
            }
        }

        public static bool onRegionUpdatedItem(ItemManager __instance, Player player, byte new_x, byte new_y, byte step)
        {
            if (!CoopModule.CoProvider.Hosting)
                return true;

            if (step == 0)
            {
                for (byte b = 0; b < Regions.WORLD_SIZE; b += 1)
                {
                    for (byte b2 = 0; b2 < Regions.WORLD_SIZE; b2 += 1)
                    {
                        if (player.channel.isOwner && ItemManager.regions[b, b2].isNetworked && !Regions.checkArea(b, b2, new_x, new_y, ItemManager.ITEM_REGIONS))
                        {
                            if (ItemManager.regions[b, b2].drops.Count > 0)
                            {
                                ItemManager.regions[b, b2].isPendingDestroy = true;
                                ItemManager.regionsPendingDestroy.Add(ItemManager.regions[b, b2]);
                            }
                            ItemManager.CancelInstantiationsInRegion(b, b2);
                            ItemManager.regions[b, b2].isNetworked = false;
                        }
                        if (Provider.isServer && player.movement.loadedRegions[b, b2].isItemsLoaded && !Regions.checkArea(b, b2, new_x, new_y, ItemManager.ITEM_REGIONS))
                        {
                            player.movement.loadedRegions[b, b2].isItemsLoaded = false;
                        }
                    }
                }
            }

            if (step == 5 && Provider.isServer && Regions.checkSafe(new_x, new_y))
            {
                UnturnedLog.info("SendItem");

                Vector3 position = player.transform.position;
                for (int i = new_x - ItemManager.ITEM_REGIONS; i <= new_x + ItemManager.ITEM_REGIONS; i++)
                {
                    for (int j = new_y - ItemManager.ITEM_REGIONS; j <= new_y + ItemManager.ITEM_REGIONS; j++)
                    {
                        if (Regions.checkSafe((byte)i, (byte)j) && !player.movement.loadedRegions[i, j].isItemsLoaded)
                        {
                            /*
                            if (player.channel.isOwner)
                            {
                                __instance.generateItems((byte)i, (byte)j);
                            }
                            */
                            player.movement.loadedRegions[i, j].isItemsLoaded = true;
                            float sortOrder = Regions.HorizontalDistanceFromCenterSquared(i, j, position);

                            __instance.askItems(player.channel.owner.transportConnection, (byte)i, (byte)j, sortOrder);
                        }
                    }
                }
            }

            return false;
        }

        /*
        private static readonly MethodInfo GetComponentCharacterControllerMethod = typeof(Component).GetMethod("GetComponent", new Type[0]).MakeGenericMethod(new Type[] { typeof(CharacterController) });
        private static readonly MethodInfo AddComponentCharacterControllerMethod = typeof(GameObject).GetMethod("AddComponent", new Type[0]).MakeGenericMethod(new Type[] { typeof(CharacterController) });
        private static readonly MethodInfo GetGameObjectMethod = typeof(Component).GetMethod("get_gameObject");

        public static IEnumerable<CodeInstruction> PlayerMovementTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var inst in instructions)
            {
                if (inst.opcode == OpCodes.Call && GetComponentCharacterControllerMethod.Equals(inst.operand))
                {
                    var newinst = new CodeInstruction(inst);
                    newinst.opcode = OpCodes.Call;
                    newinst.operand = GetGameObjectMethod;
                    yield return newinst;

                    yield return new CodeInstruction(OpCodes.Callvirt, AddComponentCharacterControllerMethod);
                }
                yield return inst;
            }
        }
        */
    }
}
