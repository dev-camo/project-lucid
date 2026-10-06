using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using Table = Hardlight.Localisation.StringTable;
using Definition = ClientDataAPI.LocalisationDefinitions;

namespace ProjectLucid.Tests
{
    // Bounded real Unity lifecycle and nested coroutine fixture. Generated
    // codec files are temporary proof inputs, not original authored content.
    public sealed class StringTableLifecycleTests
    {
        private static readonly BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static FieldInfo Field(Type type, string name) => type.GetField(name,Declared);
        private static Definition Data(Languages language, string hash, string title) => new Definition
        {
            SupportedLanguages = new[] { new ClientDataAPI.SupportedLanguage { Language = (int)language } },
            StringTable = new ClientDataAPI.StringTable
            {
                Language = (int)language, Hash = hash,
                Strings = new[]
                {
                    new ClientDataAPI.LocalisedString { Id="NUMBER_SEPARATOR",Content="_",NumArgs=0 },
                    new ClientDataAPI.LocalisedString { Id="DECIMAL_SEPARATOR",Content=".",NumArgs=0 },
                    new ClientDataAPI.LocalisedString { Id="title",Content=title,NumArgs=2 }
                }
            }
        };

        [UnityTest]
        public IEnumerator OriginalStringTable_AutomaticLoadingDeferredHandlersAndCleanup()
        {
            Assert.IsTrue(ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance,null));
            Assert.IsTrue(ProcessManager.IsSystemNull<Language>() && ReferenceEquals(MonoSingleton<Language>.Instance,null));
            Assert.IsTrue(ProcessManager.IsSystemNull<HLUnityCore>() && ReferenceEquals(MonoSingleton<HLUnityCore>.Instance,null));
            string temporary=Path.Combine(Path.GetTempPath(),"project-lucid-live-strings-"+Guid.NewGuid().ToString("N"));
            Type tableType=typeof(Table),storeType=typeof(HLPropertyStore),coreType=typeof(HLUnityCore);
            var tableFields=new List<FieldInfo>();var tableValues=new List<object>();
            foreach (FieldInfo field in tableType.GetFields(Declared)) if (field.IsStatic && !field.IsLiteral) { tableFields.Add(field);tableValues.Add(field.GetValue(null)); }
            FieldInfo storeInstance=Field(storeType,"s_internalInstance"),loads=Field(storeType,"LoadHandlers"),saves=Field(storeType,"SaveHandlers"),languageEvent=Field(typeof(Language),"OnLanguageChanged"),bridge=Field(coreType,"s_unityCoreNativeBridge"),tracking=Field(coreType,"s_trackingAuthorisationNativeBridge");
            object oldStore=storeInstance.GetValue(null),oldLoads=loads.GetValue(null),oldSaves=saves.GetValue(null),oldLanguageEvent=languageEvent.GetValue(null),oldBridge=bridge.GetValue(null),oldTracking=tracking.GetValue(null);
            bool priorConfiguration=!ProcessManager.IsSystemNull<SystemConfiguration>();SystemConfiguration createdConfiguration=null;
            var config=ScriptableObject.CreateInstance<HLLocalisationConfigurationAsset>();var coreConfig=ScriptableObject.CreateInstance<HLUnityCoreConfigurationAsset>();
            StackableDataHandle configurationHandle=null,coreHandle=null;
            GameObject coreOwner=null,languageOwner=null,tableOwner=null;HLUnityCore core=null;Language language=null;Table table=null;Application.LogCallback retainedCoreCallback=null;
            bool ready=false;int readinessCalls=0,handlerCalls=0,requestCalls=0;var trace=new List<string>();var customKeys=new List<string>();
            SystemRef<Table> reference=ProcessManager.GetSystemRef<Table>();bool cleanupObserved=false;
            Action<Table> shutdown=value=>
            {
                Assert.AreSame(table,value);
                Assert.AreSame(table,MonoSingleton<Table>.Instance,"base unregistration precedes static singleton clear");
                var loadCallbacks=((Delegate)loads.GetValue(null)).GetInvocationList();var saveCallbacks=((Delegate)saves.GetValue(null)).GetInvocationList();
                Assert.IsFalse(Array.Exists(loadCallbacks,d=>ReferenceEquals(d.Target,table)),"original destruction removed table load callback before base");
                Assert.IsFalse(Array.Exists(saveCallbacks,d=>ReferenceEquals(d.Target,table)),"original destruction removed table save callback before base");
                var changed=(Delegate)languageEvent.GetValue(null);
                Assert.IsFalse(changed!=null && Array.Exists(changed.GetInvocationList(),d=>ReferenceEquals(d.Target,table)),"original destruction removed table language callback before base");
                cleanupObserved=true;
            };
            try
            {
                Directory.CreateDirectory(temporary);
                File.WriteAllBytes(Path.Combine(temporary,"Japanese.bytes"),Data(Languages.Japanese,"live-japanese","japanese proof").encode());
                File.WriteAllBytes(Path.Combine(temporary,"Korean.bytes"),Data(Languages.Korean,"live-korean","korean proof").encode());
                Field(typeof(HLLocalisationConfigurationAsset),"m_localisedDefinitionsDirectory").SetValue(config,temporary);
                configurationHandle=SystemConfiguration.AddConfig<SystemConfigurationAsset>(config);
                coreHandle=SystemConfiguration.AddConfig<SystemConfigurationAsset>(coreConfig);
                if (!priorConfiguration) createdConfiguration=ProcessManager.GetSystem<SystemConfiguration>();
                var properties=new HLPropertyList();properties.AddProperty("language_override",true);properties.AddProperty("language_current","Japanese");properties.AddProperty("StringTableCachedTime",-1234567890123L);
                // Genuine original already-loaded store fields, without a fake
                // storage provider or constructor/file/transport replacement.
                var store=(HLPropertyStore)FormatterServices.GetUninitializedObject(storeType);Field(storeType,"m_isLoaded").SetValue(store,true);Field(storeType,"ActiveProperties").SetValue(store,properties);
                storeInstance.SetValue(null,store);loads.SetValue(null,null);saves.SetValue(null,null);languageEvent.SetValue(null,null);
                coreOwner=new GameObject("Lucid original localisation logging host");core=coreOwner.AddComponent<HLUnityCore>();
                retainedCoreCallback=(Application.LogCallback)Delegate.CreateDelegate(typeof(Application.LogCallback),core,coreType.GetMethod("OnHandleLog",Declared));
                core.AddCustomKeyStringHandler((key,value)=>customKeys.Add(key+":"+value));
                languageOwner=new GameObject("Lucid original localisation Language");language=languageOwner.AddComponent<Language>();
                yield return null;
                Assert.IsTrue(language.LanguageLoaded && language.CurrentLanguage==Languages.Japanese && Language.IsLanguageOverriden(),"real automatic Language.Start consumes genuine saved override");
                Field(tableType,"StringsTableHandlers").SetValue(null,null);Field(tableType,"OnRequestStringTable").SetValue(null,null);Field(tableType,"<Hash>k__BackingField").SetValue(null,null);
                Field(tableType,"ShouldInvokeStringHandlers").SetValue(null,(Func<bool>)(()=>{++readinessCalls;return ready;}));
                Table.StringsTableHandlers+=()=>
                {
                    ++handlerCalls;trace.Add("handlers:"+Table.StringsLanguage);
                    Assert.IsTrue(ready);Assert.AreEqual(Table.StringsLanguage==Languages.Japanese?"japanese proof":"korean proof",Table.GetString("title"));
                };
                Table.OnRequestStringTable+=()=>{++requestCalls;trace.Add("request:"+Table.StringsLanguage);};
                reference.OnSystemShutdown+=shutdown;
                tableOwner=new GameObject("Lucid original automatic StringTable");table=tableOwner.AddComponent<Table>();
                Assert.AreSame(table,MonoSingleton<Table>.Instance,"Unity automatically dispatches original table Awake");
                Assert.AreSame(table,reference.GetSafe());Assert.IsFalse(Table.AreStringsLoaded());
                yield return null;
                Assert.IsTrue(Table.AreStringsLoaded());Assert.AreEqual("live-japanese",Table.Hash);Assert.AreEqual("japanese proof",Table.GetString("title"));
                Assert.AreEqual("_",table.NumberFormat.NumberGroupSeparator);Assert.AreEqual(0,handlerCalls);Assert.AreEqual(0,requestCalls);
                Assert.That(readinessCalls,Is.GreaterThanOrEqualTo(2),"Start applies definition/readiness and Unity starts the deferred iterator immediately");
                Assert.IsNotNull(Field(tableType,"m_coroutineDelayingStringHandlerInvocation").GetValue(table));
                var saved=new HLPropertyList();((HLPropertyStore.SaveHandler)saves.GetValue(null))(saved);
                Assert.AreEqual(-1234567890123L,saved.AsLong("StringTableCachedTime"),"original Start's immediate loaded-store callback precedes save registration");
                ready=true;
                for (int i=0;i<4 && handlerCalls==0;++i) yield return null;
                CollectionAssert.AreEqual(new[]{"handlers:Japanese","request:Japanese"},trace,"live readiness publishes language then handlers then request");
                Assert.AreEqual(1,handlerCalls);Assert.AreEqual(1,requestCalls);
                Assert.IsNotNull(Field(tableType,"m_coroutineDelayingStringHandlerInvocation").GetValue(table),"original completion does not clear retained coroutine handle");
                trace.Clear();Assert.IsTrue(Language.OverrideLanguage(Languages.Korean));
                Assert.AreEqual("live-korean",Table.Hash);Assert.AreEqual("korean proof",Table.GetString("title"));Assert.AreEqual(Languages.Korean,Table.StringsLanguage);
                CollectionAssert.AreEqual(new[]{"handlers:Korean","request:Korean"},trace,"original language event synchronously loads/applies the second genuine generated definition");
                CollectionAssert.AreEqual(new[]{"Language:Japanese","Language:Korean"},customKeys,"play-mode loader calls real registered HLUnityCore custom-key transport before cache/file");
                Assert.AreEqual(2,handlerCalls);Assert.AreEqual(2,requestCalls);
                Object.Destroy(tableOwner);tableOwner=null;yield return null;
                Assert.IsTrue(cleanupObserved && ProcessManager.IsSystemNull<Table>() && ReferenceEquals(MonoSingleton<Table>.Instance,null));
                Assert.IsFalse(Table.AreStringsLoaded());Assert.AreEqual("live-korean",Table.Hash,"original destruction retains global last hash");
                Assert.IsNotNull(Field(tableType,"OnRequestStringTable").GetValue(null),"original destruction retains static request event");
                Assert.IsTrue(Language.OverrideLanguage(Languages.Japanese));Assert.AreEqual(2,handlerCalls,"destroyed table removed original language subscription");
            }
            finally
            {
                reference.OnSystemShutdown-=shutdown;
                if (tableOwner!=null) Object.DestroyImmediate(tableOwner);
                if (languageOwner!=null) Object.DestroyImmediate(languageOwner);
                if (coreOwner!=null) Object.DestroyImmediate(coreOwner);
                if (retainedCoreCallback!=null) Application.logMessageReceived-=retainedCoreCallback;
                bridge.SetValue(null,oldBridge);tracking.SetValue(null,oldTracking);
                if (configurationHandle!=null) ProcessManager.GetSystem<SystemConfiguration>().StackableData.RemoveOverrides(configurationHandle);
                if (coreHandle!=null) ProcessManager.GetSystem<SystemConfiguration>().StackableData.RemoveOverrides(coreHandle);
                if (createdConfiguration!=null) ProcessManager.UnregisterSystem(createdConfiguration);
                if (config!=null) Object.DestroyImmediate(config);if (coreConfig!=null) Object.DestroyImmediate(coreConfig);
                storeInstance.SetValue(null,oldStore);loads.SetValue(null,oldLoads);saves.SetValue(null,oldSaves);languageEvent.SetValue(null,oldLanguageEvent);
                for (int i=0;i<tableFields.Count;++i) tableFields[i].SetValue(null,tableValues[i]);
                if (Directory.Exists(temporary)) Directory.Delete(temporary,true);
            }
        }
    }
}
