using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Controlled managed obligations only. No platform identity or original SDK parity.
    public static class LocalAccountVerification
    {
        private const string ListenerName = "ProjectLucid.Offline.LocalAccountListener";
        private const string PlayerName = "ProjectLucid.Offline.LocalAccountPlayer";
        private const string ChallengeError = "Platform achievement challenges are unavailable offline.";
        private static bool facadeLeaseBusy;

        private static void RequireLocalFactories()
        {
            Assembly assembly = typeof(GameCenterLocalPlayerUtility).Assembly;
            Type listener = assembly.GetType(ListenerName, true);
            Type player = assembly.GetType(PlayerName, true);
            Assert.AreSame(assembly, listener.Assembly);
            Assert.AreSame(assembly, player.Assembly);
            Assert.IsTrue(typeof(IGameCenterLocalPlayerListener).IsAssignableFrom(listener));
            Assert.IsTrue(typeof(IGameCenterLocalPlayerListenerCallbackHandler).IsAssignableFrom(listener));
            Assert.IsTrue(typeof(INativeGameCenterLocalPlayer).IsAssignableFrom(player));
            Assert.IsFalse(typeof(MonoBehaviour).IsAssignableFrom(listener));
            RequireOnlyLocalConstructor("CreateGameCenterLocalPlayerListener", listener, Type.EmptyTypes);
            RequireOnlyLocalConstructor("CreateNativeGameCenterLocalPlayer", player,
                new[] { typeof(IGameCenterLocalPlayerListenerCallbackHandler) });
        }

        // Inspect every operand before any factory invocation. This is a route-safety
        // preflight for the controlled source, not a complete emission/parity witness.
        private static void RequireOnlyLocalConstructor(string name, Type owner, Type[] parameters)
        {
            MethodInfo factory = typeof(GameCenterLocalPlayerUtility).GetMethod(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(factory);
            byte[] il = factory.GetMethodBody().GetILAsByteArray();
            var codes = new Dictionary<ushort, OpCode>();
            foreach (FieldInfo field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
                if (field.FieldType == typeof(OpCode))
                {
                    OpCode code = (OpCode)field.GetValue(null);
                    codes[unchecked((ushort)code.Value)] = code;
                }
            int index = 0, constructors = 0;
            while (index < il.Length)
            {
                ushort value = il[index++];
                if (value == 0xfe)
                {
                    Assert.Less(index, il.Length);
                    value = (ushort)(0xfe00 | il[index++]);
                }
                Assert.IsTrue(codes.TryGetValue(value, out OpCode code));
                int size;
                switch (code.OperandType)
                {
                    case OperandType.InlineNone: size = 0; break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineVar: size = 1; break;
                    case OperandType.InlineVar: size = 2; break;
                    case OperandType.InlineI:
                    case OperandType.InlineBrTarget:
                    case OperandType.ShortInlineR: size = 4; break;
                    case OperandType.InlineI8:
                    case OperandType.InlineR: size = 8; break;
                    case OperandType.InlineMethod:
                        Assert.AreEqual(OpCodes.Newobj.Value, code.Value);
                        Assert.LessOrEqual(index + 4, il.Length);
                        ConstructorInfo constructor = factory.Module.ResolveMethod(BitConverter.ToInt32(il, index)) as ConstructorInfo;
                        Assert.IsNotNull(constructor);
                        Assert.AreSame(owner, constructor.DeclaringType);
                        ParameterInfo[] args = constructor.GetParameters();
                        Assert.AreEqual(parameters.Length, args.Length);
                        for (int n = 0; n < args.Length; n++) Assert.AreSame(parameters[n], args[n].ParameterType);
                        constructors++;
                        size = 4;
                        break;
                    default:
                        throw new InvalidOperationException("Unreviewed factory operand: " + code.OperandType);
                }
                Assert.LessOrEqual(index + size, il.Length);
                index += size;
            }
            Assert.AreEqual(il.Length, index);
            Assert.AreEqual(1, constructors);
        }

        private sealed class Account : IDisposable
        {
            internal readonly IGameCenterLocalPlayerListener Listener;
            internal readonly IGameCenterLocalPlayerListenerCallbackHandler Callbacks;
            internal readonly INativeGameCenterLocalPlayer Player;
            internal Account(bool forceStub)
            {
                RequireLocalFactories();
                Listener = GameCenterLocalPlayerUtility.CreateGameCenterLocalPlayerListener(forceStub);
                Assert.AreEqual(ListenerName, Listener.GetType().FullName);
                Callbacks = (IGameCenterLocalPlayerListenerCallbackHandler)Listener;
                try
                {
                    Player = GameCenterLocalPlayerUtility.CreateNativeGameCenterLocalPlayer(Callbacks, forceStub);
                    Assert.AreEqual(PlayerName, Player.GetType().FullName);
                }
                catch
                {
                    try { Player?.Deinitialise(); }
                    finally { Listener.Clear(); }
                    throw;
                }
            }
            public void Dispose()
            {
                try { Player.Deinitialise(); }
                finally { Listener.Clear(); }
            }
        }

        public static void BothFactoryFlagsKeepIdentitySeparateFromAuthentication()
        {
            foreach (bool forceStub in new[] { false, true })
                using (var account = new Account(forceStub))
                {
                    account.Player.Initialise(null, GameCenterLocalPlayer.Separator);
                    Assert.IsNull(account.Listener.Name);
                    Assert.IsFalse(account.Player.Authenticated);
                    Assert.IsNull(account.Player.GamePlayerID);
                    Assert.IsFalse(account.Player.Underage);
                    Assert.IsTrue(account.Player.MultiplayerGamingRestricted);
                    Assert.IsTrue(account.Player.PersonalisedCommunicationRestricted);
                    Assert.IsNull(account.Player.GetPlayerPropertiesJoin("owned"));
                    Assert.IsNull(account.Player.GetLocalPlayerPropertiesJoin());
                    Assert.AreEqual(0, account.Player.GetLastPhotoForSizeLoadedPixelHeight());
                    Assert.AreEqual(0, account.Player.GetLastPhotoForSizeLoadedPixelWidth());
                }
            RequireLocalFactories();
            Assert.Throws<ArgumentNullException>(() => GameCenterLocalPlayerUtility.CreateNativeGameCenterLocalPlayer(null, false));
            Assert.Throws<ArgumentNullException>(() => GameCenterLocalPlayerUtility.CreateNativeGameCenterLocalPlayer(null, true));
            Type identity = typeof(GameCenterLocalPlayerUtility).Assembly.GetType("ProjectLucid.Offline.LocalProfileIdentity", true);
            Assert.AreEqual("project-lucid:local:default", identity.GetField("Identifier").GetRawConstantValue());
            Assert.AreEqual("Local Player", identity.GetField("DisplayName").GetRawConstantValue());
        }

        public static void RealCallbacksPreserveOrderPayloadAndClear()
        {
            using (var account = new Account(false))
            {
                var calls = new List<string>();
                account.Listener.OnAuthenticateHandler += success => calls.Add("auth:" + success);
                account.Listener.OnLoadPhotoForSize += (hash, image) =>
                {
                    Assert.AreEqual(int.MinValue, hash);
                    Assert.IsNull(image);
                    calls.Add("photo");
                };
                account.Listener.OnLoadPhotoForSizeCompleted += (hash, success) =>
                {
                    Assert.AreEqual(int.MinValue, hash);
                    Assert.IsFalse(success);
                    calls.Add("complete");
                };
                account.Listener.OnPlayerDidChange += id => { Assert.IsNull(id); calls.Add("changed"); };
                account.Player.RegisterAuthenticateHandler();
                account.Player.UnregisterAuthenticateHandler();
                account.Player.LoadPhotoForSize(int.MinValue, PhotoSize.PhotoSizeNormal, "owned");
                account.Callbacks.TriggerPlayerDidChange(null); // Explicit owned event transport, not a platform notification.
                account.Player.IssueAchievementChallenge("owned", "message", (success, error) =>
                {
                    Assert.IsFalse(success); Assert.AreEqual(ChallengeError, error); calls.Add("challenge");
                });
                account.Player.IssueAchievementChallenge(null, null, null);
                CollectionAssert.AreEqual(new[] { "auth:False", "photo", "complete", "changed", "challenge" }, calls);
                account.Player.Deinitialise();
                account.Listener.Clear();
                account.Player.RegisterAuthenticateHandler();
                account.Player.LoadPhotoForSize(int.MinValue, PhotoSize.PhotoSizeSmall, null);
                account.Callbacks.TriggerPlayerDidChange(null);
                Assert.AreEqual(5, calls.Count);
            }
        }

        public static void CallbackFaultStopsLaterPhotoCompletionAndOwnedClearRecovers()
        {
            using (var account = new Account(true))
            {
                var failure = new InvalidOperationException("owned callback fault");
                int completion = 0;
                Action<int, Texture2D> throwing = (hash, image) => { throw failure; };
                account.Listener.OnLoadPhotoForSize += throwing;
                account.Listener.OnLoadPhotoForSizeCompleted += (hash, success) => completion++;
                Assert.AreSame(failure, Assert.Throws<InvalidOperationException>(() =>
                    account.Player.LoadPhotoForSize(17, PhotoSize.PhotoSizeNormal, null)));
                Assert.AreEqual(0, completion);
                account.Listener.OnLoadPhotoForSize -= throwing;
                account.Player.LoadPhotoForSize(17, PhotoSize.PhotoSizeNormal, null);
                Assert.AreEqual(1, completion);
                account.Listener.Clear();
                account.Player.LoadPhotoForSize(17, PhotoSize.PhotoSizeNormal, null);
                Assert.AreEqual(1, completion);
            }
        }

        private sealed class FacadeLease : IDisposable
        {
            internal readonly GameCenterLocalPlayer Facade;
            internal readonly IGameCenterLocalPlayerReferencesHolder Holder;
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> actions;
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> prior;
            private readonly Dictionary<SystemAction, object> priorRows;
            private readonly IDictionary registry;
            private readonly Dictionary<object, object> priorRegistry;
            private readonly List<Action<object>> pending;
            private readonly Action<object>[] priorPending;
            private static FieldInfo Field(string name)
            {
                FieldInfo result = typeof(ProcessManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Static);
                Assert.IsNotNull(result); return result;
            }
            internal FacadeLease(Func<GameCenterLocalPlayer> create)
            {
                RequireLocalFactories();
                if (facadeLeaseBusy) throw new InvalidOperationException("An owned facade lease is already active.");
                Assert.IsFalse((bool)Field("s_systemActionInProgress").GetValue(null));
                actions = (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)Field("s_systemActionLookup").GetValue(null);
                registry = (IDictionary)Field("s_systemDictionary").GetValue(null);
                pending = (List<Action<object>>)Field("s_actionList").GetValue(null);
                priorPending = pending.ToArray();
                priorRegistry = new Dictionary<object, object>();
                foreach (DictionaryEntry entry in registry) priorRegistry.Add(entry.Key, entry.Value);
                prior = new Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>();
                priorRows = new Dictionary<SystemAction, object>();
                foreach (var entry in actions)
                {
                    prior.Add(entry.Key, new Dictionary<ISystem, Action<object>>(entry.Value));
                    priorRows.Add(entry.Key, entry.Value);
                }
                facadeLeaseBusy = true;
                try
                {
                    Facade = create();
                    Holder = (IGameCenterLocalPlayerReferencesHolder)Facade;
                    Assert.AreEqual(ListenerName, Holder.GameCenterLocalPlayerListener.GetType().FullName);
                    Assert.AreEqual(PlayerName, Holder.NativeGameCenterLocalPlayer.GetType().FullName);
                }
                catch
                {
                    try
                    {
                        if (Facade != null)
                        {
                            try { ProcessManager.ProcessSystemAction(Facade, SystemAction.AppShutdown); }
                            finally
                            {
                                try { Facade.Deinitialise(); }
                                finally { ProcessManager.UnsubscribeFromAllActions(Facade); }
                            }
                        }
                    }
                    finally { facadeLeaseBusy = false; }
                    throw;
                }
            }
            public void Dispose()
            {
                try { ProcessManager.ProcessSystemAction(Facade, SystemAction.AppShutdown); }
                finally
                {
                    try { Facade.Deinitialise(); }
                    finally
                    {
                        try { ProcessManager.UnsubscribeFromAllActions(Facade); }
                        finally
                        {
                            try
                            {
                                Assert.IsFalse((bool)Field("s_systemActionInProgress").GetValue(null));
                                Assert.AreSame(actions, Field("s_systemActionLookup").GetValue(null));
                                Assert.AreSame(registry, Field("s_systemDictionary").GetValue(null));
                                Assert.AreSame(pending, Field("s_actionList").GetValue(null));
                                Assert.AreEqual(priorRegistry.Count, registry.Count);
                                foreach (var entry in priorRegistry) Assert.AreSame(entry.Value, registry[entry.Key]);
                                Assert.AreEqual(priorPending.Length, pending.Count);
                                for (int i = 0; i < priorPending.Length; i++) Assert.AreSame(priorPending[i], pending[i]);
                                foreach (var entry in actions)
                                {
                                    if (!prior.TryGetValue(entry.Key, out var original))
                                    {
                                        // Genuine Unsubscribe retains newly introduced empty action rows.
                                        Assert.IsTrue(entry.Key == SystemAction.AppInitialise || entry.Key == SystemAction.AppShutdown);
                                        Assert.AreEqual(0, entry.Value.Count);
                                        continue;
                                    }
                                    Assert.AreSame(priorRows[entry.Key], entry.Value);
                                    Assert.AreEqual(original.Count, entry.Value.Count);
                                    foreach (var callback in original) Assert.AreSame(callback.Value, entry.Value[callback.Key]);
                                }
                                foreach (var entry in prior) Assert.IsTrue(actions.ContainsKey(entry.Key));
                            }
                            finally { facadeLeaseBusy = false; }
                        }
                    }
                }
            }
        }

        public static void GenuineFacadeDefaultsKeepFailedAuthenticationAndShutdownCleanup()
        {
            foreach (Func<GameCenterLocalPlayer> create in new Func<GameCenterLocalPlayer>[]
                { () => new GameCenterLocalPlayer(), () => new GameCenterLocalPlayer(false), () => new GameCenterLocalPlayer(true) })
                using (var lease = new FacadeLease(create))
                {
                    int auth = 0, changes = 0;
                    lease.Facade.OnAuthenticateHandler += success => { Assert.IsFalse(success); auth++; };
                    lease.Facade.OnPlayerDidChange += id => { Assert.IsNull(id); changes++; };
                    ProcessManager.ProcessSystemAction(lease.Facade, SystemAction.AppInitialise);
                    lease.Facade.Initialise();
                    lease.Facade.RegisterAuthenticateHandler();
                    Assert.AreEqual(1, auth);
                    Assert.IsFalse(lease.Facade.Authenticated);
                    var callbacks = (IGameCenterLocalPlayerListenerCallbackHandler)lease.Holder.GameCenterLocalPlayerListener;
                    callbacks.TriggerPlayerDidChange(null);
                    Assert.AreEqual(1, changes);
                    Assert.IsFalse(lease.Facade.TryGetLocalPlayer(out Player local));
                    Assert.IsNull(local.GamePlayerID); Assert.IsNull(local.DisplayName);
                    Assert.IsFalse(lease.Facade.TryGetPlayer("owned", out Player queried));
                    Assert.IsNull(queried.GamePlayerID); Assert.IsNull(queried.DisplayName);
                    Assert.IsFalse(lease.Facade.TryGetUnderage(out bool underage)); Assert.IsTrue(underage);
                    Assert.IsFalse(lease.Facade.TryGetMultiplayerGamingRestricted(out bool restricted)); Assert.IsTrue(restricted);
                    Assert.IsFalse(lease.Facade.TryGetPersonalisedCommunicationRestricted(out bool communication)); Assert.IsTrue(communication);
                    ProcessManager.ProcessSystemAction(lease.Facade, SystemAction.AppShutdown);
                    lease.Facade.RegisterAuthenticateHandler();
                    callbacks.TriggerPlayerDidChange(null);
                    Assert.AreEqual(1, auth); Assert.AreEqual(1, changes);
                }
        }

        private static object ReadOwnedListenerEvent(IGameCenterLocalPlayerListener listener, string name)
        {
            Assert.AreEqual(ListenerName, listener.GetType().FullName);
            Assert.AreSame(typeof(GameCenterLocalPlayerUtility).Assembly, listener.GetType().Assembly);
            FieldInfo field = listener.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            Assert.IsTrue(typeof(Delegate).IsAssignableFrom(field.FieldType));
            return field.GetValue(listener); // Read-only owned event identity, never injection.
        }

        public static void GenuinePlayerPhotoIteratorCompletesWithoutYieldThroughLocalCallbacks()
        {
            using (var lease = new FacadeLease(() => new GameCenterLocalPlayer()))
            {
                Assert.IsTrue(Player.TryParseProperties("owned-id|Owned", "owned-id", lease.Holder, out Player player));
                object priorPhoto = ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSize");
                object priorCompleted = ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSizeCompleted");
                int completion = 0;
                IEnumerator iterator = player.LoadPhotoForSize(PhotoSize.PhotoSizeSmall, (success, photo) =>
                { Assert.IsFalse(success); Assert.IsNull(photo); completion++; });
                try
                {
                    Assert.IsFalse(iterator.MoveNext());
                    Assert.AreEqual(1, completion);
                    Assert.IsFalse(iterator.MoveNext());
                    Assert.AreEqual(1, completion);
                }
                finally { (iterator as IDisposable)?.Dispose(); }
                Assert.AreSame(priorPhoto, ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSize"));
                Assert.AreSame(priorCompleted, ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSizeCompleted"));
                // A second independently owned natural iterator also completes normally.
                IEnumerator next = player.LoadPhotoForSize(PhotoSize.PhotoSizeNormal, (success, photo) =>
                { Assert.IsFalse(success); Assert.IsNull(photo); completion++; });
                try { Assert.IsFalse(next.MoveNext()); Assert.AreEqual(2, completion); }
                finally { (next as IDisposable)?.Dispose(); }
                Assert.AreSame(priorPhoto, ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSize"));
                Assert.AreSame(priorCompleted, ReadOwnedListenerEvent(lease.Holder.GameCenterLocalPlayerListener, "OnLoadPhotoForSizeCompleted"));
            }
        }
    }
}
