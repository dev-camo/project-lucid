using System;
using System.Reflection;
using System.Text.RegularExpressions;
using HardlightProject;
using NUnit.Framework;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace ProjectLucid.Tests
{
    // Behaviour of HardlightProject.AddressableHostRedirector (0x060004c1..c7)
    // against the shipped x86-64 bodies. The constructor chains a delegate
    // onto the global Addressables.InternalIdTransformFunc, so every case
    // restores the value it found and suppresses the finalizer, which would
    // otherwise unchain later on the finalizer thread. Ids are plain strings;
    // nothing is loaded or fetched.
    [TestFixture]
    public sealed class OriginalAddressableHostRedirectorTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static void WithRedirector(Action<AddressableHostRedirector> body)
        {
            Func<IResourceLocation, string> found = Addressables.InternalIdTransformFunc;
            AddressableHostRedirector redirector = null;
            try
            {
                redirector = new AddressableHostRedirector();
                body(redirector);
            }
            finally
            {
                Addressables.InternalIdTransformFunc = found;
                if (redirector != null) GC.SuppressFinalize(redirector);
            }
        }

        static string Edit(AddressableHostRedirector redirector, string internalId) =>
            (string)typeof(AddressableHostRedirector).GetMethod("EditResourceRequest", Instance)
                .Invoke(redirector, new object[] { new ResourceLocationBase("location", internalId, "provider", typeof(object)) });

        static Regex DefaultRegex(AddressableHostRedirector redirector) =>
            (Regex)typeof(AddressableHostRedirector).GetField("m_defaultMatchHostRegEx", Instance).GetValue(redirector);

        // .ctor 0x52bad0: m_targetHost = string.Empty before the base call,
        // then the default Regex (options 9 = IgnoreCase | Compiled, 0x52bb83)
        // and Delegate.Combine onto get_InternalIdTransformFunc. Finalize
        // 0x52bc40 is Delegate.Remove of an equal delegate, then the base.
        [Test]
        public void ConstructionChainsTheIdTransformAndFinalizeUnchainsIt()
        {
            WithRedirector(redirector =>
            {
                Func<IResourceLocation, string> before = Addressables.InternalIdTransformFunc;
                Assert.That(typeof(AddressableHostRedirector).GetField("m_targetHost", Instance).GetValue(redirector), Is.SameAs(string.Empty));
                Assert.That(DefaultRegex(redirector).ToString(), Is.EqualTo("(http|https):\\/\\/([a-z]|[0-9]|-)+:[0-9]{2,5}"));
                Assert.That(DefaultRegex(redirector).Options, Is.EqualTo(RegexOptions.IgnoreCase | RegexOptions.Compiled));
                Assert.That(typeof(AddressableHostRedirector).GetField("m_customMatchHostRegEx", Instance).GetValue(redirector), Is.Null);

                Addressables.InternalIdTransformFunc = null;
                Func<IResourceLocation, string> unrelated = location => "unrelated";
                Addressables.InternalIdTransformFunc = unrelated;
                var second = new AddressableHostRedirector();
                try
                {
                    Delegate[] chain = Addressables.InternalIdTransformFunc.GetInvocationList();
                    Assert.That(chain.Length, Is.EqualTo(2), "combined, not replaced");
                    Assert.That(chain[0], Is.EqualTo(unrelated));
                    Assert.That(chain[1].Target, Is.SameAs(second));
                    Assert.That(chain[1].Method.Name, Is.EqualTo("EditResourceRequest"));

                    typeof(AddressableHostRedirector).GetMethod("Finalize", Instance).Invoke(second, null);
                    Assert.That(Addressables.InternalIdTransformFunc, Is.EqualTo(unrelated), "only its own delegate is removed");
                }
                finally
                {
                    GC.SuppressFinalize(second);
                }
                Assert.That(before.GetInvocationList()[before.GetInvocationList().Length - 1].Target, Is.SameAs(redirector));
            });
        }

        // EditResourceRequest 0x52bdc0: MatchHostRegEx.Replace(InternalId,
        // ReplaceHost). The pattern needs a scheme, a host of letters, digits
        // or hyphens and a 2..5 digit port; IgnoreCase admits upper case.
        [Test]
        public void TheTargetHostReplacesEveryMatchedSchemeHostAndPort()
        {
            WithRedirector(redirector =>
            {
                redirector.SetHostURL("https://cdn.invalid");
                Assert.That(Edit(redirector, "http://127-0-0-1:8080/bundles/a.bundle"), Is.EqualTo("https://cdn.invalid/bundles/a.bundle"));
                Assert.That(Edit(redirector, "HTTPS://LOCAL-HOST:12345/b"), Is.EqualTo("https://cdn.invalid/b"));
                Assert.That(Edit(redirector, "x http://a:80 y http://b:443"), Is.EqualTo("x https://cdn.invalid y https://cdn.invalid"));

                // Negative controls: a dotted host, a one-digit port, another scheme, no port.
                Assert.That(Edit(redirector, "http://example.com:8080/c"), Is.EqualTo("http://example.com:8080/c"));
                Assert.That(Edit(redirector, "http://host:1/c"), Is.EqualTo("http://host:1/c"));
                Assert.That(Edit(redirector, "ftp://host:2121/c"), Is.EqualTo("ftp://host:2121/c"));
                Assert.That(Edit(redirector, "http://host/c"), Is.EqualTo("http://host/c"));
                // {2,5} is unanchored, so a longer port keeps its excess digits.
                Assert.That(Edit(redirector, "http://host:1234567/c"), Is.EqualTo("https://cdn.invalid67/c"));
            });
        }

        // ReplaceHost 0x52bef0: IsNullOrWhiteSpace(m_targetHost) returns the
        // match's ToString (vtable slot 3), so a blank host is a no-op.
        [Test]
        public void ABlankTargetHostKeepsTheMatchedHost()
        {
            WithRedirector(redirector =>
            {
                const string id = "http://host:8080/d";
                Assert.That(Edit(redirector, id), Is.EqualTo(id), "string.Empty from the constructor");
                redirector.SetHostURL(" \t");
                Assert.That(Edit(redirector, id), Is.EqualTo(id));
                redirector.SetHostURL(null);
                Assert.That(Edit(redirector, id), Is.EqualTo(id));
                redirector.SetHostURL("other");
                Assert.That(Edit(redirector, id), Is.EqualTo("other/d"));
            });
        }

        // get_MatchHostRegEx 0x52ba90 returns the custom regex when non-null,
        // else the default; the setter 0x52bab0 stores without validation.
        [Test]
        public void ACustomRegexReplacesTheDefaultUntilItIsCleared()
        {
            WithRedirector(redirector =>
            {
                Regex standard = DefaultRegex(redirector);
                Assert.That(redirector.MatchHostRegEx, Is.SameAs(standard));
                var custom = new Regex("^asset://[a-z]+");
                redirector.MatchHostRegEx = custom;
                Assert.That(redirector.MatchHostRegEx, Is.SameAs(custom));
                redirector.SetHostURL("https://cdn.invalid");
                Assert.That(Edit(redirector, "asset://bundle/e"), Is.EqualTo("https://cdn.invalid/e"));
                Assert.That(Edit(redirector, "http://host:8080/e"), Is.EqualTo("http://host:8080/e"), "the default is not consulted");
                redirector.MatchHostRegEx = null;
                Assert.That(redirector.MatchHostRegEx, Is.SameAs(standard));
                Assert.That(Edit(redirector, "http://host:8080/e"), Is.EqualTo("https://cdn.invalid/e"));
            });
        }
    }
}
