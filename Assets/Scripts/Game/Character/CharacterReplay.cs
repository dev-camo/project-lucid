using System;
using System.IO;
using System.Text;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 02000342, all 20 declared methods / 11 fields.
    // Both complete architecture ranges are retained separately. This candidate
    // requires the genuine Character, App, and global Core OpString providers.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterReplay
    {
        private const float FloatingPointAccuracy = 6f;
        // 0600147e: original cctor writes the raw single 0x49742400.
        // An initializer retains the original BeforeFieldInit declaration.
        private static readonly float FloatingPointMultiplier = 1000000f;
        private bool m_isActive;
        private bool m_outputActive;
        private int m_outputLine;
        private readonly StringBuilder m_outputString = new StringBuilder();
        private StreamWriter m_writer;
        private bool m_inputActive;
        private string m_inputString;
        private int m_inputStringIndex;
        private readonly Character m_character;

        // 0600146b; ARM745fec / x8676ab10: raw playback flag.
        public bool InputActive => m_inputActive;

        // 0600146c: StringBuilder initializer precedes Object's constructor;
        // character is assigned afterward, with all remaining fields zero.
        public CharacterReplay(Character character)
        {
            m_character = character;
        }

        // 0600146d: native filesystem behavior retained, unexecuted privately.
        // Mode is refreshed even when an existing output session causes return.
        public void StartOutput()
        {
            ReplayMode mode = ProcessManager.GetSystem<App>(null, true).Storage
                .GetValue<ReplayMode>(AppFSMKeys.ReplayMode, 0, true);
            m_isActive = mode != ReplayMode.Off;
            if (!m_isActive || m_outputActive) return;

            OpString directory = OpString.medium + Application.persistentDataPath + "/Replays";
            Directory.CreateDirectory(directory);
            OpString prefix = directory + "/Replay_";
            int lastFile = -2;
            bool exists;
            do
            {
                exists = File.Exists(string.Format("{0}{1:D4}.txt", prefix, lastFile + 2));
                ++lastFile;
            }
            while (exists);

            m_outputActive = true;
            m_outputLine = 0;
            m_outputString.Length = 0;
            int outputFile = lastFile + 1;
            if (mode == ReplayMode.Playback && outputFile >= 1)
            {
                m_inputString = File.ReadAllText(string.Format("{0}{1:D4}.txt", prefix, lastFile));
                m_inputActive = true;
                m_inputStringIndex = 0;
            }
            OpString path = prefix + string.Format("{0:D4}.txt", outputFile);
            File.WriteAllText(path, string.Empty);
            m_writer = new StreamWriter(path, true);
            m_outputString.Append(string.Format("\n{0:D4}. ", m_outputLine));
        }

        // 0600146e: Close is the original StreamWriter virtual slot8; a
        // thrown close leaves both writer and active flag intact.
        public void EndOutput()
        {
            if (!m_outputActive) return;
            m_writer.Close();
            m_writer = null;
            m_outputActive = false;
        }

        // 0600146f: movement is extracted before reading the live queued flags.
        public bool FileExtractBrain(CharacterBrain brain, out Vector2 movement, out CharacterActionFlags actions)
        {
            movement = FileExtractValue(brain.GetMovement());
            actions = brain.ActionStateQueued;
            if (m_isActive) actions = FileExtractValue(actions.ToInt());
            return m_inputActive;
        }

        // 06001470: both delimiter searches use the same current index; no
        // advance or malformed-input guard precedes Substring / Split.
        private string[] FileExtractArray()
        {
            if (!m_inputActive) return null;
            int start = m_inputString.IndexOf("(", m_inputStringIndex, StringComparison.Ordinal);
            int end = m_inputString.IndexOf(")", m_inputStringIndex, StringComparison.Ordinal);
            return m_inputString.Substring(start + 1, end - start - 1).Split(',', StringSplitOptions.None);
        }

        // 06001471: multiply, original Unity nearest-even rounding, divide.
        // No clamping, finite-value guard, or away-from-zero rounding is added.
        private static float Round(float value)
        {
            float multiplier = FloatingPointMultiplier;
            return Mathf.Round(value * multiplier) / multiplier;
        }

        // 06001472 / 06001473: scalar overloads have no m_isActive guard and
        // use the original current-culture parsers and newline formatting.
        private int FileExtractValue(int vIn)
        {
            if (m_inputActive)
            {
                int end = m_inputString.IndexOf("\n", m_inputStringIndex + 1, StringComparison.Ordinal);
                vIn = int.Parse(m_inputString.Substring(m_inputStringIndex + 1, end - (m_inputStringIndex + 1)));
            }
            FileAppendOutput(string.Format("\n{0}", vIn));
            return vIn;
        }

        private float FileExtractValue(float vIn)
        {
            if (m_inputActive)
            {
                int end = m_inputString.IndexOf("\n", m_inputStringIndex + 1, StringComparison.Ordinal);
                vIn = float.Parse(m_inputString.Substring(m_inputStringIndex + 1, end - (m_inputStringIndex + 1)));
            }
            FileAppendOutput(string.Format("\n{0}", vIn));
            return vIn;
        }

        // 06001474: nonempty arrays are indexed directly, without a length2
        // check. Parsed components are not rounded a second time.
        private Vector2 FileExtractValue(Vector2 vIn)
        {
            if (!m_isActive) return vIn;
            string[] values = FileExtractArray();
            Vector2 value = values != null && values.Length != 0
                ? new Vector2(float.Parse(values[0]), float.Parse(values[1]))
                : new Vector2(Round(vIn.x), Round(vIn.y));
            FileAppendOutput(string.Format("\n{0:F6}", value));
            return value;
        }

        // 06001475: same raw nonempty-array guard, now three components.
        public Vector3 FileExtractValue(Vector3 vIn)
        {
            if (!m_isActive) return vIn;
            string[] values = FileExtractArray();
            Vector3 value = values != null && values.Length != 0
                ? new Vector3(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]))
                : new Vector3(Round(vIn.x), Round(vIn.y), Round(vIn.z));
            FileAppendOutput(string.Format("\n{0:F6}", value));
            return value;
        }

        // 06001476: preserves all four raw quaternion components.
        public Quaternion FileExtractValue(Quaternion qIn)
        {
            if (!m_isActive) return qIn;
            string[] values = FileExtractArray();
            Quaternion value = values != null && values.Length != 0
                ? new Quaternion(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3]))
                : new Quaternion(Round(qIn.x), Round(qIn.y), Round(qIn.z), Round(qIn.w));
            FileAppendOutput(string.Format("\n{0:F6}", value));
            return value;
        }

        // 06001477: original Unity matrix linear index order and original
        // OpString calls retained, including a fresh i accessor on each append.
        public Matrix4x4 FileExtractValue(Matrix4x4 mtxIn)
        {
            if (!m_isActive) return mtxIn;
            Matrix4x4 value = new Matrix4x4();
            string[] values = FileExtractArray();
            if (values != null && values.Length != 0)
            {
                for (int i = 0; i < 16; ++i) value[i] = float.Parse(values[i]);
            }
            else
            {
                for (int i = 0; i < 16; ++i) value[i] = Round(mtxIn[i]);
            }
            string output = new string("(");
            for (int i = 0; i < 16; ++i)
            {
                output += (string)(OpString.i + string.Format("{0:F6}", value[i]));
                if (i < 15) output += (string)(OpString.i + ", ");
            }
            output += (string)(OpString.i + ")");
            FileAppendOutput("\n" + output);
            return value;
        }

        // 06001478: input exhaustion returns after Break, while a comparison
        // mismatch clears input then still increments the index and writes.
        private void FileAppendOutput(string output)
        {
            if (!m_outputActive) return;
            if (m_inputActive)
            {
                if (m_inputStringIndex >= m_inputString.Length)
                {
                    m_inputActive = false;
                    Debug.Break();
                    return;
                }
                if (string.CompareOrdinal(m_inputString.Substring(m_inputStringIndex, output.Length), output) != 0)
                {
                    m_inputActive = false;
                    Debug.Break();
                }
                m_inputStringIndex += output.Length;
            }
            m_writer.Write(output);
        }

        // 06001479: original formatting reads body properties in this order.
        public void OutputPreFSM(Rigidbody body)
        {
            if (!m_outputActive) return;
            FileAppendOutput(string.Format("\n{0:D4}. PreFSM: Position = {1:F6}, Rotation = {2:F6}, Velocity = {3:F6}",
                m_outputLine, body.position, body.rotation, body.velocity));
        }

        // 0600147a: debug output is built before reading Rigidbody properties;
        // only newline characters after character0 are changed to commas.
        public void OutputPostFSM(Rigidbody body)
        {
            if (!m_outputActive) return;
            m_character.DebugGetTransformInfo(m_outputString);
            FileAppendOutput(string.Format("\n{0:D4}. PostFSM: Position = {1:F6}, Rotation = {2:F6}, Velocity = {3:F6}",
                m_outputLine, body.position, body.rotation, body.velocity));
            StringBuilder output = m_outputString;
            output.Replace('\n', ',', 1, output.Length - 1);
            FileAppendOutput(m_outputString.ToString());
            ++m_outputLine;
            m_outputString.Length = 0;
            m_outputString.Append(string.Format("\n{0:D4}. ", m_outputLine));
        }

        // 0600147b: both original architectures return immediately.
        public static void AddDebugButton() { }

        // 0600147c: unknown values log and are still stored unchanged.
        private static void OnDebugButtonClick()
        {
            IGraphStorage storage = ProcessManager.GetSystem<App>(null, true).Storage;
            ReplayMode mode = storage.GetValue<ReplayMode>(AppFSMKeys.ReplayMode, 0, true);
            switch (mode)
            {
                case ReplayMode.Off: mode = ReplayMode.Record; break;
                case ReplayMode.Record: mode = ReplayMode.Playback; break;
                case ReplayMode.Playback: mode = ReplayMode.Off; break;
                default: HLOutput.LogError(string.Format("Unknown replay mode selected: {0}.", mode)); break;
            }
            storage.SetValue(AppFSMKeys.ReplayMode, mode);
        }

        // 0600147d: querying the label stores the enum0 default when absent.
        private static string GetDebugButtonName()
        {
            ReplayMode mode = ProcessManager.GetSystem<App>(null, true).Storage
                .GetValue<ReplayMode>(AppFSMKeys.ReplayMode, 0, true);
            return string.Format("Replay mode: {0}", mode);
        }
    }
}
