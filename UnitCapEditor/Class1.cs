using UnityEngine;
using UnityModManagerNet;
using HarmonyLib;
using System;
using System.Reflection;

namespace UnitCapEditor {
    static class Main {
        public static int ID_NUM = 43;
        public static string fleetStr = "50";
        public static int fleetCap = 50;
        public static string robotStr = "40";
        public static int robotCap = 40;
        public static bool enabled;
        public static UnityModManager.ModEntry mod;

        static bool Load(UnityModManager.ModEntry modEntry) {
            var harmony = new Harmony(modEntry.Info.Id);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            mod = modEntry;
            modEntry.OnToggle = OnToggle;
            modEntry.OnGUI = OnGUI;
            return true;
        }

        static bool OnToggle(UnityModManager.ModEntry modEntry, bool value) {
            enabled = value;
            // Turning the mod on or off resets to normal unit caps
            UpdateCaps(50, 40, true);
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry) {
            GUILayout.Label("Fleet cap:");
            fleetStr = GUILayout.TextField(fleetStr, GUILayout.Width(100f));
            GUILayout.Label("Robot cap:");
            robotStr = GUILayout.TextField(robotStr, GUILayout.Width(100f));
            if (GUILayout.Button("Apply") && int.TryParse(fleetStr, out var fleet) && int.TryParse(robotStr, out var robot)) {
                UpdateCaps(fleet, robot, true);
            }
        }

        public static void UpdateCaps(int newFleetCap, int newRobotCap, bool sending) {
            // Don't try to update caps if a game hasn't started
            if (TeamInfo.sTeams.Count == 0) {
                return;
            }

            if (newFleetCap < 0) {
                fleetCap = 0;
            } else {
                fleetCap = newFleetCap;
            }

            if (newRobotCap < 0) {
                robotCap = 0;
            } else {
                robotCap = newRobotCap;
            }

            foreach (TeamInfo.TeamInfomation info in TeamInfo.sTeams) {
                info.Resources.FleetCap = fleetCap;
                info.Resources.BotCap = robotCap;
            }

            // Send new unit caps to other players in multiplayer (only works for other players that have the mod)
            if (sending && !PhotonNetwork.offlineMode) {
                Corescript.CoreObject.GetComponent<PhotonView>().RPC("ReciveText", PhotonTargets.Others, new object[] {
                    fleetCap.ToString() + "," + robotCap.ToString(),
                    ID_NUM
                });
            }
        }
    }
    [HarmonyPatch(typeof(IngameChat))]
    [HarmonyPatch("ReciveText")]
    static class CapChangerReceive_Patch {
        static bool Prefix(IngameChat __instance, string SentText, int Team) {
            if (!Main.enabled)
                return true;

            try {
                if (Team == Main.ID_NUM) { // Use the Team number to identify if this mod is being sent data
                    String[] caps = SentText.Split(',');
                    if (int.TryParse(caps[0], out var fleet) && int.TryParse(caps[1], out var robot)) {
                        Main.UpdateCaps(fleet, robot, false); // We are receiving data, so don't send it out again
                    }
                    return false; // Don't let the original "ReciveText" run
                }
            } catch (Exception e) {
                Main.mod.Logger.Error(e.ToString());
            }
            return true;
        }
    }
}