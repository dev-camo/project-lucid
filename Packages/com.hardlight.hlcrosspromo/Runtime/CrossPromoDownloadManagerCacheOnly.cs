using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Networking;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200000c; bodies translated from the 1.10.1 x86-64 binary.
	// Downloads into memory buffers and hands the payloads to callbacks; nothing is written to disk.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CrossPromoDownloadManagerCacheOnly : ICrossPromoDownloadManager
	{
		private readonly ICoroutineHandler m_coroutineHandler;

		private readonly CrossPromoConfig m_config;

		private readonly List<string> m_currentlyDownloading = new List<string>();

		// 06000060 (1.10.1 RVA 0x1A12CF0)
		public bool HasPendingDownloads => m_currentlyDownloading.Count > 0;

		// 06000061 (1.10.1 RVA 0x1A12D30)
		public CrossPromoDownloadManagerCacheOnly(CrossPromoConfig config, ICoroutineHandler coroutineHandler)
		{
			m_config = config;
			m_coroutineHandler = coroutineHandler;
		}

		// 06000062 (1.10.1 RVA 0x1A12E10)
		public void StartDownloadMetaData(string uri, string fileIdentifier, Action<string> onMetaDataReceived, Action<bool> onDownloadComplete)
		{
			if (CanStartDownload(fileIdentifier))
			{
				m_coroutineHandler.StartCoroutine(DownloadFile(uri, fileIdentifier, onMetaDataReceived, onDownloadComplete));
			}
		}

		// 06000063 (1.10.1 RVA 0x1A130A0)
		public void StartDownloadSprite(string uri, string fileIdentifier, CrossPromoResourceManagerCacheOnly.SpriteType spriteType, Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType> onDataReceived)
		{
			if (CanStartDownload(fileIdentifier))
			{
				m_coroutineHandler.StartCoroutine(DownloadFile(uri, fileIdentifier, spriteType, onDataReceived));
			}
		}

		// 06000064 (1.10.1 RVA 0x1A12F00): claims the identifier when it is free.
		private bool CanStartDownload(string fileIdentifier)
		{
			if (m_currentlyDownloading.Contains(fileIdentifier))
			{
				return false;
			}
			m_currentlyDownloading.Add(fileIdentifier);
			return true;
		}

		// 06000065 (1.10.1 RVA 0x1A12FD0); <DownloadFile>d__9 MoveNext 06000073 (1.10.1 RVA 0x1A13920)
		private IEnumerator DownloadFile(string uri, string localFilePath, Action<string> onMetaDataReceived, Action<bool> onDownloadComplete)
		{
			using (UnityWebRequest www = new UnityWebRequest(uri))
			{
				yield return SendWebRequest(uri, localFilePath, www);
				bool succeeded = WebRequestSucceeded(www);
				if (succeeded)
				{
					onMetaDataReceived?.Invoke(www.downloadHandler.text);
				}
				onDownloadComplete?.Invoke(succeeded);
			}
		}

		// 06000066 (1.10.1 RVA 0x1A13190); <DownloadFile>d__10 MoveNext 0600006c (1.10.1 RVA 0x1A13470)
		private IEnumerator DownloadFile(string uri, string localFilePath, CrossPromoResourceManagerCacheOnly.SpriteType spriteType, Action<byte[], string, CrossPromoResourceManagerCacheOnly.SpriteType> onDataReceived)
		{
			using (UnityWebRequest www = new UnityWebRequest(uri))
			{
				yield return SendWebRequest(uri, localFilePath, www);
				if (WebRequestSucceeded(www))
				{
					onDataReceived?.Invoke(www.downloadHandler.data, localFilePath, spriteType);
				}
			}
		}

		// 06000067 (1.10.1 RVA 0x1A13290); <SendWebRequest>d__11 MoveNext 0600007a (1.10.1 RVA 0x1A13D40)
		private IEnumerator SendWebRequest(string uri, string localFilePath, UnityWebRequest www)
		{
			www.downloadHandler = new DownloadHandlerBuffer();
			yield return www.SendWebRequest();
			m_currentlyDownloading.Remove(localFilePath);
			string error = www.downloadHandler.error;
			if (!WebRequestSucceeded(www))
			{
				// Retail evaluates downloadHandler.error and WebRequestSucceeded(www) here and discards
				// both, and uri is never hoisted; a call compiled out of retail (likely [Conditional])
				// consumed them, and what it was cannot be recovered.
			}
		}

		// 06000068 (1.10.1 RVA 0x1A13350)
		private static bool WebRequestSucceeded(UnityWebRequest webRequest)
		{
			return string.IsNullOrWhiteSpace(webRequest.downloadHandler.error);
		}

		// 06000069 (1.10.1 RVA 0x1A13370)
		public void StartDownload(string uri, string filePath, bool deleteDuplicateBeforeMoving, Action<bool> metaDataDownloadCallback)
		{
			throw new NotImplementedException();
		}
	}
}
