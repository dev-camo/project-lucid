using System.Collections.Generic;
using Dreamteck.Splines;
using NUnit.Framework;
using UnityEngine;

namespace ProjectLucid.Tests
{
    // The shipped player branches of Dreamteck.Splines.SplinePositioner, run in play mode where Application.isPlaying is true:
    // SplineUser caches it in Awake, so a setter only queues the rebuild, and the tracer's trigger and node reports run (the edit
    // mode twins in OriginalDreamteckSplinePositionerTests cover the editor branch). The computer holds the Linear polyline
    // (0,0,0) -> (10,0,0) -> (10,0,10): 20 units long, so percent p is (20p,0,0) up to 0.5 and (10,0,20p-10) after. The positioners
    // are plain components on owned game objects (Transform physics mode unless a test says otherwise). The tests are synchronous,
    // so no engine Update runs between the calls.
    public sealed class OriginalDreamteckSplinePositionerPlayModeTests
    {
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _owned = new List<GameObject>();

        [TearDown]
        public void DestroyOwnedObjects()
        {
            foreach (GameObject owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        private static void Near(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(Tolerance), message + " (actual " + actual.ToString("F5") + ")");
        }

        private static bool IsMissing(object value)
        {
            return value == null || (value is Object unityObject && unityObject == null);
        }

        private GameObject NewObject(string name)
        {
            GameObject owner = new GameObject(name);
            _owned.Add(owner);
            return owner;
        }

        private SplineComputer NewLine()
        {
            SplineComputer computer = NewObject("line").AddComponent<SplineComputer>();
            computer.type = Spline.Type.Linear;
            computer.SetPoints(new[]
            {
                new SplinePoint(new Vector3(0f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 0f)),
                new SplinePoint(new Vector3(10f, 0f, 10f))
            });
            computer.RebuildImmediate();
            return computer;
        }

        private SplinePositioner NewPositioner(SplineComputer computer = null)
        {
            SplinePositioner positioner = NewObject("positioner").AddComponent<SplinePositioner>();
            if (computer != null)
            {
                positioner.spline = computer;
                positioner.RebuildImmediate();
            }

            return positioner;
        }

        private static T Read<T>(object target, string name)
        {
            return DreamteckGeneratorFixture.Field<T>(target, name);
        }

        private static void Write(object target, string name, object value)
        {
            DreamteckGeneratorFixture.SetField(target, name, value);
        }

        private static object Invoke(object target, string name, params object[] arguments)
        {
            return DreamteckGeneratorFixture.Invoke(target, name, arguments);
        }

        // get_targetObject 0x4c8f80 and set_targetObject 0x4c8ff0: the owner is the default target; a new target gets the
        // Transform (or Rigidbody / Rigidbody2D, by the physics mode) of the target object. GetTransform / GetRigidbody /
        // GetRigidbody2D 0x4c9f50/0x4c9fd0/0x4ca080 read the target object too.
        [Test]
        public void TargetObjectReplacesTheOwnerAsTheMovedObject()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            positioner.transform.position = new Vector3(7f, 7f, 7f);
            GameObject other = NewObject("other");
            other.transform.position = new Vector3(9f, 9f, 9f);
            positioner.targetObject = other;
            Assert.That(positioner.targetObject, Is.SameAs(other));
            Assert.That(Invoke(positioner, "GetTransform"), Is.SameAs(other.transform));
            positioner.SetPercent(0.25);
            Near(other.transform.position, new Vector3(5f, 0f, 0f), "the target object moved");
            Near(positioner.transform.position, new Vector3(7f, 7f, 7f), "the owner stayed");

            positioner.targetObject = null;
            Assert.That(positioner.targetObject, Is.SameAs(positioner.gameObject));
            positioner.SetPercent(0.75);
            Near(positioner.transform.position, new Vector3(10f, 0f, 5f), "the owner moves again");
            Near(other.transform.position, new Vector3(5f, 0f, 0f), "the old target stayed");

            Rigidbody body = other.AddComponent<Rigidbody>();
            positioner.targetObject = other;
            positioner.physicsMode = SplineTracer.PhysicsMode.Rigidbody;
            Assert.That(Read<Rigidbody>(positioner, "targetRigidbody"), Is.SameAs(body));
            Assert.That(Invoke(positioner, "GetRigidbody"), Is.SameAs(body));
            Assert.That(IsMissing(Invoke(positioner, "GetRigidbody2D")), Is.True, "the target object has no Rigidbody2D");
            positioner.targetObject = positioner.gameObject;
            Assert.That(IsMissing(Read<Rigidbody>(positioner, "targetRigidbody")), Is.True, "the owner has no Rigidbody");
        }

