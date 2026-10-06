using System;
using System.Linq;
using System.Reflection;

namespace ProjectLucid
{
    public static class NetworkBufferVerification
    {
        private static int checks;
        private static void Check(bool value, string message)
        {
            checks++;
            if (!value) throw new Exception(message);
        }
        private static void Throws<T>(Action action, string message) where T : Exception
        {
            bool matched = false;
            try { action(); } catch (T) { matched = true; }
            Check(matched, message);
        }
        private static void Bytes(byte[] actual, byte[] expected, string message) => Check(actual.SequenceEqual(expected), message);
        public static int RunManaged()
        {
            checks = 0;
            var type = typeof(NetworkBuffer);
            Check(type.Assembly.GetName().Name == "HLNetworking.Runtime", "original networking assembly");
            Check(type.IsValueType && !type.IsEnum && type.IsPublic, "original public value type");
            Check(type.IsLayoutSequential && type.GetCustomAttributesData().Count == 0, "original implicit sequential/no attrs");
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly).OrderBy(f => f.MetadataToken).ToArray();
            string[] names = { "m_data", "m_start", "m_capacity", "m_offset", "m_limit" };
            Check(fields.Length == 5, "complete original field count");
            for (int i = 0; i < fields.Length; i++)
            {
                Check(fields[i].Name == names[i] && fields[i].IsPrivate && !fields[i].IsInitOnly, "ordered private mutable field " + i);
                Check(fields[i].FieldType == (i == 0 ? typeof(byte[]) : typeof(int)), "original field type " + i);
                Check(fields[i].GetCustomAttributesData().Count == 0, "original field attributes " + i);
            }
            Check(type.GetConstructors().Length == 1 && type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Length == 14, "all fifteen original own methods");
            var unvalidated = new NetworkBuffer(null, -5, -7);
            object boxed = unvalidated;
            Check(fields[0].GetValue(boxed) == null && (int)fields[1].GetValue(boxed) == -5 && (int)fields[2].GetValue(boxed) == -7 && (int)fields[3].GetValue(boxed) == 0 && (int)fields[4].GetValue(boxed) == -7, "constructor retains invalid inputs without validation");
            Check(unvalidated.Offset == 0 && unvalidated.Remaining == 0, "negative limit remaining clamp");
            var zero = default(NetworkBuffer);
            Check(zero.Offset == 0 && zero.Remaining == 0, "default struct getters");
            Throws<IndexOutOfRangeException>(() => zero.readVarInt32(), "limit checked before default null array");
            Check(zero.Offset == 0, "exhausted read leaves cursor");
            Throws<IndexOutOfRangeException>(() => zero.writeVarInt32(1), "limit checked before default null write");
            Check(zero.Offset == 0, "exhausted write leaves cursor");
            var nil = new NetworkBuffer(null, 0, 1);
            Throws<NullReferenceException>(() => nil.readVarInt32(), "positive limit null read");
            Check(nil.Offset == 1, "null read increments cursor before failure");
            nil = new NetworkBuffer(null, 0, 1);
            Throws<NullReferenceException>(() => nil.writeVarInt32(1), "positive limit null write");
            Check(nil.Offset == 1, "null write increments cursor before failure");

            int[] numbers = { 0, 1, 127, 128, 255, 16383, 16384, 2097151, 2097152, 268435455, 268435456, int.MaxValue, int.MinValue, -1, -2 };
            byte[][] encodings = {
                new byte[] {0}, new byte[] {1}, new byte[] {127}, new byte[] {128,1}, new byte[] {255,1},
                new byte[] {255,127}, new byte[] {128,128,1}, new byte[] {255,255,127}, new byte[] {128,128,128,1},
                new byte[] {255,255,255,127}, new byte[] {128,128,128,128,1}, new byte[] {255,255,255,255,7},
                new byte[] {128,128,128,128,8}, new byte[] {255,255,255,255,15}, new byte[] {254,255,255,255,15}
            };
            for (int i = 0; i < numbers.Length; i++)
            {
                var data = new byte[encodings[i].Length];
                var writer = new NetworkBuffer(data, 0, data.Length);
                writer.writeVarInt32(numbers[i]);
                Bytes(data, encodings[i], "exact original integer bytes " + numbers[i]);
                Check(NetworkBuffer.varInt32Length(numbers[i]) == data.Length, "exact encoded length " + numbers[i]);
                Check(writer.Offset == data.Length && writer.Remaining == 0, "write cursor " + numbers[i]);
                var reader = new NetworkBuffer(data, 0, data.Length);
                Check(reader.readInt32() == numbers[i] && reader.Offset == data.Length, "alias read and cursor " + numbers[i]);
                Array.Clear(data, 0, data.Length); writer = new NetworkBuffer(data, 0, data.Length); writer.writeInt32(numbers[i]);
                Bytes(data, encodings[i], "alias write " + numbers[i]);
            }
            var noncanonical = new NetworkBuffer(new byte[] {128,0}, 0, 2);
            Check(noncanonical.readVarInt32() == 0 && noncanonical.Offset == 2, "noncanonical zero accepted");
            var overlong = new NetworkBuffer(new byte[] {128,128,128,128,128,1}, 0, 6);
            Check(overlong.readVarInt32() == 8 && overlong.Offset == 6, "overlong varint shift wraps at32");
            var truncated = new NetworkBuffer(new byte[] {128}, 0, 1);
            Throws<IndexOutOfRangeException>(() => truncated.readVarInt32(), "unterminated varint fails at logical limit");
            Check(truncated.Offset == 1, "truncated read retains consumed byte");
            var physical = new NetworkBuffer(new byte[0], 0, 1);
            Throws<IndexOutOfRangeException>(() => physical.readVarInt32(), "physical array failure");
            Check(physical.Offset == 1, "physical read failure retains increment");
            physical = new NetworkBuffer(new byte[0], 0, 1);
            Throws<IndexOutOfRangeException>(() => physical.writeVarInt32(128), "physical write failure");
            Check(physical.Offset == 1, "physical write failure retains increment");
            var partialBytes = new byte[1]; var partial = new NetworkBuffer(partialBytes, 0, 1);
            Throws<IndexOutOfRangeException>(() => partial.writeVarInt32(128), "second byte logical write failure");
            Check(partial.Offset == 1 && partialBytes[0] == 128, "partial write committed first byte");

