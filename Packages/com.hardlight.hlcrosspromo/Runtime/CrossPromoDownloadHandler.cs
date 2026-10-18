using System;
using System.IO;
using Unity.IL2CPP.CompilerServices;
using UnityEngine.Networking;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000009; bodies translated from the 1.10.1 x86-64 binary.
	// Streams a download into the incoming folder and moves it to its target only once it is valid.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CrossPromoDownloadHandler : DownloadHandlerScript
	{
		private readonly CrossPromoConfig m_config;

		private readonly string m_tmpFilePath;

		private readonly string m_targetFilePath;

		private readonly bool m_deleteDuplicateBeforeMoving;

		private Stream m_fileStream;

		// 0600004c (1.10.1 RVA 0x1A11FD0), 0600004d (1.10.1 RVA 0x1A11FE0)
		public bool IsValid { get; private set; }

		// 0600004e (1.10.1 RVA 0x1A11FF0)
		public string Error => string.IsNullOrEmpty(error) ? "Message from server: " + text : "Download error: " + error;

		// 0600004f (1.10.1 RVA 0x1A12070): base buffer of 0x1000 bytes; the temporary file keeps the target's file name.
		public CrossPromoDownloadHandler(CrossPromoConfig config, string targetFilePath, bool deleteDuplicateBeforeMoving)
			: base(new byte[4096])
		{
			m_config = config;
			m_targetFilePath = targetFilePath;
			m_deleteDuplicateBeforeMoving = deleteDuplicateBeforeMoving;
			m_tmpFilePath = Path.Combine(m_config.IncomingPath, Path.GetFileName(targetFilePath));
			if (!Directory.Exists(m_config.IncomingPath))
			{
				Directory.CreateDirectory(m_config.IncomingPath);
			}
		}

		// 06000050 (1.10.1 RVA 0x1A121A0)
		[Obsolete]
		protected override void ReceiveContentLength(int contentLength)
		{
			base.ReceiveContentLength(contentLength);
		}

		// 06000051 (1.10.1 RVA 0x1A121B0)
		protected override float GetProgress()
		{
			return base.GetProgress();
		}

		// 06000052 (1.10.1 RVA 0x1A121C0): the stream opens lazily on the first chunk; either failure logs and stops the download.
		protected override bool ReceiveData(byte[] data, int dataLength)
		{
			if (m_fileStream == null)
			{
				try
				{
					m_fileStream = File.OpenWrite(m_tmpFilePath);
				}
				catch (Exception exception)
				{
					HLOutput.LogError(string.Format("{0}: Failed to open a FileStream with error: {1}", nameof(CrossPromoDownloadHandler), exception));
					return false;
				}
			}
			try
			{
				m_fileStream.Write(data, 0, dataLength);
			}
			catch (Exception exception)
			{
				HLOutput.LogError(string.Format("{0}: Failed to write to a FileStream with error: {1}", nameof(CrossPromoDownloadHandler), exception));
				return false;
			}
			return true;
		}

		// 06000053 (1.10.1 RVA 0x1A12380): valid only without an error, with an open stream and more
		// bytes than the threshold (lifted comparison).
		protected override void CompleteContent()
		{
			IsValid = string.IsNullOrEmpty(error) && m_fileStream?.Length > m_config.InvalidContentLengthThreshold;
			m_fileStream?.Close();
			if (IsValid)
			{
				if (m_deleteDuplicateBeforeMoving && File.Exists(m_targetFilePath))
				{
					File.Delete(m_targetFilePath);
				}
				File.Move(m_tmpFilePath, m_targetFilePath);
			}
		}

		// 06000054 (1.10.1 RVA 0x1A124A0)
		public override void Dispose()
		{
			base.Dispose();
			if (File.Exists(m_tmpFilePath))
			{
				File.Delete(m_tmpFilePath);
			}
		}
	}
}
