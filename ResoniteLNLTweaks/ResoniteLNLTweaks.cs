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
        public const string Version = "0.1.0";
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
        public static readonly ModConfigurationKey<int> WINDOWSIZE = new("windowSize", "Window Size: The maximum data size of each LNL packet. Default is 64.", () => 64);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> NATIVESOCKETS = new("nativeSockets", "Native Sockets: Enable Native Sockets (WARNING: Experimental).", () => false);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> DISABLEMOD = new("disableMod", "Disable Mod: Do not override any LNL values.", () => false);

        public static ModConfiguration Config;

        public static Type BaseChannelType = AccessTools.TypeByName("LiteNetLib.BaseChannel");

        public static Type ReliableChannelType = AccessTools.TypeByName("LiteNetLib.ReliableChannel");

        public static ConstructorInfo BaseChannelCI = AccessTools.Constructor(BaseChannelType, new Type[] { typeof(NetPeer) });

        public static ConstructorInfo ReliableChannelCI = AccessTools.Constructor(ReliableChannelType, new Type[] { typeof(NetPeer), typeof(bool), typeof(byte) });

        private static bool _windowSizePatched = false;

        private static Harmony _harmony;

        private static void RepatchLNL()
        {
            bool disablemod = Config.GetValue(DISABLEMOD);
            if (_windowSizePatched) // Patched, let's unpatch
            {
                _harmony.Unpatch(BaseChannelCI, HarmonyPatchType.All);
                _harmony.Unpatch(ReliableChannelCI, HarmonyPatchType.All);
                _windowSizePatched = false;
                Msg($"Unpatched BaseChannel and ReliableChannel.");
            }
            if (!disablemod) // Apply transpiler patch for DefaultWindowSize
            {
                _harmony.Patch(BaseChannelCI, transpiler: new HarmonyMethod(typeof(ResoniteLNLTweaks), nameof(BaseChannelTranspiler)));
                _harmony.Patch(ReliableChannelCI, transpiler: new HarmonyMethod(typeof(ResoniteLNLTweaks), nameof(ReliableChannelTranspiler)));
                _windowSizePatched = true;
            }
        }

        public override void OnEngineInit()
        {
            try
            {
                //Harmony.DEBUG = true;
                Config = GetConfiguration();
                bool disablemod = Config.GetValue(DISABLEMOD);

                _harmony = new Harmony(BuildInfo.GUID);

                RepatchLNL();
                _harmony.PatchAll();

                Config.OnThisConfigurationChanged += OnConfigChange; //Subscribe to when any key in this mod has changed
            }
            catch (Exception ex)
            {
                Error(ex);
            }
        }

        static void OnConfigChange(ConfigurationChangedEvent configurationChangedEvent)
        {
            switch (configurationChangedEvent.Key.Name)
            {
                case "disableMod":
                    RepatchLNL();
                    break;
                case "windowSize":
                    if (Config.GetValue(WINDOWSIZE) % 64 == 0) // Don't apply unless the value is a factor of 64
                    {
                        RepatchLNL();
                    }
                    break;
            }
        }

        static IEnumerable<CodeInstruction> ReliableChannelTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            int windowSize = Config.GetValue(WINDOWSIZE);
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                CodeInstruction instr = codes[i];

                if (instr.opcode == OpCodes.Ldc_I4_S) // Update first Ldc_I4_S
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4, windowSize);
                    Msg($"Patched ReliableChannel: DefaultWindowSize: {windowSize}");
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

                if (instr.opcode == OpCodes.Ldc_I4_S) // Update first Ldc_I4_S
                {
                    codes[i] = new CodeInstruction(OpCodes.Ldc_I4, windowSize);
                    Msg($"Patched BaseChannel: DefaultWindowSize: {windowSize}");
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