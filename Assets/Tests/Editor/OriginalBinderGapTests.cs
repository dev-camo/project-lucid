using System;
using System.Collections.Generic;
using Hardlight.UI.Binding.Nodes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // Owned inactive binder nodes only; no listener registration or lifecycle runs.
    // The genuine member lookup cache may retain the carrier type's metadata.
    public sealed class OriginalBinderGapTests
    {
        // StringFormatter.Refresh (0x06000165, x86-64 0x1a08370): BindReference.GetValue on
        // Arg1..Arg4 (fields +0x60..+0x78) precedes GetValue<string> on String (+0x58) at
        // 0x1a085bf, and only a null result is replaced by String.Empty (0x1a085c7).
        // Fails if the format is read first, or if the null-coalesce is dropped
        // (String.Format(null, ...) throws).
        [Test]
        public void ArgumentsAreReadInOrderBeforeTheFormatAndOnlyANullFormatBecomesEmpty()
        {
            Assert.IsFalse(Application.isPlaying, "Owned case requires the Editor (uncached) binding path");
            GameObject owner = null;
            OriginalStringFormatterOrderCarrier carrier = null;
            try
            {
                owner = new GameObject("Owned inactive StringFormatter order case");
                owner.SetActive(false);
                StringFormatter formatter = owner.AddComponent<StringFormatter>();
                carrier = ScriptableObject.CreateInstance<OriginalStringFormatterOrderCarrier>();
                carrier.Format = carrier.Recording("String", "{0}|{1}|{2}|{3}");
                carrier.First = carrier.Recording("Arg1", "a");
                carrier.Second = carrier.Recording("Arg2", "b");
                carrier.Third = carrier.Recording("Arg3", "c");
                carrier.Fourth = carrier.Recording("Arg4", "d");
                formatter.String.SetTarget(carrier, "Format");
                formatter.Arg1.SetTarget(carrier, "First");
                formatter.Arg2.SetTarget(carrier, "Second");
                formatter.Arg3.SetTarget(carrier, "Third");
                formatter.Arg4.SetTarget(carrier, "Fourth");
                string[] order = { "Arg1", "Arg2", "Arg3", "Arg4", "String" };

                carrier.Reads.Clear();
                formatter.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order));
                Assert.That(formatter.Output.Value, Is.EqualTo("a|b|c|d"));

                carrier.Format = carrier.Recording("String", null);
                carrier.Reads.Clear();
                formatter.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order), "a null format is still read after every argument");
                Assert.That(formatter.Output.Value, Is.EqualTo(string.Empty));

                carrier.Format = carrier.Recording("String", "plain");
                carrier.Reads.Clear();
                formatter.Refresh();
                Assert.That(carrier.Reads, Is.EqualTo(order));
                Assert.That(formatter.Output.Value, Is.EqualTo("plain"), "a non-null format is used as supplied");
                Assert.IsFalse(owner.activeInHierarchy);
            }
            finally
            {
                try
                {
                    if (owner != null) Object.DestroyImmediate(owner);
                }
                finally
                {
                    if (carrier != null) Object.DestroyImmediate(carrier);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
