using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Verification
{
    public static class GameCenterLocalPlayerPreservationVerification
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static void Check(bool condition, string message, ref int count)
        {
            if (!condition) throw new InvalidOperationException(message);
            ++count;
        }
        private static bool ThrowsNull(Action action)
        {
            try { action(); return false; }
            catch (NullReferenceException) { return true; }
            catch (TargetInvocationException e) when (e.InnerException is NullReferenceException) { return true; }
        }
        private static void Invoke(GameCenterLocalPlayer value, string method, object argument) =>
            typeof(GameCenterLocalPlayer).GetMethod(method, PrivateInstance).Invoke(value, new[] { argument });

        // Preserve every existing action row, dictionary and delegate identity.
        // The original registry keeps empty rows after unsubscribe.
        private sealed class ActionSnapshot : IDisposable
        {
            private readonly Dictionary<SystemAction, Dictionary<ISystem, Action<object>>> lookup;
            private readonly List<KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>>> rows;
            private readonly List<List<KeyValuePair<ISystem, Action<object>>>> entries;
            public ActionSnapshot()
            {
                lookup = (Dictionary<SystemAction, Dictionary<ISystem, Action<object>>>)typeof(ProcessManager)
                    .GetField("s_systemActionLookup", PrivateStatic).GetValue(null);
                rows = new List<KeyValuePair<SystemAction, Dictionary<ISystem, Action<object>>>>(lookup);
                entries = new List<List<KeyValuePair<ISystem, Action<object>>>>();
                foreach (var row in rows) entries.Add(new List<KeyValuePair<ISystem, Action<object>>>(row.Value));
            }
            public void Dispose()
            {
                lookup.Clear();
                for (int i = 0; i < rows.Count; ++i)
                {
                    rows[i].Value.Clear();
                    foreach (var pair in entries[i]) rows[i].Value.Add(pair.Key, pair.Value);
                    lookup.Add(rows[i].Key, rows[i].Value);
                }
            }
        }

        public static int ShippingStubIdentityAndCallbacks()
        {
            int count = 0, first = 0, second = 0;
            bool last = false;
            var listener = new GameCenterLocalPlayerListenerStub();
            Action<bool> a = value => { ++first; last = value; };
            Action<bool> b = value => ++second;
            listener.OnAuthenticateHandler += a;
            Check(first == 1 && last, "First authentication subscriber must receive true immediately.", ref count);
            listener.OnAuthenticateHandler += b;
            Check(first == 2 && second == 1, "Adding a second subscriber must reinvoke the full combined delegate.", ref count);
            listener.OnAuthenticateHandler += null;
            Check(first == 3 && second == 2, "Adding null must reinvoke the retained delegate.", ref count);
            listener.OnAuthenticateHandler -= b;
            Check(first == 3 && second == 2, "Removing a subscriber must not invoke authentication.", ref count);
            listener.TriggerAuthenticateHandlerEvent(false);
            Check(first == 4 && second == 2 && !last, "Explicit authentication event must retain false.", ref count);
            listener.Clear();
            listener.TriggerAuthenticateHandlerEvent(true);
            Check(first == 5 && second == 2 && last, "Clear must retain authentication subscribers.", ref count);
            Check(listener.Name == null, "Shipping listener Name must be null.", ref count);

            int photoHash = 0, completedHash = 0, photos = 0, completions = 0;
            Texture2D photo = null;
            bool completed = true;
            string playerId = "unobserved";
            listener.OnLoadPhotoForSize += (hash, value) => { ++photos; photoHash = hash; photo = value; };
            listener.OnLoadPhotoForSizeCompleted += (hash, value) => { ++completions; completedHash = hash; completed = value; };
            listener.OnPlayerDidChange += value => playerId = value;
            listener.TriggerLoadPhotoForSizeEvent(int.MinValue, null);
            Check(photos == 1 && photoHash == int.MinValue && ReferenceEquals(photo, null), "Photo dispatch must preserve null and signed hash.", ref count);
            listener.TriggerLoadPhotoForSizeCompletedEvent(int.MaxValue, false);
            Check(completions == 1 && completedHash == int.MaxValue && !completed, "Completion dispatch must preserve false and signed hash.", ref count);
            listener.TriggerPlayerDidChange(null);
            Check(playerId == null, "Player event must preserve null.", ref count);
            listener.Clear();
            listener.TriggerPlayerDidChange("retained");
            Check(playerId == "retained", "Clear must retain player subscribers.", ref count);

            var native = new GameCenterLocalPlayerStub(listener);
            Check(native.Authenticated && !native.Underage && !native.MultiplayerGamingRestricted && !native.PersonalisedCommunicationRestricted,
                "Shipping native Stub must retain its four fixed flags.", ref count);
            Check(Guid.TryParse(native.GamePlayerID, out Guid ignored), "Shipping identifier must be a GUID string.", ref count);
            string local = native.GamePlayerID + "|Stub Local Player";
            Check(native.GetLocalPlayerPropertiesJoin() == local, "Local join must retain exact shipped name and separator.", ref count);
            Check(native.GetPlayerPropertiesJoin(native.GamePlayerID) == local, "Matching identity must reuse the exact local join.", ref count);
            string unknownA = native.GetPlayerPropertiesJoin("unknown");
            string unknownB = native.GetPlayerPropertiesJoin(null);
            string[] splitA = unknownA.Split('|'), splitB = unknownB.Split('|');
            Check(splitA.Length == 2 && splitA[1] == "Stub Player" && Guid.TryParse(splitA[0], out ignored), "Unknown identity must use a generated GUID and original display name.", ref count);
            Check(splitB.Length == 2 && splitB[1] == "Stub Player" && splitA[0] != splitB[0], "Each unknown identity lookup must generate another GUID.", ref count);
            int prior = first;
            native.RegisterAuthenticateHandler();
            Check(first == prior + 1 && last, "Registration must dispatch authenticated true through the real callback handler.", ref count);
            native.UnregisterAuthenticateHandler(); native.Initialise(null, "ignored"); native.Deinitialise();
            Check(first == prior + 1, "Empty lifecycle methods must not dispatch extra authentication.", ref count);
            Check(native.GetLastPhotoForSizeLoadedPixelHeight() == 0 && native.GetLastPhotoForSizeLoadedPixelWidth() == 0, "Shipping Stub dimensions must both be zero.", ref count);
            int challenges = 0; bool success = false; string error = "unobserved";
            native.IssueAchievementChallenge(null, null, (value, text) => { ++challenges; success = value; error = text; });
            native.IssueAchievementChallenge(null, null, null);
            Check(challenges == 1 && success && error == null, "Shipping challenge callback must report true/null and tolerate null callback.", ref count);
            var missing = new GameCenterLocalPlayerStub(null);
            Check(ThrowsNull(missing.RegisterAuthenticateHandler), "A missing callback handler must retain the original registration null fault.", ref count);
            return count;
        }

        public static int PlayerParsingAndIdentity()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var holder = ProjectLucid.Preservation.ShippingGameCenterObjects.CreateLocalPlayer();
                string[] joins = { null, "", "one", "one|two|three", "|", "one|", "|two", "one|two" };
                bool[] valid = { false, false, false, false, true, true, true, true };
                for (int i = 0; i < joins.Length; ++i)
                {
                    bool result = Player.TryParseProperties(joins[i], "identifier", holder, out Player player);
                    Check(result == valid[i], "Parser acceptance mismatch at vector " + i, ref count);
                    if (!valid[i]) Check(player.GamePlayerID == null && player.DisplayName == null, "Rejected parse must reset output at vector " + i, ref count);
                    else
                    {
                        string[] parts = joins[i].Split('|');
                        Check(player.GamePlayerID == parts[0] && player.DisplayName == parts[1], "Empty entries must remain present at vector " + i, ref count);
                    }
                }
                Check(!Player.TryParseProperties("one|two", null, holder, out Player invalidA) && invalidA.GamePlayerID == null, "Null identifier must reject and reset output.", ref count);
                Check(!Player.TryParseProperties("one|two", "", holder, out Player invalidB) && invalidB.DisplayName == null, "Empty identifier must reject and reset output.", ref count);
                Check(!Player.TryParseProperties("one|two", "identifier", null, out Player invalidC) && invalidC.GamePlayerID == null, "Null holder must reject and reset output.", ref count);
                var a = new Player("game-a", "name-a", "identifier", holder);
                var b = new Player("game-b", "name-b", "identifier", holder);
                Check(a.GamePlayerID == "game-a" && a.DisplayName == "name-a", "Constructor must retain authored game identifier and name.", ref count);
                Check(a == b && !(a != b), "Equality must ignore game identifier and display name when identifier hashes match.", ref count);
                Check(a.Equals(b) && a.Equals((object)b), "Typed and boxed equality must retain the same rule.", ref count);
                Check(!a.Equals(null) && !a.Equals("identifier"), "Object equality must reject null and unrelated objects before hashing.", ref count);
                Check(a.GetHashCode() == "identifier".GetHashCode(), "Hash code must come from the separate lookup identifier.", ref count);
                Check(a.ToString() == "GamePlayerID:game-a DisplayName:name-a", "Original ToString spacing and literals must be retained.", ref count);
                Check(new Player(null, null, "identifier", holder).ToString() == "GamePlayerID: DisplayName:", "String concatenation must retain null component handling.", ref count);
                MethodInfo match = typeof(Player).GetMethod("MatchIdentifier", PrivateInstance);
                Check((bool)match.Invoke(a, new object[] { "identifier" }), "Private identifier match must use lookup identifier.", ref count);
                Check(!(bool)match.Invoke(a, new object[] { null }), "A valid stored identifier must compare false to null.", ref count);
                Player empty = default;
                Check(ThrowsNull(() => empty.GetHashCode()), "Default player hash must retain the null fault.", ref count);
                Check(ThrowsNull(() => { bool equal = empty == a; }), "Default player equality must retain the null fault.", ref count);
                Check(!empty.Equals((object)"unrelated"), "Default object equality must reject unrelated type without hashing.", ref count);
                Check(ThrowsNull(() => match.Invoke(empty, new object[] { "identifier" })), "Default private match must retain its null fault.", ref count);
            }
            return count;
        }

        public static int LocalPlayerAuthenticationOrder()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var local = ProjectLucid.Preservation.ShippingGameCenterObjects.CreateLocalPlayer();
                var refs = (IGameCenterLocalPlayerReferencesHolder)local;
                var listener = (GameCenterLocalPlayerListenerStub)refs.GameCenterLocalPlayerListener;
                var trace = new List<string>();
                local.OnLocalPlayerDidChange += () => trace.Add("local");
                local.OnAuthenticateHandler += value => trace.Add("auth:" + value);
                local.OnPlayerDidChange += value => trace.Add("player:" + value);
                Check(refs.AreGameCenterLocalPlayerReferencesValid && local.Authenticated, "Explicit shipping route must hold both authentic providers.", ref count);
                Check(!local.TryGetLocalPlayer(out Player prior) && prior.GamePlayerID == null, "Before refresh, authenticated native player must still have an empty cache.", ref count);
                Invoke(local, "OnAppInitialise", null);
                Check(string.Join(",", trace) == "local,auth:True", "Immediate authentication subscription must refresh and emit local before auth.", ref count);
                Check(local.TryGetLocalPlayer(out Player player) && player.GamePlayerID == refs.NativeGameCenterLocalPlayer.GamePlayerID && player.DisplayName == "Stub Local Player", "Refresh must retain the authentic native local properties.", ref count);
                trace.Clear(); local.RegisterAuthenticateHandler();
                Check(string.Join(",", trace) == "local,auth:True", "Explicit authentication registration must preserve refresh/event order.", ref count);
                trace.Clear(); listener.TriggerPlayerDidChange(player.GamePlayerID);
                Check(string.Join(",", trace) == "local,player:" + player.GamePlayerID, "Matching local player change must refresh before player event.", ref count);
                trace.Clear(); listener.TriggerPlayerDidChange("other");
                Check(string.Join(",", trace) == "player:other", "Unmatched player change must skip refresh and retain player event.", ref count);
                trace.Clear();
                Check(ThrowsNull(() => listener.TriggerPlayerDidChange(null)) && trace.Count == 0, "Null changed identifier must fault before player callback when cached player is available.", ref count);
                listener.TriggerAuthenticateHandlerEvent(false);
                Check(string.Join(",", trace) == "auth:False", "False authentication must skip refresh and retain false callback.", ref count);
                Check(local.TryGetUnderage(out bool underage) && !underage, "Underage query must copy original false flag.", ref count);
                Check(local.TryGetMultiplayerGamingRestricted(out bool multiplayer) && !multiplayer, "Multiplayer query must copy original false flag.", ref count);
                Check(local.TryGetPersonalisedCommunicationRestricted(out bool communication) && !communication, "Communication query must copy original false flag.", ref count);
                local.Initialise(); local.Deinitialise(); local.UnregisterAuthenticateHandler();
                Check(local.TryGetPlayer("other", out Player other) && other.DisplayName == "Stub Player", "Other-player query must use the genuine native provider.", ref count);
                trace.Clear(); Invoke(local, "OnAppShutdown", null);
                listener.TriggerAuthenticateHandlerEvent(true); listener.TriggerPlayerDidChange("after");
                Check(trace.Count == 0, "Shutdown must remove the authored listener subscriptions.", ref count);
                Check(local.TryGetLocalPlayer(out Player retained) && retained.GamePlayerID == player.GamePlayerID, "Shutdown must leave the original local cache intact.", ref count);
            }
            return count;
        }

        // Requires a real Unity engine. The shipping route finishes synchronously;
        // no stand-in native provider, texture or coroutine implementation is used.
        public static int ShippingPhotoCoroutineInUnity()
        {
            int count = 0;
            using (new ActionSnapshot())
            {
                var local = ProjectLucid.Preservation.ShippingGameCenterObjects.CreateLocalPlayer();
                var refs = (IGameCenterLocalPlayerReferencesHolder)local;
                var listener = (GameCenterLocalPlayerListenerStub)refs.GameCenterLocalPlayerListener;
                Check(local.TryGetPlayer(refs.NativeGameCenterLocalPlayer.GamePlayerID, out Player player), "Original player parse must succeed before photo request.", ref count);
                var trace = new List<string>();
                int photoHash = 0, doneHash = 1; Texture2D eventPhoto = null, resultPhoto = null; bool resultSuccess = false; int callbacks = 0;
                listener.OnLoadPhotoForSize += (hash, texture) => { photoHash = hash; eventPhoto = texture; trace.Add("photo"); };
                listener.OnLoadPhotoForSizeCompleted += (hash, success) => { doneHash = hash; trace.Add("done:" + success); };
                IEnumerator routine = player.LoadPhotoForSize(PhotoSize.PhotoSizeNormal, (success, texture) => { ++callbacks; resultSuccess = success; resultPhoto = texture; trace.Add("callback"); });
                Check(trace.Count == 0, "Iterator construction must defer photo callbacks.", ref count);
                Check(!routine.MoveNext(), "Synchronous shipping completion must finish without yielding.", ref count);
                Check(string.Join(",", trace) == "photo,done:True,callback", "Photo, completion and final callback order must remain exact.", ref count);
                Check(photoHash == doneHash, "Both native callback events must retain the generated request hash.", ref count);
                Check(callbacks == 1 && resultSuccess, "Final completion must report exactly one successful callback.", ref count);
                Check(ReferenceEquals(eventPhoto, resultPhoto), "Final callback must retain the exact photo event object.", ref count);
                Check(resultPhoto == Texture2D.whiteTexture, "Shipping native path must use the real white texture.", ref count);
                Check(!routine.MoveNext() && callbacks == 1, "Completed iterator must not repeat its callback.", ref count);
                ((IDisposable)routine).Dispose();
                Check(callbacks == 1, "Original empty Dispose must not repeat completion.", ref count);
                Delegate photoDelegate = (Delegate)typeof(GameCenterLocalPlayerListenerStub).GetField("OnLoadPhotoForSize", PrivateInstance).GetValue(listener);
                Delegate doneDelegate = (Delegate)typeof(GameCenterLocalPlayerListenerStub).GetField("OnLoadPhotoForSizeCompleted", PrivateInstance).GetValue(listener);
                Check(photoDelegate.GetInvocationList().Length == 1, "Normal completion must remove only its own photo subscription.", ref count);
                Check(doneDelegate.GetInvocationList().Length == 1, "Normal completion must remove only its own completion subscription.", ref count);
            }
            return count;
        }
    }
}
