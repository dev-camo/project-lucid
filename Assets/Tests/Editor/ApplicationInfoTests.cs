using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Hardlight.Enums;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // ApplicationInfo (Game.Runtime 020000ad, bodies 0x534F50..0x5352C0) and ApplicationInfoView
    // (0200082c, 0x606880..0x606A40) against the 1.10.1 x86-64 bodies cited per case.
    //
    // Not covered: WriteToFile (it writes into the project's StreamingAssets folder), the file-present
    // branch of LoadOrCreateDefault (it needs that file and a UnityWebRequest pump) and
    // ApplicationInfoView.OnEnable (it starts a coroutine on the shared coroutine runner).
    public sealed class ApplicationInfoTests
    {
        // The class is [Serializable] with two public fields; native ToJson (call at 0x535010) writes them in
        // declaration order.
        [Test]
        public void TheStampSerialisesItsCodeThenItsTime()
        {
            string json = JsonUtility.ToJson(new ApplicationInfo { BuildCode = "b-77", BuildTime = 132223104000000000L });
            Assert.That(json, Is.EqualTo("{\"BuildCode\":\"b-77\",\"BuildTime\":132223104000000000}"));
        }

        // CreateDefault 0x535170 stores the "n/a" literal and DateTime.Now.ToFileTimeUtc() (0x5351D2..0x5351E1).
        [Test]
        public void TheDefaultStampIsNaAtTheCurrentFileTime()
        {
            long before = DateTime.Now.ToFileTimeUtc();
            ApplicationInfo info = ApplicationInfo.CreateDefault();
            long after = DateTime.Now.ToFileTimeUtc();
            Assert.That(info.BuildCode, Is.EqualTo("n/a"));
            Assert.That(info.BuildTime, Is.InRange(before, after));
        }

        // MoveNext 0x535480: without the file (File.Exists, 0x53554F) the default goes straight to the
        // callback and the iterator finishes without yielding; a null callback is skipped (0x5355D5).
        // A prepared project holds the original's ApplicationInfo.json in StreamingAssets, so the case points the
        // class's location (built by the class initialiser, 0x5352C0) at a path that does not exist for its duration
        // and puts the original path back afterwards; the project's files are never touched.
        [Test]
        public void WithoutAFileTheDefaultReachesTheCallbackAtOnce()
        {
            // Reflection: the location is a private static readonly string with no public route to a different file.
            FieldInfo location = typeof(ApplicationInfo).GetField("Location", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            object original = location.GetValue(null);
            string missing = Path.Combine(Path.GetTempPath(), "lucid-" + Guid.NewGuid().ToString("N"), "ApplicationInfo.json");
            try
            {
                location.SetValue(null, missing);
                Assert.That(File.Exists(missing), Is.False, "set-up: no ApplicationInfo.json");
                ApplicationInfo delivered = null;
                int calls = 0;
                var iterator = ApplicationInfo.LoadOrCreateDefault(info => { delivered = info; calls++; });
                Assert.That(iterator.MoveNext(), Is.False);
                Assert.That(calls, Is.EqualTo(1));
                Assert.That(delivered.BuildCode, Is.EqualTo("n/a"));

                Assert.That(ApplicationInfo.LoadOrCreateDefault(null).MoveNext(), Is.False);
            }
            finally
            {
                location.SetValue(null, original);
            }
        }

        // Constructor 0x606A40 builds both text bindables with "..." and the time bindable empty.
        [Test]
        public void TheViewStartsWithPlaceholders()
        {
            Owned.Run(owned =>
            {
                var view = owned.Host("Lucid application info view", false).AddComponent<ApplicationInfoView>();
                Assert.That(view.BuildVersion.Value, Is.EqualTo("..."));
                Assert.That(view.BuildCode.Value, Is.EqualTo("..."));
                Assert.That(view.BuildTime.Value, Is.EqualTo(default(DateTime)));
            });
        }

        // OnApplicationInfoLoaded 0x606920: version = String.Concat(StringTable.GetString(prefix),
        // Application.version) (0x6069B9), the code is copied, and the time is DateTime.FromFileTimeUtc of the
        // stored value (0x606A11), so its kind is Utc. The prefix comes from an owned table.
        [Test]
        public void LoadedInformationFillsTheThreeBindables()
        {
            Owned.Run(owned =>
            {
                OwnedStringTable.Run(new Dictionary<Strings, string> { { Strings.MISSING, "v:" } }, () =>
                {
                    var view = owned.Host("Lucid application info view", false).AddComponent<ApplicationInfoView>();
                    Set(view, "m_versionPrefix", Strings.MISSING);
                    Call(view, "OnApplicationInfoLoaded", new ApplicationInfo { BuildCode = "abc123", BuildTime = 132223104000000000L });
                    Assert.That(view.BuildVersion.Value, Is.EqualTo("v:" + Application.version));
                    Assert.That(view.BuildCode.Value, Is.EqualTo("abc123"));
                    Assert.That(view.BuildTime.Value, Is.EqualTo(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
                    Assert.That(view.BuildTime.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
                });
            });
        }
    }
}
