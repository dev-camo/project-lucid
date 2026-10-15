using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ProjectLucid.Editor
{
    // Behaviour of the six original HardlightProject types shipped in Assembly-CSharp (1.10.1),
    // each grounded in its x86-64 body. Types are reached by name because test assemblies cannot
    // reference the predefined Assembly-CSharp. Every object is owned, inactive unless a case needs
    // it active, and destroyed afterwards; shader globals are restored; Camera.main is only read.
    public sealed class OriginalAssemblyCSharpStudioBehaviourTests
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            for (int i = owned.Count - 1; i >= 0; i--)
                if (owned[i] != null) Object.DestroyImmediate(owned[i]);
            owned.Clear();
        }

        private GameObject Owned(string name)
        {
            var gameObject = new GameObject("Lucid " + name);
            gameObject.SetActive(false);
            owned.Add(gameObject);
            return gameObject;
        }

        private Component Studio(string type) =>
            Owned(type).AddComponent(Type.GetType("HardlightProject." + type + ", Assembly-CSharp", true));

        private static void Set(Component component, string field, object value) => component.GetType().GetField(field, Members).SetValue(component, value);
        private static object Get(Component component, string field) => component.GetType().GetField(field, Members).GetValue(component);
        private static void Call(Component component, string method, params object[] arguments)
        {
            try { component.GetType().GetMethod(method, Members).Invoke(component, arguments); }
            catch (TargetInvocationException failure) when (failure.InnerException != null) { throw failure.InnerException; }
        }

        private static void Near(Vector3 expected, Vector3 actual) => Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), expected + " vs " + actual);

        // prototype_timer.Update (x86-64 0x42e690): 0x42e6a1 hides when 0 >= spawnedTime, so exactly
        // zero hides and NaN does not; 0x42e6a6 keeps counting down only while spawnedTime >= -0.5.
        // Catches a strict `< 0` hide test and a missing or moved -0.5 bound.
        [Test]
        public void TimerHidesAtExactlyZeroAndStopsCountingBelowMinusHalf()
        {
            Component timer = Studio("prototype_timer");
            GameObject spawned = Owned("timer spawned objects");
            Set(timer, "spawnedObjects", spawned);
            foreach (float start in new[] { 0.001f, 0f, -0.5f, -0.6f })
            {
                spawned.SetActive(true);
                Set(timer, "spawnedTime", start);
                float delta = Time.deltaTime;
                Call(timer, "Update");
                Assert.That(spawned.activeSelf, Is.EqualTo(start > 0f), "visible after Update from " + start);
                Assert.That((float)Get(timer, "spawnedTime"), Is.EqualTo(start >= -0.5f ? start - delta : start).Within(1e-6f), "countdown from " + start);
            }
        }

        // laserTest.UpdateLaser (0x42e7c0) and Start (0x42e750, a separate identical body): the line
        // end takes Transform.get_localPosition (0x42e7de), the ball Transform.get_position (0x42e810).
        // Catches the line using the world position or the ball using the local one.
        [Test]
        public void LaserLineUsesTheLocalEndPointAndTheBallTheWorldEndPoint()
        {
            foreach (string method in new[] { "Start", "UpdateLaser" })
            {
                Component laser = Studio("laserTest");
                GameObject parent = Owned("laser parent");
                parent.transform.position = new Vector3(10f, 20f, 30f);
                GameObject endPoint = Owned("laser end point");
                endPoint.transform.SetParent(parent.transform, false);
                endPoint.transform.localPosition = new Vector3(2f, 3f, 4f);
                GameObject ball = Owned("laser ball");
                LineRenderer line = Owned("laser line").AddComponent<LineRenderer>();
                line.positionCount = 2;
                Set(laser, "laserEndPoint", endPoint);
                Set(laser, "endball", ball);
                Set(laser, "lineRender", line);
                Call(laser, method);
                Near(new Vector3(2f, 3f, 4f), line.GetPosition(1));
                Near(new Vector3(12f, 23f, 34f), ball.transform.position);
            }
        }

        // prototype_destructable_wall.CheckBallDistance (0x42e840): 0x42e91b loads 9.0 and
        // 0x42e923/0x42e927 invoke wallhit only when 9 > distance. Catches `<= 9` and a moved threshold.
        [Test]
        public void WallInvokesItsEventOnlyWhenTheBallIsStrictlyCloserThanNineUnits()
        {
            foreach (KeyValuePair<float, int> distance in new Dictionary<float, int> { { 8.999f, 1 }, { 9f, 0 }, { 9.001f, 0 } })
            {
                Component wall = Studio("prototype_destructable_wall");
                GameObject ball = Owned("wall ball");
                ball.transform.position = new Vector3(distance.Key, 0f, 0f);
                int hits = 0;
                var wallhit = new UnityEvent();
                wallhit.AddListener(() => hits++);
                Set(wall, "ball", ball);
                Set(wall, "wallhit", wallhit);
                Call(wall, "CheckBallDistance");
                Assert.That(hits, Is.EqualTo(distance.Value), "hits at distance " + distance.Key);
            }
        }

        // prototype_power_line.OnTriggerEnter (0x42eac0): only a "Player" tag is handled; a power source
        // then stores the player, switches the line to world space, sets point 0 to 1.5 above itself and
        // stores poweredTime 8.0 (0x42ebce, 0x41000000). Catches another duration or a skipped tag check.
        [Test]
        public void PowerSourceStartsAnEightSecondWorldSpaceLineOnlyForThePlayer()
        {
            Component power = Studio("prototype_power_line");
            power.transform.position = new Vector3(3f, 4f, 5f);
            LineRenderer line = Owned("power line").AddComponent<LineRenderer>();
            line.positionCount = 2;
            line.useWorldSpace = false;
            GameObject player = Owned("power player");
            Collider collider = player.AddComponent<BoxCollider>();
            Set(power, "powerSource", true);
            Set(power, "lineR", line);
            Call(power, "OnTriggerEnter", collider);
            Assert.That((bool)Get(power, "powerOn"), Is.False, "an untagged collider is ignored");
            Assert.That((float)Get(power, "poweredTime"), Is.EqualTo(0f));
            player.tag = "Player";
            Call(power, "OnTriggerEnter", collider);
            Assert.That(Get(power, "target"), Is.SameAs(player));
            Assert.That((bool)Get(power, "powerOn"), Is.True);
            Assert.That((float)Get(power, "poweredTime"), Is.EqualTo(8f));
            Assert.That(line.useWorldSpace, Is.True);
            Near(new Vector3(3f, 5.5f, 5f), line.GetPosition(0));
        }

        // DreamGetPlayerPositionDebug.Update (0x42e610) publishes Transform.get_position (0x42e64e),
        // the world position, as _playerPosition. Catches publishing the local position.
        [Test]
        public void PlayerPositionShaderGlobalIsTheWorldPosition()
        {
            Vector4 prior = Shader.GetGlobalVector("_playerPosition");
            try
            {
                Component probe = Studio("DreamGetPlayerPositionDebug");
                GameObject parent = Owned("player position parent");
                parent.transform.position = new Vector3(10f, 20f, 30f);
                probe.transform.SetParent(parent.transform, false);
                probe.transform.localPosition = new Vector3(2f, 3f, 4f);
                Call(probe, "Update");
                Assert.That(Shader.GetGlobalVector("_playerPosition"), Is.EqualTo(new Vector4(12f, 23f, 34f, 0f)));
            }
            finally { Shader.SetGlobalVector("_playerPosition", prior); }
        }

        // DreamDissolveManagerDebug.Update (0x42e480): Object.op_Equality(m_mainCamera, null) at 0x42e4f4
        // re-fetches Camera.main (0x42e503) for a destroyed camera, then publishes its cameraToWorldMatrix
        // and the mask and opacity floats. Catches a missing re-fetch or a reference-only null check.
        [Test]
        public void DissolveRefetchesTheMainCameraAfterItsCameraWasDestroyed()
        {
            Matrix4x4 priorMatrix = Shader.GetGlobalMatrix("_CamToWorld");
            float priorMask = Shader.GetGlobalFloat("_Mask_Position");
            float priorOpacity = Shader.GetGlobalFloat("_Overall_Effect_Opacity");
            try
            {
                // Borrow an existing main camera read-only; otherwise own one.
                Camera main = Camera.main;
                if (main == null)
                {
                    GameObject cameraObject = Owned("dissolve main camera");
                    cameraObject.tag = "MainCamera";
                    main = cameraObject.AddComponent<Camera>();
                    cameraObject.transform.SetPositionAndRotation(new Vector3(7f, 11f, 13f), Quaternion.Euler(10f, 20f, 30f));
                    cameraObject.SetActive(true);
                }
                Assert.That(Camera.main, Is.SameAs(main));
                Component dissolve = Studio("DreamDissolveManagerDebug");
                GameObject staleObject = Owned("dissolve stale camera");
                Set(dissolve, "m_mainCamera", staleObject.AddComponent<Camera>());
                Object.DestroyImmediate(staleObject);
                Set(dissolve, "m_maskPosition", 2.75f);
                Set(dissolve, "m_opacity", 0.375f);
                Call(dissolve, "Update");
                Assert.That(Get(dissolve, "m_mainCamera"), Is.SameAs(main));
                Assert.That(Shader.GetGlobalMatrix("_CamToWorld"), Is.EqualTo(main.cameraToWorldMatrix));
                Assert.That(Shader.GetGlobalFloat("_Mask_Position"), Is.EqualTo(2.75f));
                Assert.That(Shader.GetGlobalFloat("_Overall_Effect_Opacity"), Is.EqualTo(0.375f));
            }
            finally
            {
                Shader.SetGlobalMatrix("_CamToWorld", priorMatrix);
                Shader.SetGlobalFloat("_Mask_Position", priorMask);
                Shader.SetGlobalFloat("_Overall_Effect_Opacity", priorOpacity);
            }
        }
    }
}
