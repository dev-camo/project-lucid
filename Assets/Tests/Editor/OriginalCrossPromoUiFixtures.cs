using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Shared reflection access and in-memory collaborators for the HLCrossPromo UI tests. Nothing
    // here downloads or opens a URL: the collaborators only record calls and hand back sprites
    // the fixture created.
    internal static class CrossPromoUiAccess
    {
        private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static FieldInfo Field(Type type, string name)
        {
            for (Type at = type; at != null; at = at.BaseType)
            {
                FieldInfo field = at.GetField(name, Declared);
                if (field != null) return field;
            }
            throw new AssertionException("Missing field " + type + ":" + name);
        }

        public static object Get(object instance, string name) => Field(instance.GetType(), name).GetValue(instance);

        public static void Set(object instance, string name, object value) => Field(instance.GetType(), name).SetValue(instance, value);

        public static object Call(Type declaring, object instance, string name, params object[] args)
        {
            MethodInfo method = null;
            foreach (MethodInfo candidate in declaring.GetMethods(Declared))
                if (candidate.Name == name && candidate.GetParameters().Length == args.Length)
                {
                    if (method != null) throw new AssertionException("Ambiguous method " + declaring + ":" + name);
                    method = candidate;
                }
            if (method == null) throw new AssertionException("Missing method " + declaring + ":" + name);
            try
            {
                return method.Invoke(instance, args);
            }
            catch (TargetInvocationException failure)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        public static string Describe(Sprite sprite) => sprite == null ? "null" : sprite.name;

        public static CrossPromoAd Ad(string name) => new CrossPromoAd
        {
            Name = name,
            IconTexture = new CrossPromoTexture { URL = "icon-" + name },
            FullScreenTexture = new CrossPromoTexture { URL = "full-" + name },
            StoreURL = "store-" + name
        };

        public static CrossPromoAd[] Ads(params string[] names)
        {
            var ads = new CrossPromoAd[names.Length];
            for (int i = 0; i < names.Length; i++) ads[i] = Ad(names[i]);
            return ads;
        }

        // The real system over in-memory collaborators. Its constructor (0x1A0F570) subscribes to
        // the resource manager, sets m_syncInProgress at 0x1A0F6DF and asks for metadata, which
        // the recorder never completes. The sync therefore stays in progress, so the explicit
        // ICrossPromo.Initialised add (0x1A0E6B0) subscribes the handler instead of calling it
        // back at once.
        public static CrossPromo NewCrossPromo(RecordingResourceManager resources) =>
            new CrossPromo(default(CrossPromoConfig), resources, new RejectingDataParser());

        // A member's custom attributes in metadata order, as "TypeName(ctor args)".
        public static string[] AttributeRows(MemberInfo member) =>
            member.GetCustomAttributesData()
                .Select(a => a.AttributeType.Name + "(" + string.Join(",", a.ConstructorArguments.Select(arg => arg.Value)) + ")")
                .ToArray();

        public static Delegate[] Handlers(object owner, string eventField)
        {
            var handler = (Delegate)Field(owner.GetType(), eventField).GetValue(owner);
            return handler == null ? new Delegate[0] : handler.GetInvocationList();
        }
    }

    // Owns every engine object a test creates; sprites are named so call logs can show them.
    internal sealed class CrossPromoUiObjects : IDisposable
    {
        private readonly List<Object> owned = new List<Object>();
        private Texture2D texture;

        public GameObject Root { get; }

        public CrossPromoUiObjects(string name)
        {
            Root = new GameObject(name, typeof(RectTransform));
            Root.SetActive(false);
            owned.Add(Root);
        }

        public GameObject Child(string name, Transform parent = null, params Type[] components)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent != null ? parent : Root.transform, false);
            foreach (Type component in components) child.AddComponent(component);
            return child;
        }

        public Sprite Sprite(string name, float width = 10f, float height = 6f)
        {
            if (texture == null)
            {
                texture = new Texture2D(16, 16);
                owned.Add(texture);
            }
            Sprite sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero);
            sprite.name = name;
            owned.Add(sprite);
            return sprite;
        }

        public void Dispose()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }
    }

    // Test-only ICrossPromo for the full screen view, which only talks to the interface. Calls
    // are appended to a log the fixture shares with its pages. By default Initialised handlers
    // are kept whatever Ready says, so a test can raise them; CallBackOnAddWhenReady instead
    // mirrors CrossPromo's explicit add (0x1A0E6B0), which, when ready, invokes the new handler
    // with the current metadata and does not keep it.
    internal sealed class RecordingCrossPromo : ICrossPromo
    {
        public readonly List<string> Log;
        public bool Ready;
        public bool CallBackOnAddWhenReady;
        public CrossPromoMetaData MetaData;
        public Func<CrossPromoTexture, Sprite> FullScreenSprites = texture => null;
        public Action<CrossPromoMetaData> InitialisedHandlers;

        public RecordingCrossPromo(List<string> log)
        {
            Log = log;
        }

        event Action<CrossPromoMetaData> ICrossPromo.Initialised
        {
            add
            {
                Log.Add("add_Initialised");
                if (CallBackOnAddWhenReady && Ready)
                {
                    value?.Invoke(MetaData);
                    return;
                }
                InitialisedHandlers += value;
            }
            remove { Log.Add("remove_Initialised"); InitialisedHandlers -= value; }
        }

        event Action<CrossPromoMetaData, int> ICrossPromo.FullScreenViewRequested
        {
            add { Log.Add("add_FullScreenViewRequested"); }
            remove { Log.Add("remove_FullScreenViewRequested"); }
        }

        event Action<CrossPromoAd, int> ICrossPromo.LaunchButtonShown
        {
            add { Log.Add("add_LaunchButtonShown"); }
            remove { Log.Add("remove_LaunchButtonShown"); }
        }

        event Action<CrossPromoAd> ICrossPromo.FullScreenViewShown
        {
            add { Log.Add("add_FullScreenViewShown"); }
            remove { Log.Add("remove_FullScreenViewShown"); }
        }

        event Action<CrossPromoAd> ICrossPromo.FullScreenButtonClicked
        {
            add { Log.Add("add_FullScreenButtonClicked"); }
            remove { Log.Add("remove_FullScreenButtonClicked"); }
        }

        event Action ICrossPromo.FullScreenViewClosed
        {
            add { Log.Add("add_FullScreenViewClosed"); }
            remove { Log.Add("remove_FullScreenViewClosed"); }
        }

        public bool IsReady()
        {
            Log.Add("IsReady");
            return Ready;
        }

        public Sprite RequestIconTexture(CrossPromoTexture iconTexture)
        {
            Log.Add("RequestIconTexture(" + iconTexture.URL + ")");
            return null;
        }

        public void ReleaseIconTexture(Sprite texture) => Log.Add("ReleaseIconTexture(" + CrossPromoUiAccess.Describe(texture) + ")");

        public Sprite RequestFullScreenTexture(CrossPromoTexture fullScreenTexture)
        {
            Log.Add("RequestFullScreenTexture(" + fullScreenTexture.URL + ")");
            return FullScreenSprites(fullScreenTexture);
        }

        public void ReleaseFullScreenTexture(Sprite texture) => Log.Add("ReleaseFullScreenTexture(" + CrossPromoUiAccess.Describe(texture) + ")");

        public void RequestFullScreenView(int currentAd) => Log.Add("RequestFullScreenView(" + currentAd + ")");

        public void RaiseImpressionEvent(CrossPromoAd ad, int impressionCount) => Log.Add("RaiseImpressionEvent(" + ad.Name + "," + impressionCount + ")");

        public void RaiseFullScreenClosedEvent() => Log.Add("RaiseFullScreenClosedEvent");

        public void RaiseFullScreenImpression(CrossPromoAd ad) => Log.Add("RaiseFullScreenImpression(" + ad.Name + ")");

        public void RaiseFullScreenButtonClicked(CrossPromoAd ad) => Log.Add("RaiseFullScreenButtonClicked(" + ad.Name + ")");
    }

    // In-memory resource manager behind a real CrossPromo; it never downloads.
    internal sealed class RecordingResourceManager : ICrossPromoResourceManager
    {
        public readonly List<string> Log = new List<string>();
        public Func<CrossPromoTexture, Sprite> IconSprites = texture => null;

        public event Action<bool> DownloadCompleted
        {
            add { Log.Add("add_DownloadCompleted"); }
            remove { Log.Add("remove_DownloadCompleted"); }
        }

        public bool HasPendingDownloads => false;

        public void DownloadMetaData(Action<bool> onMetaDataDownloadCompleted) => Log.Add("DownloadMetaData");

        public void VerifyAdResources(CrossPromoAd[] metaDataAds) => Log.Add("VerifyAdResources");

        public Sprite RequestFullScreenTexture(CrossPromoTexture texMeta)
        {
            Log.Add("RequestFullScreenTexture(" + texMeta.URL + ")");
            return null;
        }

        public void ReleaseFullScreenTexture(Sprite texture) => Log.Add("ReleaseFullScreenTexture(" + CrossPromoUiAccess.Describe(texture) + ")");

        public Sprite RequestIconTexture(CrossPromoTexture texMeta)
        {
            Log.Add("RequestIconTexture(" + texMeta.URL + ")");
            return IconSprites(texMeta);
        }

        public void ReleaseIconTexture(Sprite texture) => Log.Add("ReleaseIconTexture(" + CrossPromoUiAccess.Describe(texture) + ")");

        public string RequestMetaData() => null;
    }

    internal sealed class RejectingDataParser : ICrossPromoDataParser
    {
        public bool TryParseMetadata(Dictionary<string, Dictionary<string, object>> dataRoot, out CrossPromoMetaData metaData)
        {
            metaData = default(CrossPromoMetaData);
            return false;
        }
    }
}
