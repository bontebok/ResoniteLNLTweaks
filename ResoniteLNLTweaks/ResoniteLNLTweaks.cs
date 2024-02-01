using HarmonyLib;
using ResoniteModLoader;
using System;
using System.Net;
using LiteNetLib;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: AssemblyTitle(ResoniteLNLTweaks.BuildInfo.Name)]
[assembly: AssemblyProduct(ResoniteLNLTweaks.BuildInfo.GUID)]
[assembly: AssemblyVersion(ResoniteLNLTweaks.BuildInfo.Version)]
[assembly: AssemblyCompany("com.ruciomods")]

namespace ResoniteLNLTweaks
{
    public static class BuildInfo
    {
        public const string Name = "ResoniteLNLTweaks";
        public const string Author = "Rucio";
        public const string Version = "0.0.1";
        public const string Link = "https://github.com/bontebok/ResoniteLNLTweaks";
        public const string GUID = "com.ruciomods.ResoniteLNLTweaks";
    }

    public class ResoniteLNLTweaks : ResoniteMod
    {
        public override string Name => BuildInfo.Name;
        public override string Author => BuildInfo.Author;
        public override string Version => BuildInfo.Version;
        public override string Link => BuildInfo.Link;

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<int> WINDOWSIZE = new("windowSize", "Window Size: The maximum data size of each LNL packet. Default is 64.", () => 128);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> NATIVESOCKETS = new("nativeSockets", "Native Sockets: Enable Native Sockets (WARNING: Experimental).", () => false);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> DISABLEMOD = new("disableMod", "Disable Mod: Do not override any LNL values.", () => false);

        public static ModConfiguration Config;

        public static Type BaseChannelType = AccessTools.TypeByName("LiteNetLib.BaseChannel");

        public static Type ReliableChannelType = AccessTools.TypeByName("LiteNetLib.ReliableChannel");

        public static ConstructorInfo BaseChannelCI = AccessTools.Constructor(BaseChannelType, new Type[] { typeof(NetPeer) });

        public static ConstructorInfo ReliableChannelCI = AccessTools.Constructor(ReliableChannelType, new Type[] { typeof(NetPeer), typeof(bool), typeof(byte) });

        public override void OnEngineInit()
        {
            try
            {
                Harmony.DEBUG = true;
                Config = GetConfiguration();
                bool disablemod = Config.GetValue(DISABLEMOD);

                Harmony harmony = new Harmony(BuildInfo.GUID);

                Msg($"BaseChannel: {BaseChannelCI.Name}");
                Msg($"ReliableChannel: {ReliableChannelCI}");

                if (!disablemod)
                {
                    harmony.Patch(BaseChannelCI, transpiler: new HarmonyMethod(typeof(ResoniteLNLTweaks), nameof(BaseChannelTranspiler)));
                    harmony.Patch(ReliableChannelCI, transpiler: new HarmonyMethod(typeof(ResoniteLNLTweaks), nameof(ReliableChannelTranspiler)));
                }

                harmony.PatchAll();

            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        static IEnumerable<CodeInstruction> ReliableChannelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int windowSize = Config.GetValue(WINDOWSIZE);
            var codes = new List<CodeInstruction>(instructions);
            //int offset = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_I4_S) // Find and update first Ldc_I4_S to Ldc_I4
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4, windowSize);
                    Msg($"Patched ReliableChannel IL.");
                    //offset = i + 1;
                    break;
                }
            }
            return codes;
        }

        static IEnumerable<CodeInstruction> BaseChannelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int windowSize = Config.GetValue(WINDOWSIZE);
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_I4_S) // Find and update first Ldc_I4_S to Ldc_I4
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4, windowSize);
                    Msg($"Patched BaseChannel IL.");
                    break;
                }
            }
            return codes;
        }

        [HarmonyPatch(typeof(NetManager))]
        public class NetManagerPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch("Start", new Type[] { typeof(IPAddress), typeof(IPAddress), typeof(int), typeof(bool) })]
            public static bool Start(ref bool ___UseNativeSockets)
            {
                bool disablemod = Config.GetValue(DISABLEMOD);
                bool nativesockets = Config.GetValue(NATIVESOCKETS);

                if (disablemod)
                    return true;

                if (nativesockets)
                {
                    if (nativesockets != ___UseNativeSockets)
                    {
                        Msg($"Enabling LNL Native Sockets (WARNING: Experimental).");
                        ___UseNativeSockets = true;
                    }
                }
                else
                {
                    if (nativesockets != ___UseNativeSockets)
                    {
                        Msg($"Disabling LNL NativeSockets.");
                        ___UseNativeSockets = false;
                    }
                }
                return true;
            }
        }
    }
}