﻿using CommonZones.Zones;
using Rocket.API;
using Rocket.Unturned.Player;
using SDG.NetTransport;
using SDG.Unturned;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CommonZones
{
    public class ZoneCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Player;
        public string Name => "zone";
        public string Help => "Zone utility commands.";
        public string Syntax => "/zone <visualize|go|list>";
        public List<string> Aliases => new List<string>(0);
        public List<string> Permissions => new List<string>(1) { "cz.zone" };
        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (caller is not UnturnedPlayer player) return;

            if (command.Length == 0)
            {
                player.SendChat("zone_syntax");
                return;
            }
            string operation = command[0];
            string perm = "cz.zone." + operation;
            if (!player.HasPermission(perm))
            {
                player.SendChat("missing_permission", perm);
            }
            if (operation.Equals("visualize", StringComparison.OrdinalIgnoreCase))
            {
                Visualize(command, player);
            }
            else if (operation.Equals("go", StringComparison.OrdinalIgnoreCase) || operation.Equals("goto", StringComparison.OrdinalIgnoreCase))
            {
                Go(command, player);
            }
            else if (operation.Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                List(command, player);
            }
            else if (operation.Equals("edit", StringComparison.OrdinalIgnoreCase))
            {
                if (player.Player.TryGetComponent(out ZonePlayerComponent comp))
                    comp.EditCommand(command);
                else
                    player.SendChat("zone_syntax");
            }
            else if (operation.Equals("create", StringComparison.OrdinalIgnoreCase))
            {
                if (player.Player.TryGetComponent(out ZonePlayerComponent comp))
                    comp.CreateCommand(command);
                else
                    player.SendChat("zone_syntax");
            }
            else if (operation.Equals("util", StringComparison.OrdinalIgnoreCase))
            {
                if (player.Player.TryGetComponent(out ZonePlayerComponent comp))
                    comp.UtilCommand(command);
                else
                    player.SendChat("zone_syntax");
            }
            else
            {
                player.SendChat("zone_syntax");
                return;
            }
        }
        private void Visualize(string[] command, UnturnedPlayer player)
        {
            Zone? zone;
            if (command.Length == 1)
            {
                Vector3 plpos = player.Player.GetPosition();
                if (player.Player == null) return; // player got kicked
                zone = GetZone(plpos);
            }
            else
            {
                string name = string.Join(" ", command, 1, command.Length - 1);
                zone = GetZone(name);
            }
            if (zone == null)
            {
                player.SendChat("zone_visualize_no_results");
                return;
            }
            Vector2[] points = zone.GetParticleSpawnPoints(out Vector2[] corners, out Vector2 center);
            CSteamID channel = player.Player.channel.owner.playerID.steamID;
            bool hasui = ZonePlayerComponent._airdrop != null;
            foreach (Vector2 point in points)
            {   // Border
                Vector3 pos = new Vector3(point.x, 0f, point.y);
                pos.y = Util.GetHeight(pos, zone.MinHeight);
                Util.TriggerEffectReliable(ZonePlayerComponent._side.id, channel, pos);
                if (hasui)
                    Util.TriggerEffectReliable(ZonePlayerComponent._airdrop!.id, channel, pos);
            }
            foreach (Vector2 point in corners)
            {   // Corners
                Vector3 pos = new Vector3(point.x, 0f, point.y);
                pos.y = Util.GetHeight(pos, zone.MinHeight);
                Util.TriggerEffectReliable(ZonePlayerComponent._corner.id, channel, pos);
                if (hasui)
                    Util.TriggerEffectReliable(ZonePlayerComponent._airdrop!.id, channel, pos);
            }
            {   // Center
                Vector3 pos = new Vector3(center.x, 0f, center.y);
                pos.y = Util.GetHeight(pos, zone.MinHeight);
                Util.TriggerEffectReliable(ZonePlayerComponent._center.id, channel, pos);
                if (hasui)
                    Util.TriggerEffectReliable(ZonePlayerComponent._airdrop!.id, channel, pos);
            }
            player.Player.StartCoroutine(ClearPoints(player.Player));
            player.SendChat("zone_visualize_success", (points.Length + corners.Length + 1).ToString(CommonZones.Locale), zone.Name);
        }
        private IEnumerator<WaitForSeconds> ClearPoints(Player player)
        {
            yield return new WaitForSeconds(60f);
            if (player == null) yield break;
            ITransportConnection channel = player.channel.owner.transportConnection;
            if (ZonePlayerComponent._airdrop != null)
                EffectManager.askEffectClearByID(ZonePlayerComponent._airdrop.id, channel);
            EffectManager.askEffectClearByID(ZonePlayerComponent._side.id, channel);
            EffectManager.askEffectClearByID(ZonePlayerComponent._corner.id, channel);
            EffectManager.askEffectClearByID(ZonePlayerComponent._center.id, channel);
        }
        private void List(string[] command, UnturnedPlayer player)
        {
            for (int i = 0; i < CommonZones.ZoneProvider.Zones.Count; i++)
            {
                L.Log(CommonZones.ZoneProvider.Zones[i].ToString(), ConsoleColor.DarkGray);
            }
        }
        private void Go(string[] command, UnturnedPlayer player)
        {
            Zone? zone;
            if (command.Length == 1)
            {
                Vector3 plpos = player.Player.GetPosition();
                if (player.Player == null) return; // player got kicked
                zone = GetZone(plpos);
            }
            else
            {
                string name = string.Join(" ", command, 1, command.Length - 1);
                zone = GetZone(name);
            }
            if (zone == null)
            {
                player.SendChat("zone_go_no_results");
                return;
            }
            if (Physics.Raycast(new Ray(new Vector3(zone.Center.x, Level.HEIGHT, zone.Center.y), Vector3.down), out RaycastHit hit, Level.HEIGHT, RayMasks.BLOCK_COLLISION))
            {
                player.Player.teleportToLocationUnsafe(hit.point, 0);
            }
        }
        internal static Zone? GetZone(string nameInput)
        {
            for (int i = 0; i < CommonZones.ZoneProvider.Zones.Count; i++)
            {                
                if (CommonZones.ZoneProvider.Zones[i].Name.Equals(nameInput, StringComparison.OrdinalIgnoreCase))
                    return CommonZones.ZoneProvider.Zones[i];
            }
            for (int i = 0; i < CommonZones.ZoneProvider.Zones.Count; i++)
            {                
                if (CommonZones.ZoneProvider.Zones[i].Name.IndexOf(nameInput, StringComparison.OrdinalIgnoreCase) != -1)
                    return CommonZones.ZoneProvider.Zones[i];
            }
            return null;
        }
        internal static Zone? GetZone(Vector3 position)
        {
            for (int i = 0; i < CommonZones.ZoneProvider.Zones.Count; i++)
            {                
                if (CommonZones.ZoneProvider.Zones[i].IsInside(position))
                    return CommonZones.ZoneProvider.Zones[i];
            }
            Vector2 pos2 = new Vector2(position.x, position.z);
            for (int i = 0; i < CommonZones.ZoneProvider.Zones.Count; i++)
            {
                if (CommonZones.ZoneProvider.Zones[i].IsInside(pos2))
                    return CommonZones.ZoneProvider.Zones[i];
            }
            return null;
        }
    }
}