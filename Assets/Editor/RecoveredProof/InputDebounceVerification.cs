using System;
using System.Reflection;
using Hardlight;

namespace ProjectLucid.Editor
{
    public static class InputDebounceVerification
    {
        public static int Run() => RunManaged();

        private static void Require(bool value, string message, ref int checks)
        {
            if (!value) throw new InvalidOperationException(message);
            checks++;
        }

        private static T Field<T>(InputDebounce value, string name)
        {
            return (T)typeof(InputDebounce).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        }

        private static bool Same(float expected, float actual)
        {
            return float.IsNaN(expected) ? float.IsNaN(actual) : expected == actual;
        }

        private static void State(InputDebounce value, float input, float time,
            bool threshold, bool direction, bool pulse, bool continuous, string label, ref int checks)
        {
            Require(Same(input, value.Input), label + ": input", ref checks);
            Require(Same(time, Field<float>(value, "m_time")), label + ": timer", ref checks);
            Require(value.ThresholdExceeded == threshold, label + ": threshold", ref checks);
            Require(value.DirectionMaintained == direction, label + ": direction", ref checks);
            Require(value.TimeExceeded == pulse, label + ": crossing pulse", ref checks);
            Require(value.TimeExceededContinuous == continuous, label + ": strict continuous observation", ref checks);
        }

        public static int RunManaged()
        {
            int checks = 0;
            Type type = typeof(InputDebounce);
            Require(type.IsClass && !type.IsAbstract && type.BaseType == typeof(object), "real ordinary class", ref checks);
            Require(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic).Length == 9, "all nine original own fields", ref checks);
            foreach (string name in new[] { "m_threshold", "m_timeMax" })
                Require(type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).IsInitOnly, "readonly " + name, ref checks);
            foreach (string name in new[] { "TimeExceeded", "ThresholdExceeded", "DirectionMaintained" })
                Require(type.GetProperty(name).GetGetMethod().IsPublic && type.GetProperty(name).GetSetMethod(true).IsPrivate,
                    "original public getter/private setter " + name, ref checks);
            Require(type.GetProperty("Input").GetSetMethod(true) == null && type.GetProperty("TimeExceededContinuous").GetSetMethod(true) == null,
                "two computed properties have no setters", ref checks);
            ParameterInfo initial = type.GetConstructor(new[] { typeof(float), typeof(float), typeof(float) }).GetParameters()[2];
            Require(initial.IsOptional && initial.DefaultValue is float && (float)initial.DefaultValue == 0f, "initial input default Single0", ref checks);
            ParameterInfo[] resetParameters = type.GetMethod("Reset").GetParameters();
            Require(resetParameters[0].IsOptional && resetParameters[0].DefaultValue is bool && !(bool)resetParameters[0].DefaultValue,
                "ignoreInitialInput default false", ref checks);
            Require(resetParameters[1].IsOptional && resetParameters[1].DefaultValue is bool && !(bool)resetParameters[1].DefaultValue,
                "timeExceedContinuous default false", ref checks);

            InputDebounce d = new InputDebounce(.25f, .5f);
            State(d, 0f, 0f, false, false, false, false, "default constructor state", ref checks);
            Require(Field<float>(d, "m_threshold") == .25f && Field<float>(d, "m_timeMax") == .5f, "constructor preserves limits", ref checks);
            Require(!Field<bool>(d, "m_ignoreInitialInput") && !Field<bool>(d, "m_timeExceedContinuous"), "constructor flags default false", ref checks);
            State(new InputDebounce(.25f, .5f, .25f), .25f, 0f, false, false, false, false, "constructor exact threshold", ref checks);
            State(new InputDebounce(.25f, .5f, -.5f), -.5f, 0f, true, false, false, false, "constructor absolute threshold", ref checks);
            State(new InputDebounce(float.NaN, .5f, 1f), 1f, 0f, false, false, false, false, "constructor NaN threshold", ref checks);

