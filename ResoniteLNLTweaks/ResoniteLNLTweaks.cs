using HarmonyLib;
using ResoniteModLoader;
using FrooxEngine;
using Elements.Core;
using SkyFrost.Base;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LiteNetLib;
using System.Reflection;
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
        public static readonly ModConfigurationKey<int> WINDOWSIZE = new("windowSize", "Window Size: The maximum data size of each LNL packet. Default is 64.", () => 64);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> NATIVESOCKETS = new("nativeSockets", "Native Sockets: Enable Native Sockets (WARNING: Experimental).", () => false);

        [AutoRegisterConfigKey]
        public static readonly ModConfigurationKey<bool> DISABLEMOD = new("disableMod", "Disable Mod: Do not override any LNL values.", () => false);

        public static ModConfiguration Config;

        public static Type BaseChannelType = AccessTools.TypeByName("LiteNetLib.BaseChannel");

        //public static ConstructorInfo BaseChannelCI = AccessTools.Constructor(BaseChannelType, new Type[] { typeof(NetPeer) });

        public override void OnEngineInit()
        {
            try
            {
                Harmony.DEBUG = true;
                Config = GetConfiguration();

                var constructorInfo = AccessTools.Constructor(BaseChannelType, new Type[] { typeof(NetPeer) });

                Harmony harmony = new Harmony(BuildInfo.GUID);

                var ctorMethodInfo = AccessTools.Method(typeof(BaseChannelPatch), "BaseChannelConstructor", new Type[] { typeof(Object), typeof(NetPeer) });


                harmony.Patch(constructorInfo, postfix: new HarmonyMethod(ctorMethodInfo));
                harmony.PatchAll();

            }
            catch (Exception ex)
            {
                Error(ex);
            }
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


        [HarmonyPatch("BaseChannel")]
        public static class BaseChannelPatch
        {

/*            [HarmonyPostfix]
            [HarmonyPatch(MethodType.Constructor)]
            public static void BaseChannelConstructor(Object __instance, NetPeer peer)
            {
                Msg($"THIS IS THE CONSTRUCTOR 1!");
            }
*/
            public static void BaseChannelConstructor(Object __instance, NetPeer peer)
            {
                Msg($"THIS IS THE CONSTRUCTOR 2!");
            }
        }

    }
}