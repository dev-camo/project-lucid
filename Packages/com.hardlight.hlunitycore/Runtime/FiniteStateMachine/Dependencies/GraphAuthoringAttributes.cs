using System;
using UnityEngine;

namespace Hardlight
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public abstract class GraphAttributeBase : Attribute
    {
        // HLUnityCore.Runtime.dll:0x06000c4c; arm64 0x1b0a754: base only.
        protected GraphAttributeBase() { }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class GraphNodeDefaultNameAttribute : Attribute
    {
        public readonly string DefaultName;
        // 0x06000c54; arm64 0x1b0aca4: retain the argument without validation.
        public GraphNodeDefaultNameAttribute(string defaultName) { DefaultName = defaultName; }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class GraphNodeColourAttribute : Attribute
    {
        // 0x06000c3b; arm64 0x1b0a664: return the color value unchanged.
        public Color Colour { get; }
        // 0x06000c3c; arm64 0x1b0a670: raw RGB arguments, alpha always one.
        public GraphNodeColourAttribute(float r, float g, float b) { Colour = new Color(r, g, b, 1f); }
    }

    public sealed class GraphNodeFocusAttribute : GraphAttributeBase
    {
        public readonly string TargetNodeType;
        public readonly bool IncludeChildren;
        // 0x06000c40; arm64 0x1b0a75c. Null namespace/class concatenate as
        // empty strings; only a blank assembly is omitted, with no trimming.
        public GraphNodeFocusAttribute(string targetNamespace, string targetClassName,
            string targetAssembly = null, bool includeChildren = false)
        {
            TargetNodeType = targetNamespace + "." + targetClassName;
            if (!string.IsNullOrWhiteSpace(targetAssembly)) TargetNodeType += ", " + targetAssembly;
            IncludeChildren = includeChildren;
        }
        // 0x06000c41; arm64 0x1b0a848: Type.FullName, no assembly suffix.
        public GraphNodeFocusAttribute(Type type, bool includeChildren = false)
        {
            TargetNodeType = type.FullName;
            IncludeChildren = includeChildren;
        }
    }

    public sealed class GraphNodePopupAttribute : GraphAttributeBase
    {
        public readonly string TargetNodeType;
        public readonly bool IncludeChildren;
        public readonly bool IncludeInvisible;
        // 0x06000c46; arm64 0x1b0a92c: same name formatting as Focus.
        public GraphNodePopupAttribute(string targetNamespace, string targetClassName,
            string targetAssembly = null, bool includeChildren = false, bool includeInvisible = false)
        {
            TargetNodeType = targetNamespace + "." + targetClassName;
            if (!string.IsNullOrWhiteSpace(targetAssembly)) TargetNodeType += ", " + targetAssembly;
            IncludeChildren = includeChildren;
            IncludeInvisible = includeInvisible;
        }
        // 0x06000c47; arm64 0x1b0aa20: Type.FullName then the supplied flags.
        public GraphNodePopupAttribute(Type type, bool includeChildren = false, bool includeInvisible = false)
        {
            TargetNodeType = type.FullName;
            IncludeChildren = includeChildren;
            IncludeInvisible = includeInvisible;
        }
    }

    public sealed class GraphEnumPopupAttribute : GraphAttributeBase
    {
        public readonly Type EnumType;
        // 0x06000c45; arm64 0x1b0a8f8: retain even null or non-enum types.
        public GraphEnumPopupAttribute(Type enumType) { EnumType = enumType; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field)]
    public class GraphDisplayNameAttribute : Attribute
    {
        public readonly string DisplayName;
        // 0x06000c4d; arm64 0x1b0ab48: retain the argument without validation.
        public GraphDisplayNameAttribute(string displayName) { DisplayName = displayName; }
    }

    public sealed class GraphHorizontalBreakAttribute : GraphAttributeBase
    {
        public readonly bool AfterProperty;
        // 0x06000c3f; arm64 0x1b0a728: store the supplied flag unchanged.
        public GraphHorizontalBreakAttribute(bool afterProperty = true) { AfterProperty = afterProperty; }
    }

    public sealed class FiniteStateMachineOpenAttribute : GraphAttributeBase
    {
        // 0x06000360; arm64 0x1abb988: GraphAttributeBase constructor only.
        public FiniteStateMachineOpenAttribute() { }
    }

    public sealed class FiniteStateMachinePopupAttribute : GraphAttributeBase
    {
        public readonly string ManualEntryFieldName;
        // 0x06000364; arm64 0x1abba68: retain the supplied string, even null.
        public FiniteStateMachinePopupAttribute(string manualEntryFieldName = "ManualEntry")
        {
            ManualEntryFieldName = manualEntryFieldName;
        }
    }

    public abstract class FiniteStateMachineChildPopupAttribute : GraphAttributeBase
    {
        public readonly string FieldName;
        public readonly bool AddDefaultEntry;
        // 0x06000361; arm64 0x1abb990: retain both supplied arguments.
        public FiniteStateMachineChildPopupAttribute(string fieldName, bool addDefaultEntry)
        {
            FieldName = fieldName;
            AddDefaultEntry = addDefaultEntry;
        }
    }

    public sealed class FiniteStateMachineChildStatePopupAttribute : FiniteStateMachineChildPopupAttribute
    {
        // 0x06000362; arm64 0x1abb9d8: original arguments pass through.
        public FiniteStateMachineChildStatePopupAttribute(string fieldName, bool addDefaultEntry = false)
            : base(fieldName, addDefaultEntry) { }
    }
}