            // An above-threshold first sample establishes history before time
            // begins; equality at the time limit is a single crossing pulse.
            d.Update(1f, .25f);
            State(d, 1f, 0f, true, false, false, false, "first above sample", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .25f, true, true, false, false, "hold below time limit", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .5f, true, true, true, false, "hold exact time limit", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .75f, true, true, false, true, "one-shot pulse ends", ref checks);
            d.Update(-1f, .25f);
            State(d, -1f, 0f, true, false, false, false, "reverse direction resets time", ref checks);
            d.Update(-1f, .5f);
            State(d, -1f, .5f, true, true, true, false, "new direction crossing", ref checks);
            d.SetInput(.1f);
            State(d, .1f, .5f, false, true, true, false, "SetInput leaves pulse and direction", ref checks);
            d.Update(-1f, .25f);
            State(d, -1f, 0f, true, false, false, false, "below-threshold history blocks accumulation", ref checks);
            d.Update(-.25f, .5f);
            State(d, -.25f, 0f, false, false, false, false, "equality is not threshold exceeded", ref checks);

            d.Reset(false, true);
            Require(!Field<bool>(d, "m_ignoreInitialInput") && Field<bool>(d, "m_timeExceedContinuous"), "Reset preserves requested mode", ref checks);
            State(d, 0f, 0f, false, false, false, false, "Reset clears ordinary state", ref checks);
            d.Update(1f, .5f);
            State(d, 1f, 0f, true, false, false, false, "continuous first sample still establishes history", ref checks);
            d.Update(1f, .5f);
            State(d, 1f, .5f, true, true, true, false, "continuous exact crossing", ref checks);
            d.Update(1f, .5f);
            State(d, 1f, 1f, true, true, true, true, "continuous repeated crossing", ref checks);

            d.Reset(true, false);
            Require(Field<bool>(d, "m_ignoreInitialInput") && !Field<bool>(d, "m_timeExceedContinuous"), "Reset ignore-first mode", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .75f, true, true, false, true, "ignore first input suppresses one-shot event", ref checks);
            Require(!Field<bool>(d, "m_ignoreInitialInput"), "ignore-first flag consumed once", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, 1f, true, true, false, true, "ignored hold remains above limit", ref checks);
            d.Reset(true, false);
            d.Update(.25f, 10f);
            State(d, .25f, 0f, false, false, false, false, "ignored below sample clears seeded timer", ref checks);
            Require(!Field<bool>(d, "m_ignoreInitialInput"), "below sample also consumes ignore flag", ref checks);
            d.Update(1f, .5f);
            State(d, 1f, 0f, true, false, false, false, "following above sample needs history", ref checks);
            d.Update(1f, .5f);
            State(d, 1f, .5f, true, true, true, false, "following hold crosses normally", ref checks);
            d.Reset(true, true);
            d.Update(1f, 0f);
            State(d, 1f, .5f, true, true, true, false, "ignored first continuous event includes equality", ref checks);

            d = new InputDebounce(.25f, .5f, 1f);
            d.Update(1f, .5f);
            State(d, 1f, .5f, true, true, true, false, "initial constructor input supplies history", ref checks);
            d.Update(1f, -.25f);
            State(d, 1f, .25f, true, true, false, false, "negative delta does not retroactively pulse", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .5f, true, true, true, false, "negative delta permits a later second crossing", ref checks);

            d = new InputDebounce(.25f, 0f, 1f);
            d.Update(1f, 0f);
            State(d, 1f, 0f, true, true, false, false, "zero limit never satisfies pre-crossing gate", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .25f, true, true, false, true, "zero-limit continuous observation is independent", ref checks);
            d.Reset(false, true);
            d.SetInput(1f);
            d.Update(1f, 0f);
            State(d, 1f, 0f, true, true, true, false, "continuous mode crosses zero limit", ref checks);
            d.Update(1f, -.25f);
            State(d, 1f, -.25f, true, true, false, false, "continuous mode retains negative time", ref checks);

            d = new InputDebounce(.25f, -.5f, 1f);
            State(d, 1f, 0f, true, false, false, true, "negative time limit is unnormalized", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, .25f, true, true, false, true, "negative one-shot gate already exceeded", ref checks);
            d.Reset(false, true);
            d.SetInput(1f);
            d.Update(1f, 0f);
            State(d, 1f, 0f, true, true, true, true, "negative limit continuous mode", ref checks);
            d = new InputDebounce(-.25f, .5f);
            d.Update(-0f, .5f);
            State(d, -0f, .5f, true, true, true, false, "zero signs agree under negative threshold", ref checks);
            Require(BitConverter.ToInt32(BitConverter.GetBytes(d.Input), 0) == int.MinValue, "raw negative-zero input remains stored", ref checks);
            d.Update(1f, .25f);
            State(d, 1f, 0f, true, false, false, false, "zero-to-positive sign changes direction", ref checks);

