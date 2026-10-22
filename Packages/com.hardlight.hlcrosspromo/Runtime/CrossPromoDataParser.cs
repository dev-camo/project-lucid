using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000008; bodies translated from the 1.10.1 x86-64 binary.
	// Reads the deserialised cross-promotion JSON: a version router picks the metadata root for the
	// app version, which lists the ads by name. A key suffixed with "-<platform>" wins over the bare key.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CrossPromoDataParser : ICrossPromoDataParser
	{
		private const string PlatformSuffix = "";

		private readonly CrossPromoConfig m_config;

		private readonly string m_platformName;

		// 0600003e (1.10.1 RVA 0x1A0F4A0): a blank platform name adds no suffix.
		public CrossPromoDataParser(CrossPromoConfig config)
		{
			m_config = config;
			m_platformName = string.IsNullOrWhiteSpace(m_config.PlatformName) ? PlatformSuffix : "-" + m_config.PlatformName;
		}

		// 0600003f (1.10.1 RVA 0x1A106D0)
		private bool TryFindProperty(Dictionary<string, object> dic, string propertyName, out object prop)
		{
			prop = null;
			if (propertyName == null)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: PropertyName is null. Can not search the dictionary.");
				return false;
			}
			if (dic.TryGetValue(propertyName + m_platformName, out prop))
			{
				return true;
			}
			return dic.TryGetValue(propertyName, out prop);
		}

		// 06000040 (1.10.1 RVA 0x1A107D0): only the text "1" is true; a missing or empty value
		// returns defaultValue.
		private bool GetPropertyAsBool(Dictionary<string, object> dic, string propertyName, bool defaultValue = false)
		{
			string value = GetPropertyAsString(dic, propertyName);
			if (string.IsNullOrEmpty(value))
			{
				return defaultValue;
			}
			return value == "1";
		}

		// 06000041 (1.10.1 RVA 0x1A108C0)
		private int GetPropertyAsInt(Dictionary<string, object> dic, string propertyName, int defaultValue = 0)
		{
			if (TryFindProperty(dic, propertyName, out object prop))
			{
				return Convert.ToInt32(prop);
			}
			return defaultValue;
		}

		// 06000042 (1.10.1 RVA 0x1A10880)
		private string GetPropertyAsString(Dictionary<string, object> dic, string propertyName)
		{
			if (TryFindProperty(dic, propertyName, out object prop))
			{
				return prop.ToString();
			}
			return null;
		}

		// 06000043 (1.10.1 RVA 0x8F8670 for reference types): the value must be a JToken (or null).
		private TValue GetPropertyAs<TValue>(Dictionary<string, object> dic, string propertyName)
		{
			if (TryFindProperty(dic, propertyName, out object prop))
			{
				return ((JToken)prop).ToObject<TValue>();
			}
			return default;
		}

		// 06000044 (1.10.1 RVA 0x1A10950)
		private List<object> GetPropertyAsObjList(Dictionary<string, object> dic, string propertyName)
		{
			return GetPropertyAs<List<object>>(dic, propertyName);
		}

		// 06000045 (1.10.1 RVA 0x1A109B0)
		private Dictionary<string, object> GetPropertyAsDic(Dictionary<string, object> dic, string propertyName)
		{
			return GetPropertyAs<Dictionary<string, object>>(dic, propertyName);
		}

		// 06000046 (1.10.1 RVA 0x1A10A10): metaData is stored, with no ads yet, before the Ads array is
		// read, so a missing array still leaves the system flag, first ad and text behind. Ads that are
		// missing or fail to parse are skipped.
		public bool TryParseMetadata(Dictionary<string, Dictionary<string, object>> dataRoot, out CrossPromoMetaData metaData)
		{
			metaData = default;
			if (dataRoot == null)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: Invalid data root.");
				return false;
			}
			if (!dataRoot.TryGetValue(m_config.RouterPropertyName, out Dictionary<string, object> versionRouter))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing " + m_config.RouterPropertyName + ".");
				return false;
			}
			string version = ParseVersionRouter(versionRouter);
			if (string.IsNullOrEmpty(version))
			{
				return false;
			}
			if (!versionRouter.TryGetValue(version, out object metaRootName))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing metaRoot: " + version + ".");
				return false;
			}
			if (!dataRoot.TryGetValue(metaRootName.ToString(), out Dictionary<string, object> metaRoot))
			{
				HLOutput.LogError(m_config.Debug, string.Format("CrossPromoDataParser: missing metaRoot: {0}.", metaRootName));
				return false;
			}
			metaData = new CrossPromoMetaData
			{
				IsSystemEnabled = GetPropertyAsBool(metaRoot, "SystemEnabled"),
				FirstAd = GetPropertyAsString(metaRoot, "FirstAd"),
				PressToPlayString = FindPressToPlayString(dataRoot, m_config.LanguageId)
			};
			string[] adNames = GetPropertyAs<string[]>(metaRoot, "Ads");
			if (adNames == null)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing Ads array in metaRoot " + version + ".");
				return false;
			}
			List<CrossPromoAd> ads = new List<CrossPromoAd>(adNames.Length);
			foreach (string adName in adNames)
			{
				if (!dataRoot.TryGetValue(adName, out Dictionary<string, object> adData))
				{
					HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing ad " + adName + " in metaRoot " + version + ".");
				}
				else if (TryParseAdData(adName, adData, out CrossPromoAd ad))
				{
					ads.Add(ad);
				}
			}
			metaData.Ads = ads.ToArray();
			return true;
		}

		// 06000047 (1.10.1 RVA 0x1A11140): a key equal to the app version wins at once; otherwise the
		// highest key below the app version, or null when every key is above it.
		private string ParseVersionRouter(Dictionary<string, object> vRouter)
		{
			string result = null;
			Version bestVersion = null;
			foreach (string key in vRouter.Keys)
			{
				Version version = new Version(key);
				int comparison = version.CompareTo(m_config.AppVersion);
				if (comparison == 0)
				{
					return key;
				}
				if (comparison < 0 && (bestVersion == null || version.CompareTo(bestVersion) > 0))
				{
					bestVersion = version;
					result = key;
				}
			}
			return result;
		}

		// 06000048 (1.10.1 RVA 0x1A11490): promoAd carries the name from the start; a disabled ad
		// fails without a report.
		private bool TryParseAdData(string adName, Dictionary<string, object> adData, out CrossPromoAd promoAd)
		{
			promoAd = new CrossPromoAd
			{
				Name = adName
			};
			if (!GetPropertyAsBool(adData, "enabled"))
			{
				return false;
			}
			promoAd.StoreURL = GetPropertyAsString(adData, "storelink");
			if (string.IsNullOrEmpty(promoAd.StoreURL))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing storelink property in ad " + adName + ".");
				return false;
			}
			Dictionary<string, object> iconImage = GetPropertyAsDic(adData, "iconimage");
			if (iconImage == null)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing iconimage in ad " + adName + ".");
				return false;
			}
			if (!TryParseTexture(iconImage, out promoAd.IconTexture))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: no iconimage found in ad " + adName + ".");
				return false;
			}
			List<object> mainImages = GetPropertyAsObjList(adData, "mainimages");
			if (mainImages == null)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing mainimages in ad " + adName + ".");
				return false;
			}
			if (mainImages.Count == 0 || !TryParseMainImage(mainImages, out promoAd.FullScreenTexture))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: no mainimages found in ad " + adName + ".");
				return false;
			}
			return true;
		}

		// 06000049 (1.10.1 RVA 0x1A11810): width and height are both read before either is checked.
		private bool TryParseTexture(Dictionary<string, object> texData, out CrossPromoTexture texture)
		{
			texture = default;
			int width = GetPropertyAsInt(texData, "width");
			int height = GetPropertyAsInt(texData, "height");
			if (width == 0 || height == 0)
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser : ad is missing width and height information");
				return false;
			}
			string url = GetPropertyAsString(texData, "url");
			if (string.IsNullOrEmpty(url))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser : ad is missing url");
				return false;
			}
			texture = new CrossPromoTexture(url, new Vector2Int(width, height));
			return true;
		}

		// 0600004a (1.10.1 RVA 0x1A11A90): an image of exactly the screen size wins at once. Otherwise
		// the image whose height/width ratio (rounded to two places) is closest to the screen's wins;
		// among equal ratios a later image replaces the kept one only when its height is strictly
		// closer to the screen height. Images that fail to parse are skipped without a report.
		private bool TryParseMainImage(List<object> mainImages, out CrossPromoTexture mainImage)
		{
			mainImage = default;
			int screenWidth = Screen.width;
			int screenHeight = Screen.height;
			float screenRatio = (float)Math.Round(screenHeight / (float)screenWidth, 2);
			float bestRatio = 0f;
			float bestHeight = 0f;
			foreach (object image in mainImages)
			{
				if (!TryParseTexture(((JToken)image).ToObject<Dictionary<string, object>>(), out CrossPromoTexture texture))
				{
					continue;
				}
				float ratio = (float)Math.Round(texture.Resolution.y / (float)texture.Resolution.x, 2);
				if (ratio == screenRatio && texture.Resolution.x == screenWidth && screenHeight == texture.Resolution.y)
				{
					mainImage = texture;
					return true;
				}
				// Native 0x1A11E29/0x1A11E2B: the inlined Mathf.Abs(int) (init flag 0x319EAD6, set at 0x1A11D9E) is
				// negl/cmovs, so int.MinValue stays negative where Mono's checked abs would throw. The native
				// computes it inside the third disjunct; hoisting it here is only safe because it is pure.
				int heightDifference = screenHeight - texture.Resolution.y;
				int absoluteHeightDifference = unchecked(heightDifference < 0 ? -heightDifference : heightDifference);
				if (bestRatio == 0f || Math.Abs(screenRatio - bestRatio) > Math.Abs(screenRatio - ratio) ||
					(bestRatio == ratio && Math.Abs(screenHeight - bestHeight) > absoluteHeightDifference))
				{
					mainImage = texture;
					bestRatio = ratio;
					bestHeight = texture.Resolution.y;
				}
			}
			return bestRatio != 0f;
		}

		// 0600004b (1.10.1 RVA 0x1A11310): a null language reads "default", and an empty localised
		// text falls back to "default".
		private string FindPressToPlayString(Dictionary<string, Dictionary<string, object>> metaData, string languageID)
		{
			if (!metaData.TryGetValue("PressToPlayString", out Dictionary<string, object> strings))
			{
				HLOutput.LogError(m_config.Debug, "CrossPromoDataParser: missing PressToPlayString localised strings");
				return null;
			}
			string language = languageID ?? "default";
			string text = GetPropertyAsString(strings, language);
			if (string.IsNullOrEmpty(text) && language != "default")
			{
				text = GetPropertyAsString(strings, "default");
			}
			return text;
		}
	}
}