        // OnDidApplyAnimationProperties 0x4c9d90: a position that differs from the last applied one moves the target (SetPercent
        // with the float widened, or SetDistance in the distance mode) with triggers but without junctions; the base then takes
        // over the clip mirrors.
        [Test]
        public void AnimatedPositionMovesTheTargetOnce()
        {
            SplinePositioner positioner = NewPositioner(NewLine());
            Write(positioner, "_position", 0.5f);
            Invoke(positioner, "OnDidApplyAnimationProperties");
            Near(positioner.transform.position, new Vector3(10f, 0f, 0f), "percent 0.5");
            Assert.That(Read<float>(positioner, "_lastPosition"), Is.EqualTo(0.5f));
            positioner.transform.position = new Vector3(1f, 1f, 1f);
            Invoke(positioner, "OnDidApplyAnimationProperties");
            Near(positioner.transform.position, new Vector3(1f, 1f, 1f), "the same position is not applied twice");

            Write(positioner, "_mode", SplinePositioner.Mode.Distance);
            Write(positioner, "_position", 5f);
            Invoke(positioner, "OnDidApplyAnimationProperties");
            Near(positioner.transform.position, new Vector3(5f, 0f, 0f), "distance 5");
            Assert.That(Read<float>(positioner, "_lastPosition"), Is.EqualTo(5f));
        }

        // SetPercent 0x4ca1a0 and SetDistance 0x4ca3c0 report the crossed triggers when asked (SetDistance from the result's own
        // percent, with its own copy of the queue loop) and the passed nodes when junctions are asked (SetDistance reports them
        // after storing the position, but still once).
        [Test]
        public void SetPercentAndSetDistanceReportTriggersAndNodes()
        {
            SplineComputer computer = NewLine();
            SplineTrigger trigger = computer.AddTrigger(0, 0.5, SplineTrigger.Type.Double);
            List<SplineUser> crossed = new List<SplineUser>();
            trigger.AddListener(delegate (SplineUser user) { crossed.Add(user); });
            GameObject nodeOwner = NewObject("node");
            nodeOwner.transform.position = new Vector3(10f, 0f, 0f);
            Node node = nodeOwner.AddComponent<Node>();
            computer.ConnectNode(node, 1);

            SplinePositioner positioner = NewPositioner(computer);
            positioner.useTriggers = true;
            List<int> points = new List<int>();
            positioner.onNode += delegate (List<SplineTracer.NodeConnection> passed)
            {
                foreach (SplineTracer.NodeConnection connection in passed)
                {
                    Assert.That(connection.node, Is.SameAs(node));
                    points.Add(connection.point);
                }
            };

            positioner.SetPercent(0.25);
            positioner.SetPercent(0.75);
            Assert.That(crossed, Is.Empty, "triggers not requested");
            Assert.That(points, Is.Empty, "junctions not requested");
            positioner.SetPercent(0.25);
            positioner.SetPercent(0.75, true, true);
            Assert.That(crossed.Count, Is.EqualTo(1));
            Assert.That(crossed[0], Is.SameAs(positioner));
            Assert.That(points, Is.EqualTo(new[] { 1 }));

            positioner.SetPercent(0.1);
            positioner.SetDistance(15f);
            Assert.That(crossed.Count, Is.EqualTo(1), "SetDistance without a request reports nothing");
            Assert.That(points.Count, Is.EqualTo(1));
            positioner.SetPercent(0.1);
            positioner.SetDistance(15f, true, true);
            Assert.That(crossed.Count, Is.EqualTo(2), "SetDistance reports the trigger");
            Assert.That(points, Is.EqualTo(new[] { 1, 1 }), "and the node");
            Assert.That(Read<float>(positioner, "_position"), Is.EqualTo(0.75f).Within(1e-4f));
        }
    }
}