            d = new InputDebounce(float.NaN, .5f, 1f);
            d.Update(1f, 1f);
            State(d, 1f, 0f, false, false, false, false, "NaN threshold rejects ordered comparison", ref checks);
            d = new InputDebounce(float.PositiveInfinity, .5f, float.PositiveInfinity);
            d.Update(float.PositiveInfinity, 1f);
            State(d, float.PositiveInfinity, 0f, false, false, false, false, "infinite threshold equality is not exceeded", ref checks);
            d = new InputDebounce(.25f, float.NaN, 1f);
            d.Update(1f, .25f);
            State(d, 1f, .25f, true, true, false, false, "NaN time limit rejects both ordered predicates", ref checks);
            d = new InputDebounce(.25f, .5f, 1f);
            d.Update(1f, float.NaN);
            State(d, 1f, float.NaN, true, true, false, false, "NaN delta retains invalid timer", ref checks);
            d.Update(1f, 1f);
            State(d, 1f, float.NaN, true, true, false, false, "NaN old time does not become a pulse", ref checks);
            d.SetInput(float.NaN);
            State(d, float.NaN, float.NaN, false, true, false, false, "SetInput NaN changes only input and threshold", ref checks);
            d.Update(1f, 1f);
            State(d, 1f, 0f, true, false, false, false, "NaN input history exits before Math.Sign", ref checks);

            d = new InputDebounce(.25f, float.PositiveInfinity, 1f);
            d.Update(1f, float.PositiveInfinity);
            State(d, 1f, float.PositiveInfinity, true, true, true, false, "positive infinity exact crossing", ref checks);
            d.Update(1f, 0f);
            State(d, 1f, float.PositiveInfinity, true, true, false, false, "positive infinity one-shot ends", ref checks);
            d.Reset(false, true);
            d.SetInput(1f);
            d.Update(1f, float.PositiveInfinity);
            State(d, 1f, float.PositiveInfinity, true, true, true, false, "positive infinity continuous event", ref checks);
            d.Update(1f, 0f);
            State(d, 1f, float.PositiveInfinity, true, true, true, false, "continuous pulse differs from strict getter at infinity", ref checks);
            d = new InputDebounce(.25f, float.NegativeInfinity, 1f);
            d.Update(1f, 0f);
            State(d, 1f, 0f, true, true, false, true, "negative infinity one-shot gate false", ref checks);
            d.Reset(false, true);
            d.SetInput(1f);
            d.Update(1f, float.NegativeInfinity);
            State(d, 1f, float.NegativeInfinity, true, true, true, false, "negative infinity continuous equality", ref checks);
            d = new InputDebounce(.25f, .5f, float.PositiveInfinity);
            d.Update(float.PositiveInfinity, .5f);
            State(d, float.PositiveInfinity, .5f, true, true, true, false, "positive infinite input has positive sign", ref checks);
            d.Update(float.NegativeInfinity, .5f);
            State(d, float.NegativeInfinity, 0f, true, false, false, false, "infinite input reversal resets time", ref checks);

            d = new InputDebounce(.25f, .5f, 1f);
            d.Update(1f, float.MaxValue);
            State(d, 1f, float.MaxValue, true, true, true, true, "large delta is unclamped", ref checks);
            d.Update(1f, float.MaxValue);
            State(d, 1f, float.PositiveInfinity, true, true, false, true, "time overflow retains infinity", ref checks);
            d.Reset();
            State(d, 0f, 0f, false, false, false, false, "default Reset recovers overflow", ref checks);
            Require(!Field<bool>(d, "m_ignoreInitialInput") && !Field<bool>(d, "m_timeExceedContinuous"), "default Reset clears both mode flags", ref checks);
            Require(Field<float>(d, "m_threshold") == .25f && Field<float>(d, "m_timeMax") == .5f, "Reset leaves readonly limits intact", ref checks);
            return checks;
        }
    }
}
