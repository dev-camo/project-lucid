// Genuine menu data and metadata contracts.
// Original menu source preserves authored serialization identities and behavior.
// These cases exercise data without constructing pages, managers or the initializer.
using System;
using System.Linq;
using System.Reflection;
using Hardlight;
using NUnit.Framework;
using UnityEngine;

public sealed class OriginalMenuDataContractsTests
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Test]
    public void Menu22TopLevelTypesKeepLiteralNamesAssemblyAndOriginalFlags()
    {
            Expected(typeof(DialogPage), "Hardlight.DialogPage", 1048577);
            Expected(typeof(DialogSystem), "Hardlight.DialogSystem", 1048833);
            Expected(typeof(FadeTransition), "Hardlight.FadeTransition", 1048577);
            Expected(typeof(GuiBackgroundManager), "Hardlight.GuiBackgroundManager", 1048577);
            Expected(typeof(GuiInitializer), "Hardlight.GuiInitializer", 1048577);
            Expected(typeof(GuiPage), "Hardlight.GuiPage", 1048705);
            Expected(typeof(GuiPageContent), "Hardlight.GuiPageContent", 1048705);
            Expected(typeof(GuiPageParameters), "Hardlight.GuiPageParameters", 1056769);
            Expected(typeof(GuiPageRecord<>), "Hardlight.GuiPageRecord`1", 1048577);
            Expected(typeof(GuiPageRegistry), "Hardlight.GuiPageRegistry", 1048833);
            Expected(typeof(GuiPageTransition), "Hardlight.GuiPageTransition", 1048705);
            Expected(typeof(HubPage), "Hardlight.HubPage", 1048577);
            Expected(typeof(HubPageManager), "Hardlight.HubPageManager", 1048577);
            Expected(typeof(MenuPage), "Hardlight.MenuPage", 1048577);
            Expected(typeof(MenuSystem), "Hardlight.MenuSystem", 1048833);
            Expected(typeof(OverlayPage), "Hardlight.OverlayPage", 1048577);
            Expected(typeof(OverlaySystem), "Hardlight.OverlaySystem", 1048833);
            Expected(typeof(PageStackSystem<>), "Hardlight.PageStackSystem`1", 1048577);
            Expected(typeof(SubPage), "Hardlight.SubPage", 1048577);
            Expected(typeof(SubPageContent), "Hardlight.SubPageContent", 1048577);
            Expected(typeof(SubPageRelocator), "Hardlight.SubPageRelocator", 1048577);
            Expected(typeof(UISystemClosureReason), "Hardlight.UISystemClosureReason", 257);
        Assert.IsTrue(typeof(GuiPage).IsAbstract);
        Assert.IsTrue(typeof(GuiPageContent).IsAbstract);
        Assert.IsTrue(typeof(GuiPageTransition).IsAbstract);
        CollectionAssert.AreEqual(new[] { typeof(GuiPage) }, typeof(GuiPageRecord<>).GetGenericArguments()[0].GetGenericParameterConstraints());
        CollectionAssert.AreEqual(new[] { typeof(GuiPage) }, typeof(PageStackSystem<>).GetGenericArguments()[0].GetGenericParameterConstraints());
    }

    [Test]
    public void ParametersKeepOriginalReadonlyFieldAndOrdinaryPublicContracts()
    {
        Type type = typeof(GuiPageParameters);
        Assert.IsTrue(type.IsSerializable);
        CollectionAssert.AreEqual(new[] { typeof(ICloneable) }, type.GetInterfaces());
        FieldInfo[] fields = type.GetFields(Declared);
        Assert.AreEqual(1, fields.Length);
        Assert.AreEqual("Selectable", fields[0].Name);
        Assert.AreEqual(typeof(HLSelectable), fields[0].FieldType);
        Assert.AreEqual(FieldAttributes.Public | FieldAttributes.InitOnly, fields[0].Attributes);
        Assert.AreEqual(2, type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
        Assert.IsNotNull(type.GetConstructor(new[] { typeof(HLSelectable) }));
        MethodInfo restore = type.GetMethod("Restore", Declared);
        Assert.IsTrue(restore.IsVirtual);
        Assert.AreEqual(typeof(bool), restore.ReturnType);
        Assert.AreEqual(0, restore.GetParameters().Length);
        Assert.AreEqual(typeof(object), type.GetMethod("Clone", Declared).ReturnType);
        Assert.AreEqual(typeof(object), type.GetMethod("Equals", Declared).GetParameters().Single().ParameterType);
        Assert.AreEqual(typeof(int), type.GetMethod("GetHashCode", Declared).ReturnType);
    }

    [Test]
    public void ParametersCloneAndEqualityRetainGenuineNullAndExactTypeBehavior()
    {
        var ordinary = new GuiPageParameters();
        var explicitNull = new GuiPageParameters(null);
        Assert.IsTrue(ReferenceEquals(null, ordinary.Selectable));
        Assert.IsTrue(ReferenceEquals(null, explicitNull.Selectable));
        Assert.IsFalse(ordinary.Restore());
        var clone = (GuiPageParameters)ordinary.Clone();
        Assert.AreEqual(typeof(GuiPageParameters), clone.GetType());
        Assert.AreNotSame(ordinary, clone);
        Assert.IsTrue(ReferenceEquals(ordinary.Selectable, clone.Selectable));
        Assert.IsTrue(ordinary.Equals(explicitNull));
        Assert.IsTrue(ordinary.Equals(clone));
        Assert.IsTrue(ordinary.Equals(ordinary));
        Assert.IsFalse(ordinary.Equals(null));
        Assert.IsFalse(ordinary.Equals(new object()));
        Assert.AreEqual(ordinary.GetHashCode(), ordinary.GetHashCode());
        // Original base identity hashes may collide; no equality/hash repair or inequality claim.
    }

    private sealed class DerivedPageParameters : GuiPageParameters { }

    // GuiPageParameters.Equals (x86-64 0x1b445b0) tests isinst GuiPageParameters, then compares
    // obj.GetType() with this.GetType() through Type.op_Equality: a derived instance is never equal to
    // a base instance in either direction, while two instances of the same derived type are.
    [Test]
    public void ParametersEqualityRequiresTheExactRuntimeType()
    {
        var baseParameters = new GuiPageParameters();
        var derived = new DerivedPageParameters();
        Assert.IsFalse(baseParameters.Equals(derived));
        Assert.IsFalse(derived.Equals(baseParameters));
        Assert.IsTrue(derived.Equals(new DerivedPageParameters()));
        Assert.AreEqual(typeof(DerivedPageParameters), derived.Clone().GetType());
    }

    [Test]
    public void RecordStoresSignedIdentifiersAndOwnedReferencesWithoutDispatch()
    {
        int calls = 0;
        PageStackSystem<GuiPage>.OnPageLoaded callback = page => { calls++; };
        var data = new GuiPageParameters();
        foreach (int identifier in new[] { int.MinValue, -1747306159, 0, int.MaxValue })
        {
            var record = new GuiPageRecord<GuiPage>(identifier, data, callback);
            Assert.AreEqual(identifier, record.m_identifier);
            Assert.AreSame(data, record.m_data);
            Assert.AreSame(callback, record.m_onLoadedCallback);
            Assert.AreEqual(0, calls);
        }
        var empty = new GuiPageRecord<GuiPage>(0, null, null);
        Assert.IsNull(empty.m_data);
        Assert.IsNull(empty.m_onLoadedCallback);
        Assert.AreEqual(0, calls);
        // The generic GuiPage argument is an abstract type identity; no GuiPage or PageStackSystem is created.
    }

    [Test]
    public void OverlayPendingValueDefaultAndCopiesRetainDataReferenceIdentity()
    {
        var empty = default(OverlaySystem.OverlayPageAndParameters);
        Assert.IsTrue(ReferenceEquals(null, empty.Page));
        Assert.IsNull(empty.Parameters);
        var first = new GuiPageParameters();
        var value = new OverlaySystem.OverlayPageAndParameters { Page = null, Parameters = first };
        var copy = value;
        Assert.IsTrue(ReferenceEquals(null, copy.Page));
        Assert.AreSame(first, copy.Parameters);
        value.Parameters = new GuiPageParameters();
        Assert.AreSame(first, copy.Parameters);
        Assert.AreNotSame(value.Parameters, copy.Parameters);
        value.Parameters = null;
        Assert.IsNull(value.Parameters);
        // A nested value type has its own initialization; no enclosing OverlaySystem.Instance access occurs.
    }

    [Test]
    public void InitializerSerializedFieldsAndConditionalStartAreMetadataOnly()
    {
        Type type = typeof(GuiInitializer);
        string[] names = { "m_appLoadingPage", "m_alwaysLoadedMenuPages", "m_alwaysLoadedDialogs", "m_alwaysLoadedOverlays", "defaultPixelDragThreshold", "defaultPixelDragDPI" };
        Type[] fieldTypes = { typeof(MenuPageIdentifier), typeof(MenuPageIdentifier[]), typeof(DialogPageIdentifier[]), typeof(OverlayPageIdentifier[]), typeof(float), typeof(float) };
        FieldInfo[] fields = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
        CollectionAssert.AreEqual(names, fields.Select(field => field.Name).ToArray());
        for (int i = 0; i < fields.Length; i++)
        {
            Assert.AreEqual(fieldTypes[i], fields[i].FieldType);
            Assert.AreEqual(FieldAttributes.Private, fields[i].Attributes);
            var attributes = fields[i].GetCustomAttributesData();
            Assert.AreEqual(1, attributes.Count);
            Assert.AreEqual(typeof(SerializeField), attributes[0].AttributeType);
            Assert.AreEqual(0, attributes[0].ConstructorArguments.Count);
            Assert.AreEqual(0, attributes[0].NamedArguments.Count);
        }
        MethodInfo start = type.GetMethod("Start", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsTrue(start.IsFamily);
        Assert.AreEqual(typeof(void), start.ReturnType);
        Assert.AreEqual(0, start.GetParameters().Length);
        var conditional = start.GetCustomAttributesData();
        Assert.AreEqual(1, conditional.Count);
        Assert.AreEqual(typeof(System.Diagnostics.ConditionalAttribute), conditional[0].AttributeType);
        Assert.AreEqual("BUILD_DEVELOPMENT", conditional[0].ConstructorArguments.Single().Value);
        Assert.AreEqual(0, conditional[0].NamedArguments.Count);
        // CAD does not instantiate attributes or invoke Start; ctor float stores remain separate Engine obligations.
    }

    [Test]
    public void MenuPageFadeFieldKeepsProtectedBooleanAndOrderedSerializationAttributes()
    {
        Type type = typeof(MenuPage);
        FieldInfo[] fields = type.GetFields(Declared).OrderBy(field => field.MetadataToken).ToArray();
        Assert.AreEqual(1, fields.Length);
        FieldInfo field = fields[0];
        Assert.AreEqual("m_fadeTransition", field.Name);
        Assert.AreEqual(typeof(bool), field.FieldType);
        Assert.AreEqual(FieldAttributes.Family, field.Attributes);
        Assert.IsTrue(field.IsFamily);
        Assert.IsFalse(field.IsPrivate);
        var attributes = field.GetCustomAttributesData();
        Assert.AreEqual(2, attributes.Count);
        Assert.AreEqual(typeof(TooltipAttribute), attributes[0].AttributeType);
        Assert.AreEqual("UnityEngine.CoreModule", attributes[0].AttributeType.Assembly.GetName().Name);
        Assert.AreEqual(1, attributes[0].ConstructorArguments.Count);
        Assert.AreEqual(typeof(string), attributes[0].ConstructorArguments[0].ArgumentType);
        const string tooltip = "Whether this page should trigger a fade-to-white effect on hide";
        Assert.AreEqual(63, tooltip.Length);
        Assert.AreEqual(tooltip, attributes[0].ConstructorArguments[0].Value);
        Assert.AreEqual(0, attributes[0].NamedArguments.Count);
        Assert.AreEqual(typeof(SerializeField), attributes[1].AttributeType);
        Assert.AreEqual("UnityEngine.CoreModule", attributes[1].AttributeType.Assembly.GetName().Name);
        Assert.AreEqual(0, attributes[1].ConstructorArguments.Count);
        Assert.AreEqual(0, attributes[1].NamedArguments.Count);
        // CAD observes declarations only; no MenuPage, singleton, scene or callback is constructed or invoked.
    }

    private static void Expected(Type type, string fullName, int originalFlags)
    {
        Assert.AreEqual(fullName, type.FullName);
        Assert.AreEqual("HLUnityUI.Runtime", type.Assembly.GetName().Name);
        Assert.AreEqual(originalFlags, (int)type.Attributes);
        // Original/current tokens, natural names and generated owner ordinals are not equated.
    }
}
