using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

// LocalisedUIString.UpdateString 0x06000033 (x86-64 0x1a35960) and
// GetLocalisedText 0x06000034 (x86-64 0x1a35d40) with no StringTable instance.
// StringTable.GetStringAndNumArgs (x86-64 0x1a35df0) then fails and writes
// String.Empty (0x1a35fd1) for every identifier, NONE included.
public sealed class OriginalLocalisedUIStringLookupFallbackTests
{
    // GetLocalisedText 0x1a35daa..0x1a35dd0: failed lookup, identifier not
    // 0xb6fa1284 (NONE) and IsNullOrEmpty(content) select "NO STRING_ID SET".
    // UpdateString 0x1a3598d tests only for null before calling the lookup, so
    // an empty override is written unchanged. Catches dropping the NONE guard,
    // falling back on an empty override, or returning the raw empty lookup.
    [Test]
    public void FailedLookupWritesMissingMarkerOnlyForNonNoneIdentifierAndNullOverride()
    {
        Assert.IsTrue(StringTable.IsNull(), "Fixture requires no live StringTable instance");
        FastAction handlers = StringTable.StringsTableHandlers;
        GameObject owned = null;
        try
        {
            owned = new GameObject("Owned localisation lookup fallback", typeof(RectTransform));
            owned.SetActive(false);
            Text text = owned.AddComponent<Text>();
            LocalisedUIString localised = owned.AddComponent<LocalisedUIString>();
            localised.FindTextTargets();
            Assert.IsFalse(localised.isActiveAndEnabled);

            // The default identifier is the zero value, not NONE.
            Assert.AreNotEqual(Strings.NONE, localised.Identifier);
            text.text = "owned initial value";
            localised.UpdateString();
            Assert.AreEqual("NO STRING_ID SET", text.text);

            text.text = "owned initial value";
            localised.UpdateString(null);
            Assert.AreEqual("NO STRING_ID SET", text.text);

            localised.UpdateString(string.Empty);
            Assert.AreEqual(string.Empty, text.text);

            // NONE skips the marker: the empty missing-string result is written.
            localised.Identifier = Strings.NONE;
            text.text = "owned initial value";
            localised.UpdateString();
            Assert.AreEqual(string.Empty, text.text);

            text.text = "owned initial value";
            localised.UpdateString("explicit override");
            Assert.AreEqual("explicit override", text.text);

            Assert.IsTrue(StringTable.IsNull());
            Assert.IsFalse(owned.activeInHierarchy);
            Assert.IsTrue(ReferenceEquals(handlers, StringTable.StringsTableHandlers));
        }
        finally
        {
            if (owned != null) UnityEngine.Object.DestroyImmediate(owned);
        }
        Assert.IsTrue(ReferenceEquals(handlers, StringTable.StringsTableHandlers));
        // No lifecycle callback, string-table subscription, TMP target or table
        // content runs. The successful-lookup path needs a live StringTable.
    }
}
