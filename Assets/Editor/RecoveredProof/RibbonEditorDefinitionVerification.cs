using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Hardlight;
using UnityEngine;

namespace ProjectLucid.Editor
{
    public static class RibbonEditorDefinitionVerification
    {
        private static int count;
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Ribbon definition proof: " + message);
            ++count;
        }
        private static FieldInfo Field(Type type, string name)
        {
            return type.GetField(name, BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                ?? throw new MissingFieldException(type.FullName, name);
        }
        private static void Set(object value, Type owner, string name, object data) => Field(owner, name).SetValue(value, data);
        private static object Call(string name, Type[] parameters, params object[] args)
        {
            try { return typeof(SurfaceEditorDefinition).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic, null, parameters, null).Invoke(null, args); }
            catch (TargetInvocationException failure) { throw failure.InnerException; }
        }
        private static RibbonRenderQualitySetting Pick(RibbonEditorDefinition definition, bool selected, float distance)
        {
            try { return (RibbonRenderQualitySetting)typeof(RibbonEditorDefinition).GetMethod("GetRenderQualitySetting", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(definition, new object[] { selected, distance }); }
            catch (TargetInvocationException failure) { throw failure.InnerException; }
        }
        private static RibbonRenderQualitySetting Quality(int distance, bool selected = false)
        {
            var result = new RibbonRenderQualitySetting();
            Set(result, typeof(SplineRenderQualitySetting), "m_splineDrawDistance", distance);
            Set(result, typeof(SplineRenderQualitySetting), "m_isSelected", selected);
            return result;
        }
        private static void NullFault(Action action, string message)
        {
            bool failed = false;
            try { action(); } catch (NullReferenceException) { failed = true; }
            Check(failed, message);
        }
        private static bool Visible(Vector3 position, Vector3 forward, Vector3 point)
            => (bool)Call("IsVisible", new[] { typeof(Vector3), typeof(Vector3), typeof(Vector3) }, position, forward, point);
        private static bool CornersVisible(Bounds bounds, Vector3 position, Vector3 forward)
            => (bool)Call("HasVisibleCorners", new[] { typeof(Bounds), typeof(Vector3), typeof(Vector3) }, bounds, position, forward);

        public static int RunManaged()
        {
            count = 0;
            var single = new SingleSplineRenderQualitySettings();
            Check(single.RenderQuality == 8, "quality default eight");
            Check(single.LineThickness == 1f, "line thickness default one");
            Check(!single.DisplaySplineControlCage, "control cage default false");
            Check(single.NormalLength == 1f, "normal length one");
            Check(single.NormalThickness == 1f, "normal thickness one");
            Check(single.NormalsPerKnot == 2, "two normals per knot");
            Check(single.NormalColour == new Color(0f, 1f, 1f, 1f), "native cyan literal");
            Check(single.MaxArrowSize == 2f, "max arrow two");
            Check(single.DisplayColour == new Color(1f, 0.9215686321258545f, 0.01568627543747425f, 1f), "native yellow literal bits");
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_renderQuality", -9);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_lineThickness", -2f);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_displaySplineControlCage", true);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_normalLength", -3f);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_normalThickness", -4f);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_normalsPerKnot", -5);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_normalColour", new Color(-1, 2, 3, 4));
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_maxArrowSize", -6f);
            Set(single, typeof(SingleSplineRenderQualitySettings), "m_displayColour", new Color(5, 6, 7, 8));
            Check(single.RenderQuality == -9, "quality getter does not clamp");
            Check(single.LineThickness == -2f, "negative line thickness retained");
            Check(single.DisplaySplineControlCage, "control cage field passthrough");
            Check(single.NormalLength == -3f, "negative normal length retained");
            Check(single.NormalThickness == -4f, "negative normal thickness retained");
            Check(single.NormalsPerKnot == -5, "range attribute has no runtime clamp");
            Check(single.NormalColour == new Color(-1, 2, 3, 4), "normal colour unbounded channels");
            Check(single.MaxArrowSize == -6f, "negative arrow size retained");
            Check(single.DisplayColour == new Color(5, 6, 7, 8), "display colour unbounded channels");
            var spline = new SplineRenderQualitySetting();
            Check(!spline.IsSelected, "spline selection default false");
            Check(spline.SplineDrawDistance == 1000, "spline draw distance thousand");
            Check(!spline.DisplayKnotHandles, "handles false");
            Check(spline.KnotHeadColour == Color.white, "head white");
            Check(spline.KnotTailColour == new Color(.5f, .5f, .5f, 1f), "tail gray literal");
            Check(spline.KnotSize == 2f, "knot size two");
            Check(!spline.RenderBoundingBoxes, "bounds false");
            Check(spline.CentreSpline != null && spline.CentreSpline.RenderQuality == 8, "genuine centre quality constructed");
            var ribbon = new RibbonRenderQualitySetting();
            Check(ribbon.ShowLinesAtKnots, "lines at knots true");
            Check(ribbon.LeftSpline != null && ribbon.RightSpline != null, "both side qualities constructed");
            Check(!ReferenceEquals(ribbon.LeftSpline, ribbon.RightSpline), "separate side objects");
            Check(!ReferenceEquals(ribbon.LeftSpline, ribbon.CentreSpline), "left differs from inherited centre");
            Check(!ReferenceEquals(ribbon.RightSpline, ribbon.CentreSpline), "right differs from inherited centre");
            Set(ribbon, typeof(RibbonRenderQualitySetting), "m_leftSplineQuality", single);
            Set(ribbon, typeof(RibbonRenderQualitySetting), "m_rightSplineQuality", null);
            Set(ribbon, typeof(RibbonRenderQualitySetting), "m_showLinesAtKnots", false);
            Set(ribbon, typeof(SplineRenderQualitySetting), "m_centreSpline", null);
            Check(ReferenceEquals(ribbon.LeftSpline, single), "left getter exact reference");
            Check(ribbon.RightSpline == null, "right null retained");
            Check(!ribbon.ShowLinesAtKnots, "line toggle false retained");
            Check(ribbon.CentreSpline == null, "centre null retained");

            // This bypasses only the native ScriptableObject construction boundary. No Unity
            // object lifecycle/default-cache/Camera/GUI execution is claimed by managed checks.
            var definition = (RibbonEditorDefinition)FormatterServices.GetUninitializedObject(typeof(RibbonEditorDefinition));
            NullFault(() => Pick(definition, false, 0f), "native null quality list is not repaired");
            var list = new List<RibbonRenderQualitySetting>();
            Set(definition, typeof(RibbonEditorDefinition), "m_renderQualitySettings", list);
            var added = Pick(definition, false, 0f);
            Check(list.Count == 1, "empty list inserts one actual quality record");
            Check(ReferenceEquals(added, list[0]), "inserted default is returned");
            Check(added.SplineDrawDistance == 1000, "inserted default constructor unchanged");
            Check(Pick(definition, true, 0f) == null, "selection mismatch remains null");
            Check(list.Count == 1, "nonempty list remains unchanged");
            var near = Quality(4); var far = Quality(10); var equal = Quality(4); var selected = Quality(2, true);
            list.Clear(); list.Add(far); list.Add(near); list.Add(equal); list.Add(selected);
            Check(ReferenceEquals(Pick(definition, false, 0), near), "minimum strict covering squared distance");
            Check(ReferenceEquals(Pick(definition, false, 15), near), "inside square sixteen");
            Check(ReferenceEquals(Pick(definition, false, 16), far), "equal boundary excluded");
            Check(ReferenceEquals(Pick(definition, false, 99), far), "inside square hundred");
            Check(Pick(definition, false, 100) == null, "outer boundary excluded");
            Check(ReferenceEquals(Pick(definition, true, 0), selected), "separate selected mode");
            Check(Pick(definition, true, 4) == null, "selected exact boundary excluded");
            Check(Pick(definition, false, float.NaN) == null, "NaN distance never selected");
            Check(Pick(definition, false, float.PositiveInfinity) == null, "positive infinity never selected");
            Check(ReferenceEquals(Pick(definition, false, float.NegativeInfinity), near), "negative infinity minimum covering square");
            Check(Pick(definition, false, float.MaxValue) == null, "max value fallback no quality");
            var overflow = Quality(50000);
            list.Clear(); list.Add(overflow);
            Check(Pick(definition, false, 0) == null, "integer multiplication overflow precedes float conversion");
            Check(ReferenceEquals(Pick(definition, false, -2e9f), overflow), "negative overflow square can cover negative input");
            var negative = Quality(-4); list.Clear(); list.Add(negative);
            Check(ReferenceEquals(Pick(definition, false, 15), negative), "negative authored distance squares positive");
            Check(Pick(definition, false, 16) == null, "negative distance square strict bound");
            var zero = Quality(0); list.Clear(); list.Add(zero);
            Check(Pick(definition, false, 0) == null, "zero distance excludes zero input");
            Check(ReferenceEquals(Pick(definition, false, -1), zero), "zero square covers negative distance");
            list.Clear(); list.Add(null);
            NullFault(() => Pick(definition, false, 0), "null quality element is not skipped");
            list.Clear(); list.Add(near); list.Add(null);
            NullFault(() => Pick(definition, false, 0), "iteration does not stop after first candidate");

            var bounds = new Bounds(new Vector3(1, 2, 3), new Vector3(4, 6, 8));
            Vector3[] corners = (Vector3[])Call("GetBoundsCorners", new[] { typeof(Bounds) }, bounds);
            Vector3[] expected = { new Vector3(-1,-1,-1),new Vector3(-1,5,-1),new Vector3(3,5,-1),new Vector3(3,-1,-1),new Vector3(3,-1,7),new Vector3(-1,-1,7),new Vector3(-1,5,7),new Vector3(3,5,7) };
            Check(corners.Length == 8, "eight ordered bounds corners");
            for (int i = 0; i < 8; ++i) Check(corners[i] == expected[i], "bounds corner " + i);
            Vector3[] again = (Vector3[])Call("GetBoundsCorners", new[] { typeof(Bounds) }, bounds);
            Check(!ReferenceEquals(corners, again), "fresh bounds corner array");
            Check(Visible(Vector3.zero, Vector3.forward, new Vector3(0,0,5)), "point in front visible");
            Check(!Visible(Vector3.zero, Vector3.forward, new Vector3(0,0,-5)), "point behind hidden");
            Check(!Visible(Vector3.zero, Vector3.forward, Vector3.zero), "zero delta strict dot false");
            Check(!Visible(Vector3.zero, Vector3.forward, Vector3.right), "orthogonal point hidden");
            Check(Visible(Vector3.zero, -Vector3.forward, new Vector3(0,0,-5)), "reversed camera direction");
            Check(Visible(Vector3.zero, Vector3.forward, new Vector3(0,0,1e-4f)), "normalization above epsilon");
            Check(!Visible(Vector3.zero, Vector3.forward, new Vector3(0,0,1e-7f)), "sub-epsilon normalization zero");
            Check(!Visible(Vector3.zero, Vector3.forward, new Vector3(float.NaN,0,5)), "shared normalization and strict NaN dot");
            Check(CornersVisible(new Bounds(new Vector3(0,0,4), Vector3.one), Vector3.zero, Vector3.forward), "front bounds visible corners");
            Check(!CornersVisible(new Bounds(new Vector3(0,0,-4), Vector3.one), Vector3.zero, Vector3.forward), "rear bounds hidden corners");
            Check(CornersVisible(new Bounds(Vector3.zero, new Vector3(2,2,2)), Vector3.zero, Vector3.forward), "crossing bounds has visible front corners");
            Check(!CornersVisible(new Bounds(Vector3.zero, Vector3.zero), Vector3.zero, Vector3.forward), "degenerate corners invisible");
            return count;
        }

        public static int RunEngine()
        {
            count = 0;
            var cached = Field(typeof(RibbonEditorDefinition), "s_default");
            object old = cached.GetValue(null);
            RibbonEditorDefinition definition = null;
            Material material = null;
            try
            {
                definition = ScriptableObject.CreateInstance<RibbonEditorDefinition>();
                Check(definition != null, "genuine concrete definition creates in Unity");
                Check(definition.AutoLengthUpdateInterval == .5f, "native scriptable constructor interval");
                Check(definition.GeneratedMeshQuality == 1, "native generated mesh quality");
                Check(definition.GeneratedMeshesSaveDelaySeconds == 1f, "native generated save delay");
                Check(definition.GeneratedMeshesLocation == null, "uninitialized location remains null before serialization");
                Check(!definition.RenderWithDepthTest, "depth test default false");
                Check(!definition.DisplayAdjacentBlockKnotPositionCalculation && !definition.DisplayNearestBlockKnotPositionCalculation, "debug toggles default false");
                Check(definition.VisualisationMaterial == null, "native material default null");
                Check(Field(typeof(RibbonEditorDefinition), "m_renderQualitySettings").GetValue(definition) == null, "native ctor does not synthesize list");
                cached.SetValue(null, definition);
                Check(ReferenceEquals(RibbonEditorDefinition.Default, definition), "static default cache returns actual object");
                Set(definition, typeof(RibbonEditorDefinition), "m_disableRendering", true);
                RibbonRenderQualitySetting result = Quality(5);
                Check(!RibbonEditorDefinition.ShouldRender(Vector3.zero, false, out result) && result == null, "disabled point clears out value without camera/list access");
                result = Quality(5);
                Check(!RibbonEditorDefinition.ShouldRender(new Bounds(), false, out result) && result == null, "disabled bounds clears out value");
                result = Quality(5);
                Check(!RibbonEditorDefinition.ShouldRender(new Bounds(), false, out result, Matrix4x4.zero) && result == null, "disabled matrix wrapper short circuits transformation");
                Set(definition, typeof(RibbonEditorDefinition), "m_disableRendering", false);
                var list = new List<RibbonRenderQualitySetting> { Quality(4) };
                Set(definition, typeof(RibbonEditorDefinition), "m_renderQualitySettings", list);
                Check(ReferenceEquals(Pick(definition, false, 15), list[0]), "actual Unity object quality lookup");
                Shader shader = Shader.Find("Hidden/InternalErrorShader");
                Check(shader != null, "real installed internal shader for material serialization");
                material = new Material(shader);
                Set(definition, typeof(RibbonEditorDefinition), "m_visualisationMaterial", material);
                Check(ReferenceEquals(definition.VisualisationMaterial, material), "actual Material reference getter");
                Set(definition, typeof(RibbonEditorDefinition), "m_autoLengthUpdateInterval", -7f);
                Set(definition, typeof(RibbonEditorDefinition), "m_generatedMeshQuality", 0);
                Set(definition, typeof(RibbonEditorDefinition), "m_generatedMeshesLocation", "Original/Subfolder");
                Set(definition, typeof(RibbonEditorDefinition), "m_generatedMeshesSaveDelaySeconds", -3f);
                Set(definition, typeof(RibbonEditorDefinition), "m_renderWithDepthTest", true);
                Set(definition, typeof(RibbonEditorDefinition), "m_displayAdjacentBlockKnotPositionCalculation", true);
                Set(definition, typeof(RibbonEditorDefinition), "m_displayNearestBlockKnotPositionCalculation", true);
                string json = JsonUtility.ToJson(definition);
                Check(json.Contains("m_autoLengthUpdateInterval") && json.Contains("m_renderQualitySettings"), "real Unity serializer includes private original fields");
                var copy = ScriptableObject.CreateInstance<RibbonEditorDefinition>();
                try
                {
                    JsonUtility.FromJsonOverwrite(json, copy);
                    Check(copy.AutoLengthUpdateInterval == -7f, "negative interval JSON roundtrip");
                    Check(copy.GeneratedMeshQuality == 0, "range attribute not JSON runtime clamp");
                    Check(copy.GeneratedMeshesLocation == "Original/Subfolder", "path JSON roundtrip");
                    Check(copy.GeneratedMeshesSaveDelaySeconds == -3f, "negative delay JSON roundtrip");
                    Check(copy.RenderWithDepthTest, "protected depth flag JSON roundtrip");
                    Check(copy.DisplayAdjacentBlockKnotPositionCalculation && copy.DisplayNearestBlockKnotPositionCalculation, "debug flags JSON roundtrip");
                    var restored = (List<RibbonRenderQualitySetting>)Field(typeof(RibbonEditorDefinition), "m_renderQualitySettings").GetValue(copy);
                    Check(restored != null && restored.Count == 1, "typed list JSON roundtrip");
                    Check(restored[0].SplineDrawDistance == 4, "inherited private quality graph JSON");
                    Check(restored[0].LeftSpline != null && restored[0].RightSpline != null && restored[0].CentreSpline != null, "nested full quality records JSON");
                    Check(restored[0].CentreSpline.DisplayColour == Color.yellow, "nested native colour JSON");
                }
                finally { UnityEngine.Object.DestroyImmediate(copy); }
                return count;
            }
            finally
            {
                try { cached.SetValue(null, old); }
                finally
                {
                    try { if (material != null) UnityEngine.Object.DestroyImmediate(material); }
                    finally { if (definition != null) UnityEngine.Object.DestroyImmediate(definition); }
                }
            }
        }

        // Must be invoked inside a real Unity OnGUI callback; no cached/fabricated GUI skin.
        public static int RunGUI(SurfaceEditorDefinition definition)
        {
            count = 0;
            GUIStyle button = definition.ButtonStyle;
            Check(button != null && !ReferenceEquals(button, GUI.skin.button), "button skin style copied");
            Check(!ReferenceEquals(button, definition.ButtonStyle), "button copy refreshed each access");
            GUIStyle label = definition.LabelStyle;
            Check(!ReferenceEquals(label, GUI.skin.label), "label skin style copied");
            Check(!ReferenceEquals(label, definition.LabelStyle), "label fresh each access");
            Check(label.fontStyle == FontStyle.Bold, "label bold");
            Check(label.normal.textColor == Color.grey, "label gray");
            GUIStyle warning = definition.LabelWarningStyle;
            Check(!ReferenceEquals(warning, GUI.skin.label), "warning skin style copied");
            Check(!ReferenceEquals(warning, definition.LabelWarningStyle), "warning fresh each access");
            Check(warning.fontStyle == FontStyle.Bold, "warning bold");
            Check(warning.normal.textColor == Color.yellow, "warning yellow");
            return count;
        }
    }
}
