using NUnit.Framework;
using HardlightProject;
using UnityEngine;
namespace ProjectLucid.Editor.Tests
{
 public sealed class OriginalHashedGroupConfigurationPreservationTests
 {
  // Emission-regression check of the project's own compile, not original evidence.
  // Its declaration facts are pinned to native metadata 0x02000420/0x06001859 (see
  // HashedGroupConfigurationCurrentFacts).
  [Test]
  public void CurrentBuildEmissionMatchesRecordedSnapshot()=>ProjectLucid.Editor.HashedGroupConfigurationCurrentFacts.RequireWholeCurrentEmission();
  // CreateInstance proves the original empty ctor runs. Name, hideFlags and
  // destruction checks are engine behavior only.
  [Test]
  public void GenuineOwnedCreationAndDestruction()
  {
   HashedGroupConfiguration owned=null;
   try
   {
    owned=ScriptableObject.CreateInstance<HashedGroupConfiguration>();
    Assert.That(owned!=null,Is.True,"genuine engine returned a live owned instance");
    Assert.That(owned.GetType(),Is.EqualTo(typeof(HashedGroupConfiguration)));
    owned.name="Hashed330-Owned-Single";owned.hideFlags=HideFlags.HideAndDontSave;
    Assert.That(owned.name,Is.EqualTo("Hashed330-Owned-Single"));
    Assert.That(owned.hideFlags,Is.EqualTo(HideFlags.HideAndDontSave));
    UnityEngine.Object.DestroyImmediate(owned);
    Assert.That(owned==null,Is.True,"the exact owned engine object is destroyed");
   }
   finally
   {
    if(!ReferenceEquals(owned,null)&&owned!=null)UnityEngine.Object.DestroyImmediate(owned);
   }
  }
  [Test]
  public void TwoGenuineOwnedInstancesKeepIndependentEngineState()
  {
   HashedGroupConfiguration first=null,second=null;
   try
   {
    first=ScriptableObject.CreateInstance<HashedGroupConfiguration>();
    Assert.That(first!=null,Is.True);
    second=ScriptableObject.CreateInstance<HashedGroupConfiguration>();
    Assert.That(second!=null,Is.True);
    Assert.That(ReferenceEquals(first,second),Is.False);
    Assert.That(first==second,Is.False);
    Assert.That(first.GetType(),Is.EqualTo(typeof(HashedGroupConfiguration)));
    Assert.That(second.GetType(),Is.EqualTo(typeof(HashedGroupConfiguration)));
    first.name="Hashed330-Owned-First";second.name="Hashed330-Owned-Second";
    first.hideFlags=HideFlags.HideAndDontSave;second.hideFlags=HideFlags.HideInHierarchy;
    Assert.That(first.name,Is.EqualTo("Hashed330-Owned-First"));
    Assert.That(second.name,Is.EqualTo("Hashed330-Owned-Second"));
    Assert.That(first.hideFlags,Is.EqualTo(HideFlags.HideAndDontSave));
    Assert.That(second.hideFlags,Is.EqualTo(HideFlags.HideInHierarchy));
    UnityEngine.Object.DestroyImmediate(first);
    Assert.That(first==null,Is.True);
    Assert.That(second!=null,Is.True,"destroying first preserves the exact second owned object");
    Assert.That(second.name,Is.EqualTo("Hashed330-Owned-Second"));
    Assert.That(second.hideFlags,Is.EqualTo(HideFlags.HideInHierarchy));
   }
   finally
   {
    try{if(!ReferenceEquals(second,null)&&second!=null)UnityEngine.Object.DestroyImmediate(second);}
    finally{if(!ReferenceEquals(first,null)&&first!=null)UnityEngine.Object.DestroyImmediate(first);}
   }
  }
 }
}
