using System;
using System.Collections.Generic;
using Hardlight;
using HardlightProject;
using NUnit.Framework;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;

namespace ProjectLucid.Tests
{
    // Restored Game.Runtime CameraInputHandler (TypeDef 0x020006a7, x86-64 0x589E20..0x58A1DF). The DataManager is a
    // real component on an owned inactive host registered into its absent row; the character constants and the
    // controls mapping are real ScriptableObjects. The handler is registered by setting the flag the real Register
    // sets, because Register would hang the handler on a live ControlMapping whose callbacks write global input state.
    public sealed class CameraInputHandlerTests
    {
        private static readonly GameInput LeftInput = GameInput.Tap;
        private static readonly GameInput RightInput = GameInput.Touch;
        private static readonly GameInput UpInput = GameInput.Back;
        private static readonly GameInput DownInput = GameInput.PointerMove;

        private static void WithData(Action<Owned, DataManager> body) => Owned.Run(owned =>
        {
            // InputHandler's field initialiser takes the InputSystem reference.
            owned.LeaseRow<InputSystem>();
            body(owned, owned.Register(owned.Host("Lucid data manager", false).AddComponent<DataManager>()));
        });

        // Character constants whose camera mapping uses four inputs that differ from the defaults.
        private static CharacterControlsMappingDefinition.CameraControlsMapping AddCharacterConstants(Owned owned, DataManager data)
        {
            var mappingDefinition = owned.Track(ScriptableObject.CreateInstance<CharacterControlsMappingDefinition>());
            var constants = owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Character>());
            constants.ControlsMappingDefinition = mappingDefinition;
            CharacterControlsMappingDefinition.CameraControlsMapping camera = mappingDefinition.CameraMapping;
            Set(camera, "m_left", LeftInput);
            Set(camera, "m_right", RightInput);
            Set(camera, "m_up", UpInput);
            Set(camera, "m_down", DownInput);
            data.GlobalConstantDefinitions[GlobalConstantType.Character] = constants;
            return camera;
        }

        private static string Held(GameInput input, float value) =>
            ButtonCallbackLog.Row("held", 0, input, value, ControlMapping.LastInputType);

        // The constructor 0x589E20 looks up the Character constants (the indexer, 0x589f46), tests the type of the
        // definition (the class check at 0x589f64..0x589f79) and, when it is the character constants, stores the
        // camera mapping of their controls definition (+0x20 then +0x38, 0x589f80..0x589f88).
        [Test]
        public void TheConstructorTakesTheCameraMappingFromTheCharacterConstants()
        {
            WithData((owned, data) =>
            {
                CharacterControlsMappingDefinition.CameraControlsMapping camera = AddCharacterConstants(owned, data);

                var handler = new CameraInputHandler();

                Assert.That(Get<object>(handler, "m_controlsMapping"), Is.SameAs(camera));
                Assert.That(Get<object>(handler, "m_dataManagerRef"), Is.Not.Null);
            });
        }

        [Test]
        public void TheConstructorKeepsNoMappingWhenTheCharacterEntryIsAnotherConstantType()
        {
            WithData((owned, data) =>
            {
                data.GlobalConstantDefinitions[GlobalConstantType.Character] =
                    owned.Track(ScriptableObject.CreateInstance<GlobalConstantDefinition_Input>());

                var handler = new CameraInputHandler();

                Assert.That(Get<object>(handler, "m_controlsMapping"), Is.Null);
            });
        }

        // The dictionary indexer throws for a missing key; there is no guard around it.
        [Test]
        public void TheConstructorFaultsWithoutTheCharacterConstants()
        {
            WithData((owned, data) =>
            {
                Assert.Throws<KeyNotFoundException>(() => new CameraInputHandler());
            });
        }

        // ApplyInput 0x58A020 reports each axis as one held button. Horizontal picks Right for a value above zero and
        // otherwise Left (seta after ucomiss against zero, 0x58a05c..0x58a066); vertical picks Up for a value above
        // zero and otherwise Down (setbe, 0x58a126..0x58a12d), so zero and NaN select Left and Down. The value is the
        // absolute axis (andps with the sign mask, 0x58a0f2) and the input type is the shared last input type
        // (static +0x88, 0x58a0fc). The horizontal button is raised before the vertical one.
        [Test]
        public void EachAxisIsReportedAsAHeldButtonOfItsDirectionWithTheAbsoluteValue()
        {
            WithData((owned, data) =>
            {
                AddCharacterConstants(owned, data);
                var handler = new CameraInputHandler();
                Set(handler, "m_registered", true);
                var log = new ButtonCallbackLog();
                log.Attach(handler);

                (Vector2 input, string[] expected)[] table =
                {
                    (new Vector2(1f, 1f), new[] { Held(RightInput, 1f), Held(UpInput, 1f) }),
                    (new Vector2(-0.5f, -2f), new[] { Held(LeftInput, 0.5f), Held(DownInput, 2f) }),
                    (new Vector2(0.25f, -4f), new[] { Held(RightInput, 0.25f), Held(DownInput, 4f) }),
                    (new Vector2(-3f, 0.75f), new[] { Held(LeftInput, 3f), Held(UpInput, 0.75f) }),
                    (Vector2.zero, new[] { Held(LeftInput, 0f), Held(DownInput, 0f) }),
                    (new Vector2(float.NaN, float.NaN), new[] { Held(LeftInput, float.NaN), Held(DownInput, float.NaN) }),
                };
                foreach ((Vector2 input, string[] expected) in table)
                {
                    log.Calls.Clear();
                    handler.ApplyInput(input);
                    Assert.That(log.Calls, Is.EqualTo(expected), "input " + input);
                }
            });
        }

        // The held callback is the inherited InputHandler one, which raises nothing while the handler is unregistered.
        [Test]
        public void NothingIsReportedWhileTheHandlerIsNotRegistered()
        {
            WithData((owned, data) =>
            {
                AddCharacterConstants(owned, data);
                var handler = new CameraInputHandler();
                var log = new ButtonCallbackLog();
                log.Attach(handler);

                handler.ApplyInput(new Vector2(1f, 1f));

                Assert.That(log.Calls, Is.Empty);
            });
        }
    }
}
