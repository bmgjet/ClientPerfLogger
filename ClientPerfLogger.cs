// Reference: 0Harmony
/*
 ▄▄▄▄    ███▄ ▄███▓  ▄████  ▄▄▄██▀▀▀▓█████▄▄▄█████▓
▓█████▄ ▓██▒▀█▀ ██▒ ██▒ ▀█▒   ▒██   ▓█   ▀▓  ██▒ ▓▒
▒██▒ ▄██▓██    ▓██░▒██░▄▄▄░   ░██   ▒███  ▒ ▓██░ ▒░
▒██░█▀  ▒██    ▒██ ░▓█  ██▓▓██▄██▓  ▒▓█  ▄░ ▓██▓ ░ 
░▓█  ▀█▓▒██▒   ░██▒░▒▓███▀▒ ▓███▒   ░▒████▒ ▒██▒ ░ 
░▒▓███▀▒░ ▒░   ░  ░ ░▒   ▒  ▒▓▒▒░   ░░ ▒░ ░ ▒ ░░   
▒░▒   ░ ░  ░      ░  ░   ░  ▒ ░▒░    ░ ░  ░   ░    
 ░    ░ ░      ░   ░ ░   ░  ░ ░ ░      ░    ░      
 ░             ░         ░  ░   ░      ░  ░                                                  
 */
using Harmony;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ClientPerfLogger", "bmgjet", "1.0.0")] 
    class ClientPerfLogger : RustPlugin
    {
        Timer _timer;
        Dictionary<ulong, Vector3> Playerpos = new Dictionary<ulong, Vector3>();
        private HarmonyInstance _harmony; //Reference to harmony
        private void Init()
        {
            _harmony = HarmonyInstance.Create(Name + "PATCH");
            Type[] patchType = { AccessTools.Inner(typeof(ClientPerfLogger), "BasePlayer_PerformanceReport") };
            foreach (var t in patchType) { new PatchProcessor(_harmony, t, HarmonyMethod.Merge(t.GetHarmonyMethods())).Patch(); }
        }
        private void OnServerInitialized() { _timer = timer.Every(5, () => { CallClientPerf(); }); }

        private void CallClientPerf()
        {
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                if (basePlayer == null || basePlayer.IsSleeping() || basePlayer.IsDead()) {continue;}
                if (!Playerpos.ContainsKey(basePlayer.userID))
                {
                    Playerpos.Add(basePlayer.userID, basePlayer.transform.position);
                    basePlayer.ClientRPCPlayer<string, int>(null, basePlayer, "GetPerformanceReport", "legacy", UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                    continue;
                }
                if (Vector3.Distance(Playerpos[basePlayer.userID], basePlayer.transform.position) <= 150) { continue; }
                Playerpos[basePlayer.userID] = basePlayer.transform.position;
                basePlayer.ClientRPCPlayer<string, int>(null, basePlayer, "GetPerformanceReport", "legacy", UnityEngine.Random.Range(int.MinValue, int.MaxValue));
            }
        }

        private void Unload()
        {
            _harmony?.UnpatchAll(Name + "PATCH");
            _timer?.Destroy();
        }

        [HarmonyPatch(typeof(BasePlayer), nameof(BasePlayer.PerformanceReport))]
        internal class BasePlayer_PerformanceReport
        {
            [HarmonyPrefix]
            static bool Prefix(BaseEntity.RPCMessage msg, BasePlayer __instance)
            {
                try{
                    string text = msg.read.String(256);
                    string text2 = msg.read.StringRaw(8388608u);
                    ClientPerformanceReport clientPerformanceReport = JsonConvert.DeserializeObject<ClientPerformanceReport>(text2);
                    File.AppendAllText("ClientPerfLogger.csv", string.Concat(new string[] { clientPerformanceReport.memory_system.ToString(), "|", clientPerformanceReport.fps.ToString("0"), "|", clientPerformanceReport.ping.ToString(), "|", __instance.displayName, __instance.transform.position.ToString(),System.Environment.NewLine}));
                }catch { }
                return false;
            }
        }
    }
}