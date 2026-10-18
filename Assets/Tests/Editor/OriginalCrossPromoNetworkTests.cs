using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests.Editor
{
	// Behaviour of the restored HLCrossPromo download and resource managers (1.10.1 x86-64).
	// No request is ever sent: coroutine handlers only record the iterators they are given, so
	// no MoveNext (the only code that builds a UnityWebRequest) runs. File work stays inside a
	// fresh temporary folder that each test deletes; every Unity object created here is destroyed.
	public sealed class OriginalCrossPromoNetworkTests
	{
		private const string IncomingFolder = "incoming";
		private const string DestinationFolder = "grid";
		private const string MetaFilename = "settings.json";

		// ---------------------------------------------------------------- CrossPromoDownloadHandler

		[Test]
		public void DownloadHandler_ConstructorCreatesIncomingFolderAndStagesUnderTheTargetFileName()
		{
			WithFixture(1024L, fixture =>
			{
				// .ctor 0x1A12070: Directory.CreateDirectory(IncomingPath) when missing; the temporary
				// file is Path.Combine(IncomingPath, Path.GetFileName(target)).
				Assert.That(Directory.Exists(fixture.Config.IncomingPath), Is.False);
				string target = Path.Combine(fixture.Root, "targets", "ad.png");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, target, false)))
				{
					Assert.That(Directory.Exists(fixture.Config.IncomingPath), Is.True);
					// ReceiveData 0x1A121C0 opens the stream lazily, writes (data, 0, dataLength), returns true.
					Assert.That(handler.Receive(new byte[] { 1, 2, 3, 99 }, 3), Is.True);
					Assert.That(handler.Receive(new byte[] { 4, 5 }, 2), Is.True);
					// Five bytes stay below the 1024 threshold: CompleteContent closes the stream and
					// leaves the staged file where it was written.
					handler.Complete();
					Assert.That(handler.IsValid, Is.False);
					string staged = Path.Combine(fixture.Config.IncomingPath, "ad.png");
					Assert.That(File.ReadAllBytes(staged), Is.EqualTo(new byte[] { 1, 2, 3, 4, 5 }));
					Assert.That(File.Exists(target), Is.False);
				}
			});
		}

		[Test]
		public void DownloadHandler_CompleteContentNeedsMoreBytesThanTheThresholdAndAStream()
		{
			// CompleteContent 0x1A12380: IsValid = IsNullOrEmpty(error) && stream?.Length > threshold
			// (setg against config+0x48, lifted to false without a stream). Threshold 4:
			// 4 bytes -> invalid, 5 bytes -> valid; no data with threshold -1 -> still invalid, which
			// separates the lifted comparison from (Length ?? 0) > threshold.
			WithFixture(4L, fixture =>
			{
				Directory.CreateDirectory(fixture.Config.DestinationPath);
				string equal = Path.Combine(fixture.Config.DestinationPath, "equal.png");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, equal, false)))
				{
					Assert.That(handler.Receive(new byte[4], 4), Is.True);
					handler.Complete();
					Assert.That(handler.IsValid, Is.False);
					Assert.That(File.Exists(equal), Is.False);
					string staged = Path.Combine(fixture.Config.IncomingPath, "equal.png");
					Assert.That(File.ReadAllBytes(staged).Length, Is.EqualTo(4), "invalid content stays staged");
					// Dispose 0x1A124A0 deletes the staged file after the base dispose.
					handler.Dispose();
					Assert.That(File.Exists(staged), Is.False);
				}

				string above = Path.Combine(fixture.Config.DestinationPath, "above.png");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, above, false)))
				{
					Assert.That(handler.Receive(new byte[] { 9, 8, 7, 6, 5 }, 5), Is.True);
					handler.Complete();
					Assert.That(handler.IsValid, Is.True);
					Assert.That(File.ReadAllBytes(above), Is.EqualTo(new byte[] { 9, 8, 7, 6, 5 }));
					Assert.That(File.Exists(Path.Combine(fixture.Config.IncomingPath, "above.png")), Is.False);
				}
			});
			WithFixture(-1L, fixture =>
			{
				string empty = Path.Combine(fixture.Root, "empty.png");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, empty, false)))
				{
					handler.Complete();
					Assert.That(handler.IsValid, Is.False);
					Assert.That(File.Exists(empty), Is.False);
				}
			});
		}

		[Test]
		public void DownloadHandler_ExistingTargetIsReplacedOnlyWhenDuplicatesAreDeleted()
		{
			WithFixture(0L, fixture =>
			{
				string replaced = Path.Combine(fixture.Root, "replaced.json");
				File.WriteAllText(replaced, "old");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, replaced, true)))
				{
					Assert.That(handler.Receive(new byte[] { (byte)'n', (byte)'e', (byte)'w' }, 3), Is.True);
					handler.Complete();
					Assert.That(handler.IsValid, Is.True);
					Assert.That(File.ReadAllText(replaced), Is.EqualTo("new"));
				}

				// Without the flag CompleteContent goes straight to File.Move, which refuses an
				// existing destination: the exception escapes and the old target survives.
				string kept = Path.Combine(fixture.Root, "kept.json");
				File.WriteAllText(kept, "old");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, kept, false)))
				{
					Assert.That(handler.Receive(new byte[] { (byte)'n', (byte)'e', (byte)'w' }, 3), Is.True);
					Assert.Throws<IOException>(handler.Complete);
					Assert.That(handler.IsValid, Is.True);
					Assert.That(File.ReadAllText(kept), Is.EqualTo("old"));
					string staged = Path.Combine(fixture.Config.IncomingPath, "kept.json");
					Assert.That(File.ReadAllText(staged), Is.EqualTo("new"));
					handler.Dispose();
					Assert.That(File.Exists(staged), Is.False);
				}
			});
		}

		[Test]
		public void DownloadHandler_OpenFailureLogsReturnsFalseAndRetriesOnTheNextChunk()
		{
			WithFixture(1024L, fixture =>
			{
				string target = Path.Combine(fixture.Root, "ad.png");
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, target, false)))
				{
					// A directory where the staged file belongs makes File.OpenWrite throw.
					string staged = Path.Combine(fixture.Config.IncomingPath, "ad.png");
					Directory.CreateDirectory(staged);
					using (var capture = new OwnedHLOutputErrorCapture())
					{
						// ReceiveData 0x1A121C0, landing pad 0x1A12229: catch Exception, String.Format(
						// "{0}: Failed to open a FileStream with error: {1}", "CrossPromoDownloadHandler",
						// exception), HLOutput.LogError (0x1A12334), return false; m_fileStream stays null.
						bool received = true;
						Assert.DoesNotThrow(() => received = handler.Receive(new byte[] { 1 }, 1));
						Assert.That(received, Is.False);
						Assert.That(capture.Errors.Count, Is.EqualTo(1));
						Assert.That(capture.Errors[0],
							Does.StartWith("CrossPromoDownloadHandler: Failed to open a FileStream with error: ")
								.And.EndWith("|0"));
					}
					Directory.Delete(staged);

					// The next chunk opens the stream again; the failed chunk was never written.
					Assert.That(handler.Receive(new byte[] { 2, 3 }, 2), Is.True);
					handler.Complete();
					Assert.That(handler.IsValid, Is.False);
					Assert.That(File.ReadAllBytes(staged), Is.EqualTo(new byte[] { 2, 3 }));
				}
			});
		}

		[Test]
		public void DownloadHandler_ErrorReportsTheServerMessageWhileTheTransportErrorIsEmpty()
		{
			WithFixture(0L, fixture =>
			{
				// get_Error 0x1A11FF0: IsNullOrEmpty(error) selects "Message from server: " + text,
				// otherwise "Download error: " + error. A detached handler has no transport error.
				using (var handler = fixture.Own(new ProbeDownloadHandler(fixture.Config, Path.Combine(fixture.Root, "e.bin"), false)))
				{
					Assert.That(handler.Error, Does.StartWith("Message from server: "));
				}
			});
		}

		// ---------------------------------------------------------------- CrossPromoDownloadManager

		[Test]
		public void DownloadManager_StartsOneCoroutinePerLocalPathAndKeepsItPending()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoDownloadManager(fixture.Config, coroutines);
				Action<bool> callback = _ => { };
				Assert.That(manager.HasPendingDownloads, Is.False);

				// StartDownload 0x1A12520: List.Contains(localFilePath) returns early, else Add then
				// StartCoroutine(DownloadFile(...)).
				manager.StartDownload("memory://a", "path-a", true, callback);
				Assert.That(manager.HasPendingDownloads, Is.True);
				manager.StartDownload("memory://b", "path-a", false, null);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1), "same local path is ignored");
				manager.StartDownload("memory://a", "path-b", false, null);
				Assert.That(coroutines.Started.Count, Is.EqualTo(2), "the uri is not the key");

				// DownloadFile 0x1A126A0 only captures its arguments; nothing runs until MoveNext.
				IEnumerator first = coroutines.Started[0];
				Assert.That(first.GetType().Name, Is.EqualTo("<DownloadFile>d__7"));
				Assert.That(first.GetType().DeclaringType, Is.SameAs(typeof(CrossPromoDownloadManager)));
				Assert.That(Hoisted<string>(first, "uri"), Is.EqualTo("memory://a"));
				Assert.That(Hoisted<string>(first, "localFilePath"), Is.EqualTo("path-a"));
				Assert.That(Hoisted<bool>(first, "deleteDuplicateBeforeMoving"), Is.True);
				Assert.That(Hoisted<Action<bool>>(first, "downloadCompleteCallback"), Is.SameAs(callback));
				Assert.That(Hoisted<object>(first, "<>4__this"), Is.SameAs(manager));
				Assert.That(Hoisted<string>(coroutines.Started[1], "localFilePath"), Is.EqualTo("path-b"));
				// Only the finished request removes its path, so both stay pending.
				Assert.That(manager.HasPendingDownloads, Is.True);
			});
		}

		[Test]
		public void DownloadManagerCacheOnly_MetadataAndSpritesShareOneIdentifierClaim()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoDownloadManagerCacheOnly(fixture.Config, coroutines);
				Action<string> onMeta = _ => { };
				Action<bool> onComplete = _ => { };
				Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType> onData = (_, __, ___) => { };
				Assert.That(manager.HasPendingDownloads, Is.False);

				// CanStartDownload 0x1A12F00 claims the identifier for either kind of download.
				manager.StartDownloadMetaData("memory://meta", "shared-id", onMeta, onComplete);
				manager.StartDownloadSprite("memory://sprite", "shared-id", CrossPromoResourceManagerCacheOnly.SpriteType.Icon, onData);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
				manager.StartDownloadSprite("memory://sprite", "other-id", CrossPromoResourceManagerCacheOnly.SpriteType.Main, onData);
				Assert.That(coroutines.Started.Count, Is.EqualTo(2));
				Assert.That(manager.HasPendingDownloads, Is.True);

				IEnumerator meta = coroutines.Started[0];
				Assert.That(meta.GetType().Name, Is.EqualTo("<DownloadFile>d__9"));
				Assert.That(Hoisted<string>(meta, "uri"), Is.EqualTo("memory://meta"));
				Assert.That(Hoisted<string>(meta, "localFilePath"), Is.EqualTo("shared-id"));
				Assert.That(Hoisted<Action<string>>(meta, "onMetaDataReceived"), Is.SameAs(onMeta));
				Assert.That(Hoisted<Action<bool>>(meta, "onDownloadComplete"), Is.SameAs(onComplete));

				IEnumerator sprite = coroutines.Started[1];
				Assert.That(sprite.GetType().Name, Is.EqualTo("<DownloadFile>d__10"));
				Assert.That(Hoisted<string>(sprite, "localFilePath"), Is.EqualTo("other-id"));
				Assert.That(Hoisted<CrossPromoResourceManagerCacheOnly.SpriteType>(sprite, "spriteType"),
					Is.EqualTo(CrossPromoResourceManagerCacheOnly.SpriteType.Main));
				Assert.That(Hoisted<object>(sprite, "onDataReceived"), Is.SameAs(onData));
			});
		}

		[Test]
		public void DownloadManagerCacheOnly_FileDownloadIsNotImplementedAndClaimsNothing()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoDownloadManagerCacheOnly(fixture.Config, coroutines);
				// StartDownload 0x1A13370 throws NotImplementedException before any other work.
				Assert.Throws<NotImplementedException>(() => manager.StartDownload("memory://x", "id", true, _ => { }));
				Assert.That(manager.HasPendingDownloads, Is.False);
				manager.StartDownloadMetaData("memory://x", "id", null, null);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
			});
		}

		// ---------------------------------------------------------------- CrossPromoResourceManager

		[Test]
		public void ResourceManager_ConstructorKeepsDestinationFilesAndRecreatesAnEmptyIncomingFolder()
		{
			WithFixture(0L, fixture =>
			{
				// .ctor 0x1A140D0: create DestinationPath when missing; delete IncomingPath recursively
				// when present; then always create IncomingPath.
				Directory.CreateDirectory(Path.Combine(fixture.Config.IncomingPath, "nested"));
				File.WriteAllText(Path.Combine(fixture.Config.IncomingPath, "nested", "stale.tmp"), "x");
				Directory.CreateDirectory(fixture.Config.DestinationPath);
				string kept = Path.Combine(fixture.Config.DestinationPath, "keep.png");
				File.WriteAllText(kept, "keep");
				new CrossPromoResourceManager(fixture.Config, new RecordingDownloadManager(), new RecordingPool(), new RecordingPool());
				Assert.That(File.ReadAllText(kept), Is.EqualTo("keep"));
				Assert.That(Directory.Exists(fixture.Config.IncomingPath), Is.True);
				Assert.That(Directory.GetFileSystemEntries(fixture.Config.IncomingPath), Is.Empty);
			});
			WithFixture(0L, fixture =>
			{
				Assert.That(Directory.Exists(fixture.Config.DestinationPath), Is.False);
				// .ctor 0x1A0F380 builds its own pools and delegates to the four-argument constructor.
				new CrossPromoResourceManager(fixture.Config, new RecordingDownloadManager());
				Assert.That(Directory.Exists(fixture.Config.DestinationPath), Is.True);
				Assert.That(Directory.Exists(fixture.Config.IncomingPath), Is.True);
			});
		}

		[Test]
		public void ResourceManager_MetaDataDownloadsOverTheMetaFileAndReadsItBack()
		{
			WithFixture(0L, fixture =>
			{
				var downloads = new RecordingDownloadManager();
				var manager = new CrossPromoResourceManager(fixture.Config, downloads, new RecordingPool(), new RecordingPool());
				Action<bool> callback = _ => { };
				// DownloadMetaData 0x1A141E0: StartDownload(MetaDataURL, MetaFilePath, true, callback).
				manager.DownloadMetaData(callback);
				Assert.That(downloads.Calls.Count, Is.EqualTo(1));
				downloads.AssertCall(0, fixture.Config.MetaDataURL, fixture.Config.MetaFilePath, true, callback);
				// RequestMetaData 0x1A142B0: File.Exists ? File.ReadAllText : null.
				Assert.That(manager.RequestMetaData(), Is.Null);
				File.WriteAllText(fixture.Config.MetaFilePath, "{\"ads\":[]}");
				Assert.That(manager.RequestMetaData(), Is.EqualTo("{\"ads\":[]}"));
			});
		}

		[Test]
		public void ResourceManager_VerifyAdResourcesDownloadsMissingImagesAndPrunesUnnamedFiles()
		{
			WithFixture(0L, fixture =>
			{
				var downloads = new RecordingDownloadManager();
				var manager = new CrossPromoResourceManager(fixture.Config, downloads, new RecordingPool(), new RecordingPool());
				string grid = fixture.Config.DestinationPath;
				File.WriteAllText(fixture.Config.MetaFilePath, "meta");
				File.WriteAllText(Path.Combine(grid, "icon-a.png"), "a");
				File.WriteAllText(Path.Combine(grid, "full-b.png"), "b");
				File.WriteAllText(Path.Combine(grid, "stale.png"), "s");
				Action<bool> subscriber = _ => { };
				manager.DownloadCompleted += subscriber;

				var ads = new[]
				{
					Ad("icon-a.png", "full-a.png"),
					Ad("icon-b.png", "full-b.png"),
					Ad("icon-c.png", "full-c.png"),
				};
				manager.VerifyAdResources(ads);

				// Icon then full screen per ad (0x1A14400 passes the ad's +0x08 IconTexture before its
				// +0x20 FullScreenTexture; ad c has both missing); present files are skipped; each
				// download goes to Path.Combine(grid, LocalFileName) without duplicate deletion and with
				// the event's current delegate (0x1A14690).
				Assert.That(downloads.Calls.Count, Is.EqualTo(4));
				downloads.AssertCall(0, "memory://full-a.png", Path.Combine(grid, "full-a.png"), false, subscriber);
				downloads.AssertCall(1, "memory://icon-b.png", Path.Combine(grid, "icon-b.png"), false, subscriber);
				downloads.AssertCall(2, "memory://icon-c.png", Path.Combine(grid, "icon-c.png"), false, subscriber);
				downloads.AssertCall(3, "memory://full-c.png", Path.Combine(grid, "full-c.png"), false, subscriber);

				// DeleteObsoleteFiles 0x1A14520 keeps the meta file and any file an ad names as its
				// icon or full-screen image (AdFinder.Find 0x1A14D40 checks both), deleting the rest.
				Assert.That(File.Exists(fixture.Config.MetaFilePath), Is.True);
				Assert.That(File.Exists(Path.Combine(grid, "icon-a.png")), Is.True);
				Assert.That(File.Exists(Path.Combine(grid, "full-b.png")), Is.True);
				Assert.That(File.Exists(Path.Combine(grid, "stale.png")), Is.False);
			});
		}

		[Test]
		public void ResourceManager_TextureRequestsUseThePoolThenTheDeviceThenADownload()
		{
			WithFixture(0L, fixture =>
			{
				var downloads = new RecordingDownloadManager();
				var icons = new RecordingPool();
				var mains = new RecordingPool();
				var manager = new CrossPromoResourceManager(fixture.Config, downloads, icons, mains);
				Sprite pooled = fixture.CreateSprite(1, 1);
				icons.Entries["pooled.png"] = pooled;

				// HandleTextureRequest 0x1A147F0: a pool hit returns at once.
				Assert.That(manager.RequestIconTexture(Tex("pooled.png")), Is.SameAs(pooled));
				Assert.That(icons.Added, Is.Empty);
				Assert.That(downloads.Calls, Is.Empty);

				// A miss loads the device file (LoadTextureFromDevice 0x1A14A80) into a sprite of the
				// decoded size with a zero pivot, then adds it to the requesting pool only.
				File.WriteAllBytes(Path.Combine(fixture.Config.DestinationPath, "device.png"), fixture.EncodePng(3, 2));
				Sprite loaded = fixture.Own(manager.RequestFullScreenTexture(Tex("device.png")));
				Assert.That(loaded, Is.Not.Null);
				fixture.Own(loaded.texture);
				Assert.That(loaded.rect, Is.EqualTo(new Rect(0f, 0f, 3f, 2f)));
				Assert.That(loaded.pivot, Is.EqualTo(Vector2.zero));
				// new Texture2D(2, 2, RGB24, mipChain: false) (0x1A14B33) then the two-argument
				// LoadImage (0x1A14B40, markNonReadable false): readable, one mip level.
				Assert.That(loaded.texture.isReadable, Is.True);
				Assert.That(loaded.texture.mipmapCount, Is.EqualTo(1));
				Assert.That(mains.Added.Select(a => a.Key), Is.EqualTo(new[] { "device.png" }));
				Assert.That(mains.Added[0].Value, Is.SameAs(loaded));
				Assert.That(icons.Added, Is.Empty);
				Assert.That(downloads.Calls, Is.Empty);

				// Neither pooled nor on disk: download with no subscriber passes the null event field.
				Assert.That(manager.RequestIconTexture(Tex("missing.png")), Is.Null);
				Assert.That(downloads.Calls.Count, Is.EqualTo(1));
				downloads.AssertCall(0, "memory://missing.png",
					Path.Combine(fixture.Config.DestinationPath, "missing.png"), false, null);
				Assert.That(icons.Added, Is.Empty);
				Assert.That(icons.TryGetKeys, Is.EqualTo(new[] { "pooled.png", "missing.png" }));
				Assert.That(mains.TryGetKeys, Is.EqualTo(new[] { "device.png" }));
			});
		}

		[Test]
		public void ResourceManager_ReleasesAndPendingStateReachOnlyTheirOwnCollaborator()
		{
			WithFixture(0L, fixture =>
			{
				var downloads = new RecordingDownloadManager();
				var icons = new RecordingPool();
				var mains = new RecordingPool();
				var manager = new CrossPromoResourceManager(fixture.Config, downloads, icons, mains);
				Sprite icon = fixture.CreateSprite(1, 1);
				Sprite main = fixture.CreateSprite(1, 1);
				manager.ReleaseIconTexture(icon);
				manager.ReleaseFullScreenTexture(main);
				Assert.That(icons.Released, Is.EqualTo(new[] { icon }));
				Assert.That(mains.Released, Is.EqualTo(new[] { main }));
				// get_HasPendingDownloads 0x1A14040 forwards to the download manager.
				downloads.Pending = false;
				Assert.That(manager.HasPendingDownloads, Is.False);
				downloads.Pending = true;
				Assert.That(manager.HasPendingDownloads, Is.True);
			});
		}

		// ---------------------------------------------------------------- CrossPromoResourceManagerCacheOnly

		[Test]
		public void CacheOnly_MetaDataArrivesThroughTheMetadataCallbackAndVerifyOnlyRaisesPending()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoResourceManagerCacheOnly(fixture.Config, coroutines);
				Assert.That(manager.HasPendingDownloads, Is.False);
				Assert.That(manager.RequestMetaData(), Is.Null);

				// DownloadMetaData 0x1A15300: StartDownloadMetaData(MetaDataURL, MetaFilePath,
				// OnMetaDataDownloaded, callback); the metadata callback stores the text (0x1A153A0).
				Action<bool> callback = _ => { };
				manager.DownloadMetaData(callback);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
				IEnumerator meta = coroutines.Started[0];
				Assert.That(Hoisted<string>(meta, "uri"), Is.EqualTo(fixture.Config.MetaDataURL));
				Assert.That(Hoisted<string>(meta, "localFilePath"), Is.EqualTo(fixture.Config.MetaFilePath));
				Assert.That(Hoisted<Action<bool>>(meta, "onDownloadComplete"), Is.SameAs(callback));
				Hoisted<Action<string>>(meta, "onMetaDataReceived")("payload");
				Assert.That(manager.RequestMetaData(), Is.EqualTo("payload"));

				// The identifier stays claimed until the request finishes.
				manager.DownloadMetaData(callback);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));

				// VerifyAdResources 0x1A153C0 only sets HasPendingDownloads.
				manager.VerifyAdResources(new[] { Ad("icon.png", "full.png") });
				Assert.That(manager.HasPendingDownloads, Is.True);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
			});
		}

		[Test]
		public void CacheOnly_TextureMissDownloadsOnceThenServesTheDecodedSprite()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoResourceManagerCacheOnly(fixture.Config, coroutines);
				CrossPromoTexture texture = Tex("icon.png");

				// HandleTextureRequest 0x1A15640: a pool miss starts a sprite download keyed by the
				// local file name and returns null; a repeat request is refused by the claim.
				Assert.That(manager.RequestIconTexture(texture), Is.Null);
				Assert.That(manager.RequestIconTexture(texture), Is.Null);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
				IEnumerator download = coroutines.Started[0];
				Assert.That(Hoisted<string>(download, "uri"), Is.EqualTo("memory://icon.png"));
				Assert.That(Hoisted<string>(download, "localFilePath"), Is.EqualTo("icon.png"));
				Assert.That(Hoisted<CrossPromoResourceManagerCacheOnly.SpriteType>(download, "spriteType"),
					Is.EqualTo(CrossPromoResourceManagerCacheOnly.SpriteType.Icon));
				Assert.That(manager.HasPendingDownloads, Is.False, "only the data callback refreshes it");

				var completed = new List<bool>();
				Action<bool> subscriber = completed.Add;
				manager.DownloadCompleted += subscriber;
				var onData = Hoisted<Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType>>(download, "onDataReceived");
				onData(fixture.EncodePng(3, 2), "icon.png", CrossPromoResourceManagerCacheOnly.SpriteType.Icon);
				manager.DownloadCompleted -= subscriber;

				// OnTextureDownloaded 0x1A15960: sprite into the Icon pool, pending copied from the
				// download manager (its claim is still held, so true), DownloadCompleted(true).
				Assert.That(completed, Is.EqualTo(new[] { true }));
				Assert.That(manager.HasPendingDownloads, Is.True);
				Sprite served = fixture.Own(manager.RequestIconTexture(texture));
				Assert.That(served, Is.Not.Null);
				fixture.Own(served.texture);
				Assert.That(served.rect, Is.EqualTo(new Rect(0f, 0f, 3f, 2f)));
				Assert.That(served.pivot, Is.EqualTo(Vector2.zero));
				// Same texture creation as the device path (0x1A159E7, 0x1A159F4).
				Assert.That(served.texture.isReadable, Is.True);
				Assert.That(served.texture.mipmapCount, Is.EqualTo(1));
				Assert.That(manager.RequestIconTexture(texture), Is.SameAs(served));
				// The Main pool is separate, and the shared claim blocks a second download.
				Assert.That(manager.RequestFullScreenTexture(texture), Is.Null);
				Assert.That(coroutines.Started.Count, Is.EqualTo(1));
			});
		}

		[Test]
		public void CacheOnly_TextureDownloadCopiesPendingFromTheDownloadManager()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoResourceManagerCacheOnly(fixture.Config, coroutines);
				Assert.That(manager.RequestIconTexture(Tex("icon.png")), Is.Null);
				manager.VerifyAdResources(new[] { Ad("icon.png", "full.png") });
				Assert.That(manager.HasPendingDownloads, Is.True);

				// Release the test-owned download manager's claim so its Count is 0.
				var downloadManager = (CrossPromoDownloadManagerCacheOnly)PrivateField(manager, "m_downloadManager");
				((List<string>)PrivateField(downloadManager, "m_currentlyDownloading")).Clear();
				Assert.That(downloadManager.HasPendingDownloads, Is.False);

				// OnTextureDownloaded 0x1A15B1C-0x1A15B44: setg of m_currentlyDownloading.Count > 0
				// into the HasPendingDownloads backing field, so true becomes false.
				var onData = Hoisted<Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType>>(coroutines.Started.Single(), "onDataReceived");
				onData(fixture.EncodePng(2, 2), "icon.png", CrossPromoResourceManagerCacheOnly.SpriteType.Icon);
				Assert.That(manager.HasPendingDownloads, Is.False);
				Sprite served = fixture.Own(manager.RequestIconTexture(Tex("icon.png")));
				Assert.That(served, Is.Not.Null);
				fixture.Own(served.texture);
			});
		}

		[Test]
		public void CacheOnly_DefaultCompletionHandlerAcceptsAnUnsubscribedNotification()
		{
			WithFixture(0L, fixture =>
			{
				var coroutines = new RecordingCoroutineHandler();
				var manager = new CrossPromoResourceManagerCacheOnly(fixture.Config, coroutines);
				// .ctor 0x1A14FC5-0x1A15056 stores the cached <>c.<>9__13_0 (<.ctor>b__13_0) into
				// DownloadCompleted (+0x10) before any pool is built.
				var initial = (Action<bool>)PrivateField(manager, "DownloadCompleted");
				Assert.That(initial, Is.Not.Null);
				Assert.That(initial.GetInvocationList().Length, Is.EqualTo(1));
				Assert.That(initial.Method.Name, Is.EqualTo("<.ctor>b__13_0"));
				Assert.That(initial.Method.DeclaringType.Name, Is.EqualTo("<>c"));
				Assert.That(initial.Method.DeclaringType.DeclaringType, Is.SameAs(typeof(CrossPromoResourceManagerCacheOnly)));
				Assert.That(manager.RequestFullScreenTexture(Tex("full.png")), Is.Null);
				IEnumerator download = coroutines.Started.Single();
				Assert.That(Hoisted<CrossPromoResourceManagerCacheOnly.SpriteType>(download, "spriteType"),
					Is.EqualTo(CrossPromoResourceManagerCacheOnly.SpriteType.Main));
				// The field initializer installs an empty handler (<>c.<.ctor>b__13_0), so the
				// unchecked DownloadCompleted(true) in OnTextureDownloaded has a target.
				var onData = Hoisted<Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType>>(download, "onDataReceived");
				Assert.DoesNotThrow(() => onData(fixture.EncodePng(2, 2), "full.png", CrossPromoResourceManagerCacheOnly.SpriteType.Main));
				Sprite served = fixture.Own(manager.RequestFullScreenTexture(Tex("full.png")));
				Assert.That(served, Is.Not.Null);
				fixture.Own(served.texture);
			});
		}

		// ---------------------------------------------------------------- helpers

		private static CrossPromoTexture Tex(string localFileName)
		{
			return new CrossPromoTexture { URL = "memory://" + localFileName, LocalFileName = localFileName };
		}

		private static CrossPromoAd Ad(string icon, string fullScreen)
		{
			return new CrossPromoAd { Name = icon, IconTexture = Tex(icon), FullScreenTexture = Tex(fullScreen) };
		}

		private static object PrivateField(object owner, string field)
		{
			FieldInfo info = owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(info, Is.Not.Null, field);
			return info.GetValue(owner);
		}

		private static T Hoisted<T>(IEnumerator iterator, string field)
		{
			FieldInfo info = iterator.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.That(info, Is.Not.Null, field);
			return (T)info.GetValue(iterator);
		}

		private static void WithFixture(long threshold, Action<Fixture> body)
		{
			var fixture = new Fixture(threshold);
			try
			{
				body(fixture);
			}
			catch (Exception bodyFailure)
			{
				// Keep the body's failure first; a cleanup failure only adds context.
				try
				{
					fixture.Dispose();
				}
				catch (Exception cleanupFailure)
				{
					TestContext.Error.WriteLine("CrossPromo fixture cleanup also failed: " + cleanupFailure);
				}
				throw;
			}
			fixture.Dispose();
		}

		private sealed class Fixture : IDisposable
		{
			private readonly List<Object> m_objects = new List<Object>();
			private readonly List<IDisposable> m_disposables = new List<IDisposable>();

			public readonly string Root;
			public readonly CrossPromoConfig Config;

			public Fixture(long threshold)
			{
				Root = Path.Combine(Path.GetTempPath(), "ProjectLucidCrossPromoDownloadAndResource_" + Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(Root);
				var asset = Own(ScriptableObject.CreateInstance<CrossPromoConfigurationAsset>());
				using (var serialized = new SerializedObject(asset))
				{
					serialized.FindProperty("m_incomingFolder").stringValue = IncomingFolder;
					serialized.FindProperty("m_destinationFolder").stringValue = DestinationFolder;
					serialized.FindProperty("m_metaFilename").stringValue = MetaFilename;
					serialized.FindProperty("m_invalidContentLengthThreshold").longValue = threshold;
					serialized.ApplyModifiedPropertiesWithoutUndo();
				}
				Config = new CrossPromoConfig(asset, "1.10.1", "GB", "en", Root);
			}

			public T Own<T>(T value) where T : Object
			{
				if (value != null) m_objects.Add(value);
				return value;
			}

			public ProbeDownloadHandler Own(ProbeDownloadHandler handler)
			{
				m_disposables.Add(handler);
				return handler;
			}

			public Sprite CreateSprite(int width, int height)
			{
				var texture = Own(new Texture2D(width, height, TextureFormat.RGBA32, false));
				return Own(Sprite.Create(texture, new Rect(0f, 0f, width, height), Vector2.zero));
			}

			public byte[] EncodePng(int width, int height)
			{
				var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
				try
				{
					texture.SetPixels(Enumerable.Repeat(Color.magenta, width * height).ToArray());
					texture.Apply();
					return texture.EncodeToPNG();
				}
				finally
				{
					Object.DestroyImmediate(texture);
				}
			}

			public void Dispose()
			{
				var failures = new List<Exception>();
				foreach (IDisposable disposable in m_disposables)
				{
					try { disposable.Dispose(); }
					catch (Exception failure) { failures.Add(failure); }
				}
				for (int i = m_objects.Count - 1; i >= 0; i--)
				{
					if (m_objects[i] != null) Object.DestroyImmediate(m_objects[i]);
				}
				try { Directory.Delete(Root, true); }
				catch (Exception failure) { failures.Add(failure); }
				if (failures.Count != 0) throw new AggregateException("CrossPromo fixture cleanup failed.", failures);
			}
		}

		// Exposes the protected DownloadHandlerScript overrides; the instance is never attached
		// to a request.
		private sealed class ProbeDownloadHandler : CrossPromoDownloadHandler
		{
			public ProbeDownloadHandler(CrossPromoConfig config, string targetFilePath, bool deleteDuplicateBeforeMoving)
				: base(config, targetFilePath, deleteDuplicateBeforeMoving)
			{
			}

			public bool Receive(byte[] data, int dataLength) => ReceiveData(data, dataLength);

			public void Complete() => CompleteContent();
		}

		private sealed class RecordingCoroutineHandler : ICoroutineHandler
		{
			public readonly List<IEnumerator> Started = new List<IEnumerator>();

			public Coroutine StartCoroutine(IEnumerator downloadFile)
			{
				Started.Add(downloadFile);
				return null;
			}
		}

		private sealed class RecordingDownloadManager : ICrossPromoDownloadManager
		{
			public readonly List<(string Uri, string Path, bool Delete, Action<bool> Callback)> Calls =
				new List<(string, string, bool, Action<bool>)>();

			public bool Pending;

			public bool HasPendingDownloads => Pending;

			public void StartDownload(string uri, string filePath, bool deleteDuplicateBeforeMoving, Action<bool> metaDataDownloadCallback)
			{
				Calls.Add((uri, filePath, deleteDuplicateBeforeMoving, metaDataDownloadCallback));
			}

			public void AssertCall(int index, string uri, string path, bool delete, Action<bool> callback)
			{
				Assert.That(Calls[index].Uri, Is.EqualTo(uri), "uri " + index);
				Assert.That(Calls[index].Path, Is.EqualTo(path), "path " + index);
				Assert.That(Calls[index].Delete, Is.EqualTo(delete), "delete " + index);
				Assert.That(Calls[index].Callback, Is.SameAs(callback), "callback " + index);
			}
		}

		private sealed class RecordingPool : ISharedResourcePool<string, Sprite>
		{
			public readonly Dictionary<string, Sprite> Entries = new Dictionary<string, Sprite>();
			public readonly List<string> TryGetKeys = new List<string>();
			public readonly List<KeyValuePair<string, Sprite>> Added = new List<KeyValuePair<string, Sprite>>();
			public readonly List<Sprite> Released = new List<Sprite>();

			public bool TryGet(string key, out Sprite resource)
			{
				TryGetKeys.Add(key);
				return Entries.TryGetValue(key, out resource);
			}

			public void Add(string key, Sprite resource)
			{
				Added.Add(new KeyValuePair<string, Sprite>(key, resource));
				Entries[key] = resource;
			}

			public void ReleaseResource(Sprite texture)
			{
				Released.Add(texture);
			}
		}
	}
}
