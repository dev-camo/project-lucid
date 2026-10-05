using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hardlight;

namespace ProjectLucid
{
    public static class BugInfoVerification
    {
        public static void Run()
        {
            Console.WriteLine("PASS BugInfo checks=" + RunManaged());
        }

        public static int RunManaged()
        {
            int checks = 0;
            Action<bool, string> check = (condition, message) =>
            {
                if (!condition) throw new InvalidOperationException("BugInfo: " + message);
                checks++;
            };
            Type type = typeof(BugInfo);
            check(type.Assembly.GetName().Name == "HLUnityCore.Runtime", "original assembly identity");
            check(type.FullName == "Hardlight.BugInfo", "original type identity");
            check(type.IsPublic && type.IsValueType && type.IsSealed && type.IsLayoutSequential, "original value-type flags");
            check(type.IsDefined(typeof(IsReadOnlyAttribute), false), "original readonly type attribute");
            check(type.GetInterfaces().Length == 1 && type.GetInterfaces()[0] == typeof(IEquatable<BugInfo>), "original typed equality interface");
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            check(fields.Length == 2, "complete two-field schema");
            check(fields[0].Name == "m_id" && fields[1].Name == "m_name", "original field order");
            foreach (FieldInfo field in fields)
            {
                check(field.IsPrivate && field.IsInitOnly && field.FieldType == typeof(string), "private readonly string " + field.Name);
                check(field.GetCustomAttributes(false).Length == 0, "no fabricated field attributes " + field.Name);
            }
            MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            check(methods.Length == 3, "three original declared ordinary methods");
            check(type.GetConstructors().Length == 1, "one original constructor");
            check(type.GetMethod("Equals", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, new[] { typeof(object) }, null) == null, "no invented object equality override");

            string id = new string(new[] { 's', 'a', 'm', 'e' });
            string name = new string(new[] { 'D', 'i', 's', 'p', 'l', 'a', 'y' });
            BugInfo original = new BugInfo(id, name);
            check(ReferenceEquals(fields[0].GetValue(original), id), "constructor retains identifier reference");
            check(ReferenceEquals(fields[1].GetValue(original), name), "constructor retains name reference");
            check(original.Equals(new BugInfo("same", "different")), "typed equality ignores display name");
            check(!original.Equals(new BugInfo("SAME", "Display")), "identifier equality is case-sensitive");
            check(!original.Equals(new BugInfo("other", name)), "different identifier is unequal");
            check(!original.Equals(new BugInfo(null, name)), "nonnull receiver compares false to null identifier");
            check(original.GetHashCode() == id.GetHashCode(), "identifier's original string hash");
            check(original.GetHashCode() == new BugInfo(id, null).GetHashCode(), "display name does not affect hash");
            check(original.ToString() == "Bug Info: same - Display", "exact four-string concat");
            check(new BugInfo("", "").ToString() == "Bug Info:  - ", "empty display fields");
            check(new BugInfo(null, "name").ToString() == "Bug Info:  - name", "null identifier string concat");
            check(new BugInfo("id", null).ToString() == "Bug Info: id - ", "null name string concat");
            check(default(BugInfo).ToString() == "Bug Info:  - ", "default value concat");
            check(!original.Equals((object)new BugInfo("same", "different")), "inherited value-type object equality remains separate");
            check(original.Equals((object)new BugInfo("same", "Display")), "inherited value-type equality with both fields equal");
            ExpectNullReference(() => default(BugInfo).Equals(original), check, "default receiver equality");
            ExpectNullReference(() => default(BugInfo).Equals(default(BugInfo)), check, "two null identifiers do not become typed-equal");
            ExpectNullReference(() => new BugInfo(null, "named").GetHashCode(), check, "null identifier hash");
            check(new BugInfo("", null).GetHashCode() == "".GetHashCode(), "empty identifier hash stays string-derived");
            check(new BugInfo("", "one").Equals(new BugInfo("", "two")), "empty nonnull identifiers are typed-equal");
            check(new BugInfo("prefix", "line\nbreak").ToString() == "Bug Info: prefix - line\nbreak", "display content is not normalized");
            return checks;
        }

        private static void ExpectNullReference(Action action, Action<bool, string> check, string message)
        {
            try { action(); }
            catch (NullReferenceException) { check(true, message); return; }
            check(false, message);
        }
    }
}
