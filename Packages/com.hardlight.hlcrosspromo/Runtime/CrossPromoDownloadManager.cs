using System;
using System.Collections;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Networking;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 0200000a; bodies translated from the 1.10.1 x86-64 binary.
	// Downloads each local file at most once at a time, through a CrossPromoDownloadHandler.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromoDownloadManager : ICrossPromoDownloadManager
	{
		private readonly CrossPromoConfig m_config;

		private readonly ICoroutineHandler m_coroutineHandler;

		private readonly List<string> m_currentlyDownloading = new List<string>();

		// 06000055 (1.10.1 RVA 0x1A124E0)
		public bool HasPendingDownloads => m_currentlyDownloading.Count > 0;

		// 06000056 (1.10.1 RVA 0x1A0F2A0)
		public CrossPromoDownloadManager(CrossPromoConfig config, ICoroutineHandler coroutineHandler)
		{
			m_config = config;
			m_coroutineHandler = coroutineHandler;
		}

		// 06000057 (1.10.1 RVA 0x1A12520): a path already downloading is ignored, callback included.
		public void StartDownload(string uri, string localFilePath, bool deleteDuplicateBeforeMoving, Action<bool> downloadCompleteCallback)
		{
			if (m_currentlyDownloading.Contains(localFilePath))
			{
				return;
			}
			m_currentlyDownloading.Add(localFilePath);
			m_coroutineHandler.StartCoroutine(DownloadFile(uri, localFilePath, deleteDuplicateBeforeMoving, downloadCompleteCallback));
		}

		// 06000058 (1.10.1 RVA 0x1A126A0); <DownloadFile>d__7 MoveNext 0600005b (1.10.1 RVA 0x1A12840)
		private IEnumerator DownloadFile(string uri, string localFilePath, bool deleteDuplicateBeforeMoving, Action<bool> downloadCompleteCallback)
		{
			using (UnityWebRequest www = new UnityWebRequest(uri))
			{
				CrossPromoDownloadHandler crossPromoDownloader = new CrossPromoDownloadHandler(m_config, localFilePath, deleteDuplicateBeforeMoving);
				www.downloadHandler = crossPromoDownloader;
				yield return www.SendWebRequest();
				m_currentlyDownloading.Remove(localFilePath);
				downloadCompleteCallback?.Invoke(crossPromoDownloader.IsValid);
			}
		}
	}
}
