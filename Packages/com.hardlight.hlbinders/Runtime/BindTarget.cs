// Original HLBinders.Runtime.dll: Hardlight.UI.Binding.BindTarget 0x02000019; bodies translated from the 1.10.1 x86-64 binary.
// 0x0600007b get_IsSet 0x19F9960; 0x0600007c get_Target 0x19F99E0; 0x0600007d get_MemberName 0x19F99F0;
// 0x0600007e get_PlayModeOnly 0x19F9A00; 0x0600007f SetTarget 0x19F9A10; 0x06000080 SetValue 0x19F48D0;
// 0x06000081 GetTargetType 0x19F9E10; 0x06000082 Equals 0x19F9EA0; 0x06000083 Cache 0x19F9AC0; 0x06000084 .ctor 0x19F4D40.
// Original quirks: Cache leaves targetType unchanged when no member resolves; SetTarget recaches only on a change.

using System;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hardlight.UI.Binding
{
    [Serializable]
    public class BindTarget : IEquatable<BindTarget>
    {
        [SerializeField]
        private Object target;

        [SerializeField]
        private string memberName;

        [SerializeField]
        private bool filterTargets = true;

        [SerializeField]
        private bool playModeOnly = true;

        private FieldInfo fieldInfo;
        private PropertyInfo propertyInfo;
        private MethodInfo methodInfo;
        private Type targetType;
        private bool cached;

        public bool IsSet
        {
            get { return target != null && !string.IsNullOrEmpty(memberName); }
        }

        public Object Target
        {
            get { return target; }
        }

        public string MemberName
        {
            get { return memberName; }
        }

        public bool PlayModeOnly
        {
            get { return playModeOnly; }
        }

        public void SetTarget(Object target, string memberName)
        {
            if (this.target != target || this.memberName != memberName)
            {
                this.target = target;
                this.memberName = memberName;
                Cache();
            }
        }

        public void SetValue(object value)
        {
            if (!IsSet) return;
            if (!cached) Cache();
            // String members receive the value's text; a null value becomes the empty string.
            if (targetType == typeof(string))
                value = value != null ? value.ToString() : string.Empty;
            if (fieldInfo != null) BindingUtils.SetFieldValue(fieldInfo, target, value);
            if (propertyInfo != null) BindingUtils.SetPropertyValue(propertyInfo, target, value);
            if (methodInfo != null) BindingUtils.SetMethodValue(methodInfo, target, value);
        }

        public Type GetTargetType()
        {
            if (!IsSet) return null;
            if (!cached) Cache();
            return targetType;
        }

        public bool Equals(BindTarget other)
        {
            return other != null && target == other.target && memberName == other.memberName;
        }

        private void Cache()
        {
            MemberInfo member = BindingUtils.GetMemberInfo(target.GetType(), memberName);
            fieldInfo = member as FieldInfo;
            propertyInfo = member as PropertyInfo;
            methodInfo = member as MethodInfo;
            if (fieldInfo != null) targetType = BindingUtils.UnderlyingFieldSetType(fieldInfo);
            else if (propertyInfo != null) targetType = BindingUtils.UnderlyingPropertySetType(propertyInfo);
            else if (methodInfo != null) targetType = BindingUtils.UnderlyingMethodSetType(methodInfo);
            cached = true;
        }
    }
}
