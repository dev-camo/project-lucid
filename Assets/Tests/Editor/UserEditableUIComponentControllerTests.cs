using System;
using System.Globalization;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using ProjectLucid.Tests.OnScreenControlsShared;
using UnityEngine;
using UnityEngine.Events;
using static ProjectLucid.Tests.CharacterTriggerTestSupport;
using static ProjectLucid.Tests.OnScreenControlTestServices;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // UserEditableUIComponentController (02000995) against its 1.10.1 x86-64 bodies (0x697AA0..0x6988A0, GetPositionInfo
    // 0x68CDE0). The controller moves a component inside a movement area. Both are real RectTransforms on owned
    // inactive hosts: the area is 100 x 50 with its pivot (bottom left) at the origin, and the component is 20 x 10
    // with a centred pivot at (50, 25), so its bottom left is (40, 20), its free space is (80, 40) and its start scale
    // (width over area width) is 0.2. The expected positions and scales are worked out from the native formulas with
    // these numbers; the engine supplies only the world corners, so positions allow 1e-4. The save data, message
    // manager and GUICameraManager singleton are the real ones (OnScreenControlTestServices). Not covered: the
    // controller driven by Unity's own Update, and Flip components with a real Graphic.
    public sealed class UserEditableUIComponentControllerTests
    {
        private const string SaveId = "Lucid test control";
        private const float Tolerance = 1e-4f;

        internal sealed class Rig
        {
            internal Services Services;
            internal UserEditableUIComponentControllerProbe Controller;
            internal OnScreenEditableComponentProbe Component;
            internal RectTransform Bounds;
            internal RectTransform Rect;
            internal SaveDataSettings Settings;
            internal Camera MenuCamera;
        }

        private static RectTransform NewRect(Owned owned, string name, Vector2 size, Vector2 pivot, Vector3 position)
        {
            GameObject host = owned.Track(new GameObject(name, typeof(RectTransform)));
            host.SetActive(false);
            var rect = (RectTransform)host.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.pivot = pivot;
            rect.position = position;
            return rect;
        }

        private static void SetLayoutDefaults(Owned owned, params (string saveId, Vector2 position, float scale)[] defaults)
        {
            var configuration = owned.Track(ScriptableObject.CreateInstance<TouchLayoutConfiguration>());
            var rows = new TouchLayoutConfiguration.TouchControlDefault[defaults.Length];
            for (int i = 0; i < defaults.Length; i++)
            {
                rows[i] = new TouchLayoutConfiguration.TouchControlDefault();
                Set(rows[i], "m_saveId", defaults[i].saveId); // serialized in the original
                Set(rows[i], "m_position", defaults[i].position);
                Set(rows[i], "m_scale", defaults[i].scale);
            }
            Set(configuration, "m_controlDefaults", rows);
            // DataManager.Awake (the only setter) builds every definition lookup; the case supplies this one.
            typeof(DataManager).GetProperty("TouchLayoutConfiguration").GetSetMethod(true)
                .Invoke(ProcessManager.GetSystem<DataManager>(), new object[] { configuration });
        }

        internal static void WithController(Action<Rig> body, params (string saveId, Vector2 position, float scale)[] layoutDefaults) => With(services =>
        {
            Owned owned = services.Owned;
            var rig = new Rig { Services = services, Settings = new SaveDataSettings() };
            Set(services.Save, "m_saveSettings", rig.Settings); // loaded by the save flow in the original
            SetLayoutDefaults(owned, layoutDefaults);

            // OnSaveManagerValid reads the GUICameraManager singleton, which only its own Awake publishes. The host is
            // not tracked: DestroyInstance (the singleton's own teardown) destroys it after unregistering the system.
            owned.LeaseRow<GUICameraManager>();
            var guiHost = new GameObject("Lucid owned gui camera manager");
            guiHost.SetActive(false);
            var gui = guiHost.AddComponent<GUICameraManager>();
            rig.MenuCamera = owned.Host("Lucid owned menu camera", false).AddComponent<Camera>();
            Set(gui, "m_menuCamera", rig.MenuCamera); // serialized in the original
            Call(gui, "Awake");
            owned.Lease(new ReleaseAction(() => gui.DestroyInstance()));

            rig.Bounds = NewRect(owned, "Lucid owned movement bounds", new Vector2(100f, 50f), Vector2.zero, Vector3.zero);
            rig.Rect = NewRect(owned, "Lucid owned editable component", new Vector2(20f, 10f), new Vector2(0.5f, 0.5f), new Vector3(50f, 25f, 0f));
            rig.Component = owned.Host("Lucid owned editable component behaviour", false).AddComponent<OnScreenEditableComponentProbe>();
            rig.Component.Rect = rig.Rect;
            rig.Controller = owned.Host("Lucid owned editable controller", false).AddComponent<UserEditableUIComponentControllerProbe>();
            Set(rig.Controller, "m_saveId", SaveId); // serialized in the original
            Set(rig.Controller, "m_movementBoundsRectTransform", rig.Bounds);
            Set(rig.Controller, "m_editableUIComponent", rig.Component);
            rig.Controller.RunOnSaveManagerValid(services.Save);
            body(rig);
        });

        private static SaveDataSettingsEditableUIComponent SavedData(Rig rig) => Get<SaveDataSettingsEditableUIComponent>(rig.Controller, "m_saveDataComponent");

        private static void AssertWorldPosition(Rig rig, float x, float y, string label = null)
        {
            Vector3 position = rig.Rect.position;
            Assert.That(position.x, Is.EqualTo(x).Within(Tolerance), label + " x");
            Assert.That(position.y, Is.EqualTo(y).Within(Tolerance), label + " y");
            Assert.That(position.z, Is.EqualTo(0f).Within(Tolerance), label + " z");
        }

        private static void AssertFloat(Rig rig, string field, float expected) =>
            Assert.That(Get<float>(rig.Controller, field), Is.EqualTo(expected).Within(Tolerance), field);

        private static void AssertNormalised(Rig rig, string field, float x, float y)
        {
            var value = Get<Vector2>(rig.Controller, field);
            Assert.That(value.x, Is.EqualTo(x).Within(Tolerance), field + " x");
            Assert.That(value.y, Is.EqualTo(y).Within(Tolerance), field + " y");
        }

        // OnSaveManagerValid 0x697AA0: the base stores the settings, then the interface (as IUserEditableUIComponent)
        // and the menu camera of the GUICameraManager singleton are read, and the settings hand out the saved
        // component data for the save id.
        [Test]
        public void SaveManagerValidFindsTheInterfaceTheMenuCameraAndTheSavedComponent()
        {
            WithController(rig =>
            {
                Assert.That(rig.Controller.Settings, Is.SameAs(rig.Settings));
                Assert.That(Get<IUserEditableUIComponent>(rig.Controller, "m_editableUIComponentInterface"), Is.SameAs(rig.Component));
                Assert.That(Get<Camera>(rig.Controller, "m_menuCamera"), Is.SameAs(rig.MenuCamera));
                Assert.That(rig.Settings.EditableUIComponents.Count, Is.EqualTo(1));
                Assert.That(SavedData(rig), Is.SameAs(rig.Settings.EditableUIComponents[0]));
                Assert.That(SavedData(rig).ID, Is.EqualTo(SaveId));
            });
        }

        // The constructor 0x6988A0 writes 0.05f and 0.5f (0x29A7B10) as the scale limits.
        [Test]
        public void ScaleLimitsStartAtPointZeroFiveAndPointFive()
        {
            WithController(rig =>
            {
                Assert.That(Get<float>(rig.Controller, "m_scaleMin"), Is.EqualTo(0.05f));
                Assert.That(Get<float>(rig.Controller, "m_scaleMax"), Is.EqualTo(0.5f));
                Assert.That(Get<Vector3[]>(rig.Controller, "m_temporaryCornersArray").Length, Is.EqualTo(4));
            });
        }

        // DoInitialise 0x698110 with no listed default: the default scale is component width over area width
        // (20 / 100), the default position is the component's bottom-left offset over the free space ((40, 20) / (80, 40)).
        // With no saved placement the component is set to those values, which leaves it where it was.
        [Test]
        public void InitialiseKeepsThePrefabPlacementWithoutAListedDefault()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();

                AssertFloat(rig, "m_defaultScale", 0.2f);
                AssertNormalised(rig, "m_defaultPositionNormalised", 0.5f, 0.5f);
                AssertFloat(rig, "m_currentScale", 0.2f);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.5f, 0.5f);
                AssertFloat(rig, "m_movableWidth", 80f);
                AssertFloat(rig, "m_movableHeight", 40f);
                AssertWorldPosition(rig, 50f, 25f);
                Assert.That(rig.Component.TransformUpdatedCount, Is.EqualTo(1));
                Assert.That(SavedData(rig).UserSet, Is.False);
            });
        }

        // The listed default for this save id replaces the measured one (other ids are skipped). The current scale is
        // set to it before placing, so the component is moved but not rescaled (the scale already matches).
        [Test]
        public void InitialiseUsesTheListedDefaultForItsSaveId()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();

                AssertFloat(rig, "m_defaultScale", 0.3f);
                AssertNormalised(rig, "m_defaultPositionNormalised", 0.25f, 0.75f);
                AssertFloat(rig, "m_currentScale", 0.3f);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.25f, 0.75f);
                AssertWorldPosition(rig, 30f, 35f);
                Assert.That(rig.Rect.localScale, Is.EqualTo(Vector3.one), "not rescaled");
            }, ("Lucid other control", new Vector2(0.9f, 0.9f), 0.4f), (SaveId, new Vector2(0.25f, 0.75f), 0.3f));
        }

        // A saved placement the user set wins over the defaults: scale 0.4 differs from the start scale, so the component
        // is rescaled by 100 * 0.4 / 20 = 2 about its pivot (a 40 x 20 rectangle with bottom left (30, 15)), the free
        // space becomes (60, 30) and the position (0.5, 0.25) lands the bottom left at (30, 7.5).
        [Test]
        public void InitialisePlacesTheSavedComponentAndRescalesIt()
        {
            WithController(rig =>
            {
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                saved.Position = new Vector2(0.5f, 0.25f);
                saved.Scale = 0.4f;
                saved.UserSet = true;

                rig.Controller.RunDoInitialise();

                AssertFloat(rig, "m_currentScale", 0.4f);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.5f, 0.25f);
                Assert.That(rig.Rect.localScale.x, Is.EqualTo(2f).Within(Tolerance));
                Assert.That(rig.Rect.localScale.y, Is.EqualTo(2f).Within(Tolerance));
                Assert.That(rig.Rect.localScale.z, Is.EqualTo(2f).Within(Tolerance));
                var componentRect = Get<Rect>(rig.Controller, "m_componentRect");
                Assert.That(componentRect.x, Is.EqualTo(30f).Within(Tolerance));
                Assert.That(componentRect.y, Is.EqualTo(15f).Within(Tolerance));
                Assert.That(componentRect.width, Is.EqualTo(40f).Within(Tolerance));
                Assert.That(componentRect.height, Is.EqualTo(20f).Within(Tolerance));
                AssertFloat(rig, "m_movableWidth", 60f);
                AssertFloat(rig, "m_movableHeight", 30f);
                AssertWorldPosition(rig, 50f, 17.5f);
            });
        }

        // The scale is clamped into [m_scaleMin, m_scaleMax] and the position into the unit square (a minss then a cmpltss select at
        // 0x697E7E..0x697E90 for Mathf.Clamp, and minps/cmpnltps at 0x697FCB for Clamp01 per axis); a position of (-1, 2) therefore lands at (0, 1).
        [Test]
        public void ScaleAndPositionAreClamped()
        {
            WithController(rig =>
            {
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                saved.Position = new Vector2(-1f, 2f);
                saved.Scale = 0.9f;
                saved.UserSet = true;
                rig.Controller.RunDoInitialise();
                AssertFloat(rig, "m_currentScale", 0.5f);
                AssertNormalised(rig, "m_currentPositionNormalised", 0f, 1f);

                saved.Scale = 0.01f;
                saved.Position = new Vector2(2f, -1f);
                rig.Controller.RunDoInitialise();
                AssertFloat(rig, "m_currentScale", 0.05f);
                AssertNormalised(rig, "m_currentPositionNormalised", 1f, 0f);
            });
        }

        // The bounds' bottom left is the origin of the normalised space. With the bounds at (10, 5) and the component's
        // centre at (60, 30) (bottom left (50, 25)): DoInitialise subtracts the origin (subss 0x698195, 0x6981BB), so the
        // default is ((50 - 10) / 80, (25 - 5) / 40) = (0.5, 0.5) rather than 0.625; DoUpdate subtracts it from the touch
        // (subps 0x697CC7), so touch (70, 35) gives (70 - 10 - 10) / 80 = 0.625 and (35 - 5 - 5) / 40 = 0.625; and
        // SetComponentNormalisedPositionAndScale adds it back (addps 0x697FEE): 0.625 * 80 + 10 + 10 = 70, 0.625 * 40 + 5 + 5 = 35.
        [Test]
        public void BoundsAwayFromTheOriginOffsetTheNormalisedPosition()
        {
            WithController(rig =>
            {
                rig.Bounds.position = new Vector3(10f, 5f, 0f);
                rig.Rect.position = new Vector3(60f, 30f, 0f);

                rig.Controller.RunDoInitialise();
                AssertNormalised(rig, "m_defaultPositionNormalised", 0.5f, 0.5f);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.5f, 0.5f);
                AssertWorldPosition(rig, 60f, 30f);

                rig.Component.HasTouch = true;
                rig.Component.Touch = new Vector2(70f, 35f);
                rig.Controller.RunDoUpdate();
                AssertNormalised(rig, "m_currentPositionNormalised", 0.625f, 0.625f);
                AssertWorldPosition(rig, 70f, 35f);
            });
        }

        // The pivot offset is (pivot.x * width, pivot.y * height) (mulps 0x698017). With pivot (0.25, 0.75) on the 20 x 10
        // component whose bottom left is (50, 25), the position is (50 + 5, 25 + 7.5); swapped axes would give (65, 27.5).
        // The same touch as above moves the bottom left to (60, 30) and so the position to (65, 37.5).
        [Test]
        public void AnOffCentrePivotOffsetsTheWorldPositionPerAxis()
        {
            WithController(rig =>
            {
                rig.Bounds.position = new Vector3(10f, 5f, 0f);
                rig.Rect.pivot = new Vector2(0.25f, 0.75f);
                rig.Rect.position = new Vector3(55f, 32.5f, 0f);

                rig.Controller.RunDoInitialise();
                AssertNormalised(rig, "m_defaultPositionNormalised", 0.5f, 0.5f);
                AssertWorldPosition(rig, 55f, 32.5f);

                rig.Component.HasTouch = true;
                rig.Component.Touch = new Vector2(70f, 35f);
                rig.Controller.RunDoUpdate();
                AssertWorldPosition(rig, 65f, 37.5f);
            });
        }

        // The rescale is gated by !Mathf.Approximately(m_currentScale, scale) (0x697EC7..0x697F11): one ulp above 0.2f
        // (0.20000002f, a difference of 1.5e-8 against a tolerance of 2e-7) counts as equal, so the scale and the local
        // scale are left bit for bit as they were.
        [Test]
        public void ANearlyEqualScaleDoesNotRescaleTheComponent()
        {
            WithController(rig =>
            {
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                saved.Position = new Vector2(0.5f, 0.5f);
                saved.Scale = 0.20000002f;
                saved.UserSet = true;

                rig.Controller.RunDoInitialise();

                Assert.That(Get<float>(rig.Controller, "m_currentScale"), Is.EqualTo(0.2f), "exactly, not within a tolerance");
                Assert.That(rig.Rect.localScale.x, Is.EqualTo(1f));
                Assert.That(rig.Rect.localScale.y, Is.EqualTo(1f));
                Assert.That(rig.Rect.localScale.z, Is.EqualTo(1f));
            });
        }

        // DoInitialise guards its divisions (ucomiss at 0x698160, 0x698186, 0x6981AC). A component that fills the 20 x 10
        // bounds leaves no free space, so the default position is exactly (0, 0) and not 0 / 0. The default scale is
        // 20 / 20 = 1, which is clamped to 0.5 on placement: the component is halved to 10 x 5 about its pivot and put at the
        // bounds' bottom left, centre (5, 2.5). The free space is only measured again after that rescale.
        [Test]
        public void ADefaultPositionWithNoFreeSpaceIsZeroNotNaN()
        {
            WithController(rig =>
            {
                rig.Bounds.sizeDelta = new Vector2(20f, 10f);
                rig.Rect.position = new Vector3(10f, 5f, 0f);

                rig.Controller.RunDoInitialise();

                var defaultPosition = Get<Vector2>(rig.Controller, "m_defaultPositionNormalised");
                Assert.That((defaultPosition.x, defaultPosition.y), Is.EqualTo((0f, 0f)));
                AssertFloat(rig, "m_defaultScale", 1f);
                AssertFloat(rig, "m_currentScale", 0.5f);
                AssertFloat(rig, "m_movableWidth", 10f);
                AssertFloat(rig, "m_movableHeight", 5f);
                AssertWorldPosition(rig, 5f, 2.5f);
            });
        }

        // DoUpdate 0x697C00: the component's centre follows the touch. Touch (60, 30) less the area origin, less half the
        // component, over the free space is (50, 25) / (80, 40) = (0.625, 0.625), and a centred pivot puts the centre at
        // the touch. The placement is saved and marked as set by the user. Without a touch nothing changes.
        [Test]
        public void UpdateFollowsTheTouchAndSavesThePlacement()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();
                rig.Component.HasTouch = false;
                rig.Component.Touch = new Vector2(60f, 30f);
                rig.Controller.RunDoUpdate();
                AssertWorldPosition(rig, 50f, 25f, "no touch");
                Assert.That(SavedData(rig).UserSet, Is.False);

                rig.Component.HasTouch = true;
                rig.Controller.RunDoUpdate();

                AssertNormalised(rig, "m_currentPositionNormalised", 0.625f, 0.625f);
                AssertWorldPosition(rig, 60f, 30f);
                Assert.That(Get<bool>(rig.Controller, "m_pinchActive"), Is.False);
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                Assert.That(saved.Position.x, Is.EqualTo(0.625f).Within(Tolerance));
                Assert.That(saved.Position.y, Is.EqualTo(0.625f).Within(Tolerance));
                Assert.That(saved.Scale, Is.EqualTo(0.2f).Within(Tolerance));
                Assert.That(saved.UserSet, Is.True);
            });
        }

        // The pinch ratio scales from the scale at the start of the pinch: 40 / 20 doubles 0.2 to 0.4 (and rescales the
        // component), a later 30 / 20 gives 0.3 from the remembered 0.2 rather than 0.6 from the current 0.4, and when the
        // pinch ends the scale stays where it is.
        [Test]
        public void PinchScalesRelativeToTheScaleAtPinchBegin()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();
                rig.Component.HasTouch = true;
                rig.Component.Touch = new Vector2(60f, 30f);
                rig.Component.HasPinch = true;
                rig.Component.PinchDistance = 40f;
                rig.Component.InitialPinchDistance = 20f;

                rig.Controller.RunDoUpdate();

                Assert.That(Get<bool>(rig.Controller, "m_pinchActive"), Is.True);
                AssertFloat(rig, "m_scaleAtPinchBegin", 0.2f);
                AssertFloat(rig, "m_currentScale", 0.4f);
                Assert.That(rig.Rect.localScale.x, Is.EqualTo(2f).Within(Tolerance));
                AssertNormalised(rig, "m_currentPositionNormalised", 0.625f, 0.625f);
                AssertWorldPosition(rig, 57.5f, 28.75f);

                rig.Component.PinchDistance = 30f;
                rig.Controller.RunDoUpdate();
                AssertFloat(rig, "m_scaleAtPinchBegin", 0.2f);
                AssertFloat(rig, "m_currentScale", 0.3f);

                rig.Component.HasPinch = false;
                rig.Controller.RunDoUpdate();
                Assert.That(Get<bool>(rig.Controller, "m_pinchActive"), Is.False);
                AssertFloat(rig, "m_currentScale", 0.3f);
            });
        }

        // OnEditStart 0x6984E0: the base enters edit mode, the saved component is cloned as the backup on the first
        // start (and copied into that same backup later), the component is told editing began, then the event fires.
        // OnEditCancel 0x6985F0 restores the backup, places the component by the restored data (unset: the defaults)
        // and leaves edit mode through ExitEditMode 0x698760, which tells the component and fires the end event.
        [Test]
        public void EditStartBacksUpTheSettingsAndCancelRestoresThem()
        {
            WithController(rig =>
            {
                int started = 0;
                int ended = 0;
                var onStart = new UnityEvent();
                onStart.AddListener(() => started++);
                var onEnd = new UnityEvent();
                onEnd.AddListener(() => ended++);
                Set(rig.Controller, "m_onEditStart", onStart); // serialized in the original
                Set(rig.Controller, "m_onEditEnd", onEnd);
                rig.Controller.RunDoInitialise();
                rig.Component.HasTouch = true;
                rig.Component.Touch = new Vector2(60f, 30f);

                rig.Controller.RunEditStart();
                Assert.That(rig.Controller.EditMode, Is.True);
                Assert.That(rig.Controller.EditModeActive.Value, Is.True);
                Assert.That(rig.Component.EditingCalls, Is.EqualTo(new[] { true }));
                Assert.That(started, Is.EqualTo(1));
                var backup = Get<SaveDataSettingsEditableUIComponent>(rig.Controller, "m_saveDataComponentBackupBeforeEdit");
                Assert.That(backup, Is.Not.Null.And.Not.SameAs(SavedData(rig)));
                Assert.That(backup.UserSet, Is.False);

                rig.Controller.RunDoUpdate();
                Assert.That(SavedData(rig).UserSet, Is.True);
                AssertWorldPosition(rig, 60f, 30f);

                rig.Controller.RunEditCancel();
                Assert.That(SavedData(rig).UserSet, Is.False, "the backup is restored");
                AssertWorldPosition(rig, 50f, 25f, "back at the default placement");
                Assert.That(rig.Controller.EditMode, Is.False);
                Assert.That(rig.Controller.EditModeActive.Value, Is.False);
                Assert.That(rig.Component.EditingCalls, Is.EqualTo(new[] { true, false }));
                Assert.That(ended, Is.EqualTo(1));
            });
        }

        [Test]
        public void ALaterEditStartRefreshesTheSameBackup()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();
                rig.Controller.RunEditStart();
                var backup = Get<SaveDataSettingsEditableUIComponent>(rig.Controller, "m_saveDataComponentBackupBeforeEdit");
                SavedData(rig).Scale = 0.4f;
                SavedData(rig).UserSet = true;

                rig.Controller.RunEditStart();

                Assert.That(Get<SaveDataSettingsEditableUIComponent>(rig.Controller, "m_saveDataComponentBackupBeforeEdit"), Is.SameAs(backup));
                Assert.That(backup.Scale, Is.EqualTo(0.4f));
                Assert.That(backup.UserSet, Is.True);
            });
        }

        // OnEditCancel with a user-set backup places the component by the saved position and scale, not the defaults.
        [Test]
        public void CancelPlacesTheComponentByTheRestoredUserSettings()
        {
            WithController(rig =>
            {
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                saved.Position = new Vector2(0.5f, 0.25f);
                saved.Scale = 0.4f;
                saved.UserSet = true;
                rig.Controller.RunDoInitialise();
                rig.Controller.RunEditStart();
                saved.Position = new Vector2(1f, 1f);

                rig.Controller.RunEditCancel();

                Assert.That(saved.Position, Is.EqualTo(new Vector2(0.5f, 0.25f)));
                AssertNormalised(rig, "m_currentPositionNormalised", 0.5f, 0.25f);
                AssertWorldPosition(rig, 50f, 17.5f);
            });
        }

        // OnEditFlip 0x6986F0: the base toggles the flip, the horizontal position becomes 1 - x (the component is not
        // moved yet), and the Flip components follow. UpdateAfterParentFlipped 0x698830 then places the component at the
        // mirrored position, saves it, and marks the data user-set because the layout is flipped.
        [Test]
        public void FlipMirrorsThePositionAndLaterMovesTheComponent()
        {
            WithController(rig =>
            {
                var flip = rig.Services.Owned.Host("Lucid owned flip", false).AddComponent<Flip>();
                Set(rig.Controller, "m_flips", new[] { flip }); // serialized in the original
                rig.Controller.RunDoInitialise();
                Assert.That(Get<bool>(flip, "flipHorizontal"), Is.False);

                rig.Controller.RunEditFlip();

                Assert.That(rig.Controller.Flipped, Is.True);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.75f, 0.75f);
                AssertWorldPosition(rig, 30f, 35f, "not moved by the flip itself");
                Assert.That(Get<bool>(flip, "flipHorizontal"), Is.True);
                int updates = rig.Component.TransformUpdatedCount;

                rig.Controller.UpdateAfterParentFlipped();

                AssertWorldPosition(rig, 70f, 35f);
                Assert.That(rig.Component.TransformUpdatedCount, Is.EqualTo(updates + 1));
                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                Assert.That(saved.Position.x, Is.EqualTo(0.75f).Within(Tolerance));
                Assert.That(saved.Position.y, Is.EqualTo(0.75f).Within(Tolerance));
                Assert.That(saved.Scale, Is.EqualTo(0.3f).Within(Tolerance));
                Assert.That(saved.UserSet, Is.True);
            }, (SaveId, new Vector2(0.25f, 0.75f), 0.3f));
        }

        [Test]
        public void UpdateAfterParentFlippedSavesWithoutMarkingAnUnflippedLayoutAsUserSet()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();

                rig.Controller.UpdateAfterParentFlipped();

                SaveDataSettingsEditableUIComponent saved = SavedData(rig);
                Assert.That(saved.Position.x, Is.EqualTo(0.5f).Within(Tolerance));
                Assert.That(saved.Position.y, Is.EqualTo(0.5f).Within(Tolerance));
                Assert.That(saved.Scale, Is.EqualTo(0.2f).Within(Tolerance));
                Assert.That(saved.UserSet, Is.False);
            });
        }

        // OnEditReset 0x698670: the base clears the flip, the user-set mark is cleared, the Flip components follow, and
        // the component returns to the default placement (here (0.25, 0.75) at 0.3).
        [Test]
        public void ResetRestoresTheDefaultPlacementAndClearsTheFlip()
        {
            WithController(rig =>
            {
                var flip = rig.Services.Owned.Host("Lucid owned flip", false).AddComponent<Flip>();
                Set(rig.Controller, "m_flips", new[] { flip }); // serialized in the original
                rig.Controller.RunDoInitialise();
                rig.Component.HasTouch = true;
                rig.Component.Touch = new Vector2(60f, 30f);
                rig.Controller.RunDoUpdate();
                rig.Controller.RunEditFlip();
                Assert.That(Get<bool>(flip, "flipHorizontal"), Is.True);
                Assert.That(SavedData(rig).UserSet, Is.True);

                rig.Controller.RunEditReset();

                Assert.That(rig.Controller.Flipped, Is.False);
                Assert.That(SavedData(rig).UserSet, Is.False);
                Assert.That(Get<bool>(flip, "flipHorizontal"), Is.False);
                AssertNormalised(rig, "m_currentPositionNormalised", 0.25f, 0.75f);
                AssertWorldPosition(rig, 30f, 35f);
            }, (SaveId, new Vector2(0.25f, 0.75f), 0.3f));
        }

        // GetPositionInfo 0x68CDE0 appends "{id} pos: {x, y} scale s" with a line break. Floats print with the current
        // culture, which the case sets to the invariant one and restores.
        [Test]
        public void PositionInfoNamesTheControlItsPositionAndScale()
        {
            WithController(rig =>
            {
                rig.Controller.RunDoInitialise();
                CultureInfo previous = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                    var builder = new System.Text.StringBuilder("first" + Environment.NewLine);
                    rig.Controller.GetPositionInfo(ref builder);
                    Assert.That(builder.ToString(), Is.EqualTo("first" + Environment.NewLine + SaveId + " pos: {0.25, 0.75} scale 0.3" + Environment.NewLine));
                }
                finally
                {
                    CultureInfo.CurrentCulture = previous;
                }
            }, (SaveId, new Vector2(0.25f, 0.75f), 0.3f));
        }

        // ExitEditMode 0x698760 is also what the base calls when the stored edit flag is off (Initialise): the component
        // is told editing ended and the end event fires.
        [Test]
        public void ExitEditModeTellsTheComponentAndFiresTheEndEvent()
        {
            WithController(rig =>
            {
                int ended = 0;
                var onEnd = new UnityEvent();
                onEnd.AddListener(() => ended++);
                Set(rig.Controller, "m_onEditEnd", onEnd); // serialized in the original
                rig.Controller.RunExitEditMode();
                Assert.That(rig.Controller.EditMode, Is.False);
                Assert.That(rig.Component.EditingCalls, Is.EqualTo(new[] { false }));
                Assert.That(ended, Is.EqualTo(1));
                Set(rig.Controller, "m_onEditEnd", null);
                Assert.DoesNotThrow(() => rig.Controller.RunExitEditMode(), "the end event is optional");
            });
        }

        // OnValidate 0x697BC0 only queries CanValidate (twice); it changes nothing.
        [Test]
        public void ValidatingChangesNothing()
        {
            WithController(rig =>
            {
                Assert.DoesNotThrow(() => Call(rig.Controller, "OnValidate"));
                Assert.That(Get<float>(rig.Controller, "m_scaleMin"), Is.EqualTo(0.05f));
            });
        }
    }
}