            var shared = new byte[] {99,11,22,33,88}; var parent = new NetworkBuffer(shared, 1, 3); parent.skip(2);
            var child = parent.SubRange(0, 1);
            Check(child.Offset == 0 && child.Remaining == 1 && child.readInt32() == 11, "subrange independent of parent cursor");
            Check(parent.Offset == 2 && ReferenceEquals(fields[0].GetValue(child), shared), "subrange aliases same storage");
            child = parent.SubRange(1, 1); child.writeInt32(44);
            Check(shared[2] == 44 && parent.Offset == 2, "subrange write ownership and independent cursor");
            Throws<IndexOutOfRangeException>(() => parent.SubRange(2, 2), "subrange logical limit");
            child = parent.SubRange(-1, 1);
            Check((int)fields[1].GetValue(child) == 0 && child.readInt32() == 99, "negative subrange offset permitted");
            child = parent.SubRange(int.MaxValue, 1);
            Check((int)fields[1].GetValue(child) == int.MinValue, "subrange addition wraps unchecked");
            parent.skip(-3);
            Check(parent.Offset == -1 && parent.Remaining == 4, "negative skip expands remaining");
            Throws<IndexOutOfRangeException>(() => parent.skip(5), "skip checks only upper bound");
            Check(parent.Offset == -1, "failed skip retains cursor");
            parent = new NetworkBuffer(shared, 0, int.MaxValue); parent.skip(int.MaxValue); parent.skip(1);
            Check(parent.Offset == int.MinValue && parent.Remaining == 0, "skip and remaining signed overflow retained");

            var dest = new byte[2]; var copy = new NetworkBuffer(new byte[] {55,66}, 0, 2);
            copy.readBytes(dest, 0, 2); Bytes(dest, new byte[] {55,66}, "raw read copy");
            Check(copy.Offset == 2, "raw read advances after success");
            copy = new NetworkBuffer(new byte[] {55,66}, 0, 2);
            Throws<ArgumentException>(() => copy.readBytes(new byte[0], 0, 1), "copy destination failure");
            Check(copy.Offset == 0, "copy failure retains cursor");
            Throws<IndexOutOfRangeException>(() => copy.readBytes(null, 0, 3), "logical read limit precedes Array.Copy null check");
            Check(copy.Offset == 0, "logical copy failure retains cursor");
            Throws<ArgumentOutOfRangeException>(() => copy.readBytes(dest, 0, -1), "negative read reaches Array.Copy");
            Check(copy.Offset == 0, "negative copy failure retains cursor");
            dest = new byte[2]; copy = new NetworkBuffer(dest, 0, 2); copy.writeBytes(new byte[] {77,88}, 0, 2);
            Bytes(dest, new byte[] {77,88}, "raw write copy"); Check(copy.Offset == 2, "raw write cursor");
            copy = new NetworkBuffer(dest, 0, 2);
            Throws<ArgumentException>(() => copy.writeBytes(new byte[0], 0, 1), "copy source failure");
            Check(copy.Offset == 0, "write copy failure retains cursor");
            Throws<IndexOutOfRangeException>(() => copy.writeBytes(null, 0, 3), "logical write limit precedes null source");

            string[] strings = { null, "", "A", "é", "😎", "A\0B", "\ud800" };
            byte[][] encodedStrings = { new byte[] {0}, new byte[] {1}, new byte[] {2,65}, new byte[] {3,195,169}, new byte[] {5,240,159,152,142}, new byte[] {4,65,0,66}, new byte[] {4,239,191,189} };
            for (int i = 0; i < strings.Length; i++)
            {
                dest = new byte[encodedStrings[i].Length]; copy = new NetworkBuffer(dest, 0, dest.Length); copy.writeString(strings[i]);
                Bytes(dest, encodedStrings[i], "exact UTF8 and prefix " + i);
                Check(NetworkBuffer.stringLength(strings[i]) == dest.Length && copy.Offset == dest.Length, "string size/cursor " + i);
                copy = new NetworkBuffer(dest, 0, dest.Length); string expected = i == 0 ? "" : i == 6 ? "�" : strings[i];
                Check(copy.readString() == expected && copy.Offset == dest.Length, "string decode " + i);
            }
            dest = new byte[1]; copy = new NetworkBuffer(dest, 0, 1);
            Throws<IndexOutOfRangeException>(() => copy.writeString("é"), "string payload capacity failure");
            Check(dest[0] == 3 && copy.Offset == 1, "failed payload preserves prefix and cursor");
            copy = new NetworkBuffer(new byte[] {3}, 0, 1);
            Throws<IndexOutOfRangeException>(() => copy.readString(), "string payload truncation");
            Check(copy.Offset == 1, "read payload failure preserves header consumption");
            copy = new NetworkBuffer(new byte[] {2,255}, 0, 2);
            Check(copy.readString() == "�", "UTF8 decoder replacement fallback");
            return checks;
        }
    }
}
