using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Hardlight;
using Hardlight.Localisation;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // HLCrossPromo (TypeDef 0x02000426, x86-64 0x79BA00..0x79BAB0) and its <Start>d__0 iterator MoveNext
    // (0x79BAC0..0x79BFB0), stepped by hand on an inactive owned host. The device country comes from an owned
    // native bridge standing in for the platform plugin, and the configuration from an owned asset. The host is
    // inactive, so the downloads the system starts are refused as coroutines and no request is ever made.
    public sealed class HLCrossPromoStartTests
    {
        private static readonly Regex RefusedCoroutine = new Regex("Coroutine couldn't be started");
        private static readonly FieldInfo Bridge = typeof(HLUnityCore).GetField("s_unityCoreNativeBridge", BindingFlags.Static | BindingFlags.NonPublic);

        // The device country is the only value Start reads from the bridge.
        private sealed class CountryBridge : IHLUnityCoreNativeBridge
        {
            public readonly string Country;
            public int CountryReads;

            public CountryBridge(string country)
            {
                Country = country;
            }

            public void Initialise(HLUnityCore componentOwner) { }
            public string GetDeviceOS() => "lucid-os";
            public string GetDeviceID() => "lucid-device";
            public string GetDeviceRawLocale() => "lucid-raw-locale";
            public string GetDeviceLanguageCode() => "lucid-language";

            public string GetDeviceISO2CountryCode()
            {
                CountryReads++;
                return Country;
            }

            public int GetClientCode(out uint clientCode)
            {
                clientCode = 0;
                return 0;
            }

            public int GetScreenUnusableHeaderHeightInPixels() => 0;
            public int GetScreenUnusableFooterHeightInPixels() => 0;
            public bool DoesThisDeviceHaveANotch() => false;
            public void LogToDeviceConsole(string message) { }
            public int GetPixelsFromNativeUnitDistance(float distance) => 0;
            public int CalculateApplicationChecksum() => 0;
        }

        // Runs body with the bridge installed and the prior one put back, and with the CrossPromo row acquired
        // empty, so the registration Start makes lands in an owned row.
        private static void Run(string configurationJson, Action<Owned, HLCrossPromo, CountryBridge, VerificationFixtureOwnership.RegistryRow> body) =>
            Owned.Run(owned =>
            {
                owned.Lease(new OwnedCrossPromoConfiguration(configurationJson));
                var row = owned.Lease(new VerificationFixtureOwnership.RegistryRow(ProcessManager.GetDefaultName<CrossPromo>()));
                row.Register(null);
                var bridge = new CountryBridge("ZZ");
                object prior = Bridge.GetValue(null);
                Bridge.SetValue(null, bridge);
                VerificationFixtureOwnership.RunThenCleanUp(
                    () => body(owned, owned.Host("Lucid cross promo", false).AddComponent<HLCrossPromo>(), bridge, row),
                    () => Bridge.SetValue(null, prior));
            });

        // The wait is the nested iterator of MonoSingleton<T>.WaitOnInstance. Reflection reports its declaring type as the
        // open MonoSingleton<T> and carries the closed type argument on the iterator type itself.
        private static void AssertWaitsOnSingleton(object wait, Type singleton, string label)
        {
            Type type = wait.GetType();
            Assert.That(type.DeclaringType, Is.EqualTo(typeof(MonoSingleton<>)), label);
            Assert.That(type.GetGenericArguments(), Is.EqualTo(new[] { singleton }), label);
        }

        // dump.cs TypeDefIndex 1062 and 1061 with the DummyDll rows.
        [Test]
        public void DeclarationsMatchTheOriginalMetadata()
        {
            AssertMembers(typeof(HLCrossPromo), 0x00100001,
                new (string, int)[]
                {
                },
                new (string, int)[]
                {
                    ("Start", 0x0081),
                    (".ctor", 0x1886),
                    ("Hardlight.ICoroutineHandler.StartCoroutine", 0x01e1),
                });
            AssertIl2CppOptions(typeof(HLCrossPromo), 2, 1);
            Assert.That(typeof(HLCrossPromo).BaseType, Is.EqualTo(typeof(MonoBehaviour)));
            Assert.That(typeof(HLCrossPromo).GetInterfaces(), Is.EqualTo(new[] { typeof(ICoroutineHandler) }));
            Type iterator = typeof(HLCrossPromo).GetNestedType("<Start>d__0", BindingFlags.NonPublic);
            Assert.That(iterator, Is.Not.Null);
            AssertMembers(iterator, 0x00100103,
                new (string, int)[]
                {
                    ("<>1__state", 0x0001),
                    ("<>2__current", 0x0001),
                    ("<>4__this", 0x0006),
                    ("<configurationAsset>5__2", 0x0001),
                    ("<country>5__3", 0x0001),
                },
                new (string, int)[]
                {
                    (".ctor", 0x1886),
                    ("System.IDisposable.Dispose", 0x01e1),
                    ("MoveNext", 0x01e1),
                    ("System.Collections.Generic.IEnumerator<System.Object>.get_Current", 0x09e1),
                    ("System.Collections.IEnumerator.Reset", 0x01e1),
                    ("System.Collections.IEnumerator.get_Current", 0x09e1),
                });
            Assert.That(iterator.GetInterfaces(), Is.EquivalentTo(new[] { typeof(System.Collections.Generic.IEnumerator<object>), typeof(IEnumerator), typeof(IDisposable) }));
            Assert.That(typeof(HLCrossPromo).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetCustomAttribute<System.Runtime.CompilerServices.IteratorStateMachineAttribute>().StateMachineType, Is.EqualTo(iterator));
        }

        // The explicit ICoroutineHandler.StartCoroutine 0x79BAA0 tail-calls MonoBehaviour.StartCoroutine, so an
        // inactive host refuses it as the engine does: the error is logged and no coroutine is returned.
        [Test]
        public void TheCoroutineHandlerForwardsToTheHostStartCoroutine()
        {
            Owned.Run(owned =>
            {
                var host = owned.Host("Lucid cross promo", false).AddComponent<HLCrossPromo>();
                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Coroutine started = ((ICoroutineHandler)host).StartCoroutine(new object[0].GetEnumerator());
                Assert.That(started, Is.Null);
            });
        }

        // MoveNext 0x79BAC0, state 0: read the configuration asset, then wait on the HLUnityCore singleton. State 1
        // (after that wait): read the device country, then wait on the Language singleton. State 2: build the
        // configuration from the asset, Application.version, the country, the language name and persistentDataPath,
        // construct the system (0x79bd1b) and register it (0x79bd46); the iterator then ends. Nothing is registered or
        // read from the bridge before its wait has finished. Without local storage (the byte at +0x51 of the
        // configuration, 0x79bc7d) the system works from the cache alone: the cache-only resource manager and the
        // parser (0x79be78, 0x79bf2f), and the metadata request goes through the host's StartCoroutine.
        [Test]
        public void StartWaitsForTheCoreAndTheLanguageThenRegistersACacheOnlySystem()
        {
            Run("{\"m_gameTitle\":\"lucid-title\",\"m_routerPropertyName\":\"lucid-router\",\"m_platformName\":\"lucid-platform\"," +
                "\"m_server\":1,\"m_canUseLocalStorage\":false}", (owned, host, bridge, row) =>
            {
                // With no Language singleton, GetLanguage falls back to EnglishUS (Language.GetLanguage); the
                // expected language below holds only in that state.
                Assert.That(MonoSingleton<Language>.IsNull(), Is.True, "no Language instance is alive");
                var iterator = (IEnumerator)Call(host, "Start");
                Assert.That(iterator.MoveNext(), Is.True);
                AssertWaitsOnSingleton(iterator.Current, typeof(HLUnityCore), "the core wait");
                Assert.That(bridge.CountryReads, Is.EqualTo(0));
                Assert.That(ProcessManager.IsSystemNull<CrossPromo>(), Is.True);

                Assert.That(iterator.MoveNext(), Is.True);
                AssertWaitsOnSingleton(iterator.Current, typeof(Language), "the language wait");
                Assert.That(bridge.CountryReads, Is.EqualTo(1));
                Assert.That(ProcessManager.IsSystemNull<CrossPromo>(), Is.True);

                LogAssert.Expect(LogType.Error, RefusedCoroutine);
                Assert.That(iterator.MoveNext(), Is.False);
                row.AdoptCurrent();
                Assert.That(bridge.CountryReads, Is.EqualTo(1));
                var promo = ProcessManager.GetSystem<CrossPromo>();
                var config = Get<CrossPromoConfig>(promo, "m_config");
                Assert.That(config.MetaDataURL, Is.EqualTo("https://gridserver.hardlightgames.com/info?title=lucid-title&version=" + Application.version + "&country=ZZ"));
                Assert.That(config.LanguageId, Is.EqualTo("EnglishUS"));
                Assert.That(config.PlatformName, Is.EqualTo("lucid-platform"));
                Assert.That(config.RouterPropertyName, Is.EqualTo("lucid-router"));
                Assert.That(config.CanUseLocalStorage, Is.False);
                Assert.That(config.DestinationPath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "grid")));
                Assert.That(config.IncomingPath, Is.EqualTo(Path.Combine(Application.persistentDataPath, "grid_incoming")));
                Assert.That(Get<ICrossPromoResourceManager>(promo, "m_resourceManager"), Is.TypeOf<CrossPromoResourceManagerCacheOnly>());
                Assert.That(Get<ICrossPromoDataParser>(promo, "m_parser"), Is.TypeOf<CrossPromoDataParser>());
            });
        }

        // With local storage the system uses the disk-backed resource manager (0x79bd1b). The configured folders are
        // unique owned ones under the persistent data path, created by that manager's constructor and removed here.
        [Test]
        public void StartRegistersADiskBackedSystemWhenLocalStorageIsAvailable()
        {
            string suffix = Guid.NewGuid().ToString("N");
            string incoming = "lucid-test-incoming-" + suffix, destination = "lucid-test-destination-" + suffix;
            Run("{\"m_incomingFolder\":\"" + incoming + "\",\"m_destinationFolder\":\"" + destination + "\",\"m_canUseLocalStorage\":true}",
                (owned, host, bridge, row) =>
                {
                    VerificationFixtureOwnership.RunThenCleanUp(() =>
                    {
                        var iterator = (IEnumerator)Call(host, "Start");
                        Assert.That(iterator.MoveNext(), Is.True);
                        Assert.That(iterator.MoveNext(), Is.True);
                        LogAssert.Expect(LogType.Error, RefusedCoroutine);
                        Assert.That(iterator.MoveNext(), Is.False);
                        row.AdoptCurrent();
                        var promo = ProcessManager.GetSystem<CrossPromo>();
                        Assert.That(Get<CrossPromoConfig>(promo, "m_config").CanUseLocalStorage, Is.True);
                        Assert.That(Get<ICrossPromoResourceManager>(promo, "m_resourceManager"), Is.TypeOf<CrossPromoResourceManager>());
                        Assert.That(Get<ICrossPromoDataParser>(promo, "m_parser"), Is.TypeOf<CrossPromoDataParser>());
                        Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, incoming)), Is.True);
                        Assert.That(Directory.Exists(Path.Combine(Application.persistentDataPath, destination)), Is.True);
                    },
                    () => DeleteIfPresent(Path.Combine(Application.persistentDataPath, incoming)),
                    () => DeleteIfPresent(Path.Combine(Application.persistentDataPath, destination)));
                });
        }

        private static void DeleteIfPresent(string path)
        {
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
