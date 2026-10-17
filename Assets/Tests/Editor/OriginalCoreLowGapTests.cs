using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;
using Hardlight.JSON;
using NUnit.Framework;
using ProjectLucid.Preservation;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // Low-priority HLUnityCore.Runtime gaps, each checked against the shipped
    // x86-64 body named in its comment. Owned objects are destroyed and any
    // registry action subscriptions they make are removed in finally.
    public sealed class OriginalCoreLowGapTests
    {
        // IEnumerableExtensions.GetHashCodeDeep 0x06000281 (x86-64 0x1ab0e40)
        // tests each element with IsInst IEnumerable (0x1ab1048) and recurses
        // (0x1ab1055) before any virtual GetHashCode (0x1ab106d); EqualsDeep
        // 0x06000280 (0x1ab0800) makes the same IsInst pair (0x1ab0beb/0x1ab0bf9)
        // before recursing (0x1ab0c1c). A string is IEnumerable, so it is hashed
        // and compared per char. The expected hash uses the native seed 0x21d
        // and multiplier 0x18d. Special-casing string (string.GetHashCode or
        // string.Equals) breaks the equality with the char array.
        [Test]
        public void NestedStringsHashAndCompareCharByCharThroughRecursion()
        {
            object[] text = { "ab" };
            object[] chars = { new[] { 'a', 'b' } };
            int inner = unchecked((((541 * 397) ^ 1 * ('a'.GetHashCode() + 1)) * 397) ^ 2 * ('b'.GetHashCode() + 1));
            int expected = unchecked((541 * 397) ^ 1 * (inner + 1));
            Assert.That(text.GetHashCodeDeep(), Is.EqualTo(expected));
            Assert.That(chars.GetHashCodeDeep(), Is.EqualTo(expected));
            Assert.That(text.EqualsDeep(chars), Is.True);
            Assert.That(chars.EqualsDeep(text), Is.True);
            Assert.That(text.EqualsDeep(new object[] { "ba" }), Is.False);
        }

        // JSONHelper.GetPoolSizeForType 0x060010a8 (x86-64 0x1b2ac30) passes the
        // type straight to Dictionary.TryGetValue (0x1b2aca3) and defaults to
        // 0x64 only on a miss (0x1b2acaa). The default comparer rejects a null
        // key, so a null-tolerant lookup returning 100 fails here.
        [Test]
        public void NullTypeFaultsInTheLookupInsteadOfReturningTheDefaultSize()
        {
            Assert.Throws<ArgumentNullException>(() => JSONHelper.GetPoolSizeForType(null));
            Assert.That(JSONHelper.GetPoolSizeForType(typeof(object)), Is.EqualTo(100));
        }

        // TimeScaledUtilities.WaitForFixedSeconds 0x06000eca, MoveNext 0x06000ed5
        // (x86-64 0x1b1a6c0): Time.get_fixedDeltaTime (0x1b1a7ce) is read before
        // TimeManager.GetTimescale (0x1b1a7de), then mulss/addss onto the timer.
        // The order is only observable if the category lookup changes Unity
        // time, so the compiled iterator body is inspected instead of run.
        // Reading the timescale first fails.
        [Test]
        public void FixedWaitReadsFixedDeltaTimeBeforeTheCategoryTimescale()
        {
            MethodInfo wait = typeof(TimeScaledUtilities).GetMethod(nameof(TimeScaledUtilities.WaitForFixedSeconds));
            Type iterator = wait.GetCustomAttribute<IteratorStateMachineAttribute>().StateMachineType;
            MethodInfo moveNext = iterator.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic);
            string[] reads = OriginalILCallSequence.Names(OriginalILCallSequence.Calls(moveNext))
                .Where(name => name == "Time.get_fixedDeltaTime" || name == "TimeManager.GetTimescale").ToArray();
            Assert.That(reads, Is.EqualTo(new[] { "Time.get_fixedDeltaTime", "TimeManager.GetTimescale" }));
        }

        // Player.LoadPhotoForSize local function 0x060007af (x86-64 0x1ae5940)
        // stores the texture only when the captured request hash (+0x10)
        // equals the event hash (cmp/jne at 0x1ae5944). The genuine shipping
        // stub raises the matching photo and completion synchronously; a
        // foreign-hash null photo raised from inside that completion dispatch,
        // before the iterator's own completion handler, must not replace the
        // matching photo. Dropping the hash comparison reports null instead.
        [Test]
        public void ForeignPhotoHashLeavesTheMatchingPhotoInPlace()
        {
            // The constructor subscribes to AppInitialise and AppShutdown; own those rows first.
            using (new VerificationFixtureOwnership.ActionRows(SystemAction.AppInitialise, SystemAction.AppShutdown))
            {
                GameCenterLocalPlayer local = ShippingGameCenterObjects.CreateLocalPlayer();
                var holder = (IGameCenterLocalPlayerReferencesHolder)local;
                var listener = (GameCenterLocalPlayerListenerStub)holder.GameCenterLocalPlayerListener;
                bool raising = false;
                int foreignRaised = 0;
                Action<int, bool> raiseForeign = (hash, success) =>
                {
                    if (raising) return;
                    raising = true;
                    try { listener.TriggerLoadPhotoForSizeEvent(hash ^ 1, null); ++foreignRaised; }
                    finally { raising = false; }
                };
                listener.OnLoadPhotoForSizeCompleted += raiseForeign;
                try
                {
                    Assert.That(Player.TryParseProperties("lucid-owned|Owned", "lucid-owned", holder, out Player player), Is.True);
                    int callbacks = 0;
                    bool reportedSuccess = false;
                    Texture2D reportedPhoto = null;
                    var routine = player.LoadPhotoForSize(PhotoSize.PhotoSizeNormal, (success, photo) =>
                    {
                        ++callbacks;
                        reportedSuccess = success;
                        reportedPhoto = photo;
                    });
                    Assert.That(routine.MoveNext(), Is.False, "the shipping stub completes without a yield");
                    Assert.That(foreignRaised, Is.EqualTo(1));
                    Assert.That(callbacks, Is.EqualTo(1));
                    Assert.That(reportedSuccess, Is.True);
                    Assert.That(ReferenceEquals(reportedPhoto, null), Is.False, "the foreign null photo was not stored");
                    Assert.That(reportedPhoto == Texture2D.whiteTexture, Is.True, "the stub's matching photo is reported");
                }
                finally
                {
                    listener.OnLoadPhotoForSizeCompleted -= raiseForeign;
                    ProcessManager.UnsubscribeFromAllActions(local);
                }
            }
        }

        // RuntimeConfiguration.Assets 0x06000e5b (x86-64 0x1b15640) tail-calls
        // Array.AsReadOnly (0x1b15679) on the raw field with no null test.
        // Unity gives a created instance an empty serialized array, so the
        // owned instance's field is cleared to reach the original null fault.
        // A null-coalescing getter fails here.
        [Test]
        public void NullConfigurationArrayFaultsInsteadOfReadingAsEmpty()
        {
            var configuration = ScriptableObject.CreateInstance<RuntimeConfiguration>();
            try
            {
                FieldInfo field = typeof(RuntimeConfiguration).GetField("m_configurationAssets", BindingFlags.Instance | BindingFlags.NonPublic);
                field.SetValue(configuration, null);
                Assert.Throws<ArgumentNullException>(() => { var unused = configuration.Assets; });
                field.SetValue(configuration, new SystemConfigurationAsset[0]);
                Assert.That(configuration.Assets.Count, Is.Zero);
                Assert.That(configuration.Assets, Is.Not.SameAs(configuration.Assets), "each read wraps the array anew");
            }
            finally { UnityEngine.Object.DestroyImmediate(configuration); }
        }
    }
}
