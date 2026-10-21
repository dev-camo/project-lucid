using System;
using System.Linq;
using System.Reflection;
using HardlightProject;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static ProjectLucid.Tests.WorldTriggerTestSupport;
using Object = UnityEngine.Object;

namespace ProjectLucid.Tests
{
    // The enemy ability definitions (Movement 0200053f, AirMovement 02000538, Dead 02000539, FireProjectile 0200053a,
    // GroundAttack 0200053b, GroundMovement 0200053c, GroundMovement_WithTarget 0200053d, Idle 0200053e, Taunt 02000540)
    // against the 1.10.1 x86-64 bodies at 0x54BC60..0x54C1E0. Defaults are the constants the constructors store
    // (0x54BCF0, 0x54BCD0, 0x54BD80, 0x54BF30, 0x54BFE0, 0x54C060, 0x54C0E0, 0x54C1E0); definitions are created with
    // ScriptableObject.CreateInstance and serialized fields are written through the editor JSON overwrite, as authored
    // data would be.
    // Not covered here: a started Actor creating the ability from the definition (CreateAbility needs the EnemyAbility
    // types restored with their own definitions).
    public sealed class EnemyAbilityDefinitionTests
    {
        private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const float SineTolerance = 1e-6f;

        private static void With<T>(Action<T> body) where T : ScriptableObject
        {
            var definition = ScriptableObject.CreateInstance<T>();
            try
            {
                body(definition);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static void Overwrite(Object target, string json) => EditorJsonUtility.FromJsonOverwrite(json, target);

        private static void Validate(AbilityDefinition definition) =>
            definition.GetType().GetMethod("OnValidate", Instance).Invoke(definition, null);

        // 0x54BCF0, 0x54BCD0, 0x54BF30, 0x54C060, 0x54C0E0 each store the pair (120.0f, 3.0f) at +0x48 (loaded from
        // 0x29A7820) before chaining to AbilityDefinition::.ctor; the pair is not symmetric, so a swapped order fails.
        [Test]
        public void MovementDefinitionsStartAt120DegreesTurnSpeedAnd3ForwardSpeed()
        {
            With<EnemyAbilityDefinition_AirMovement>(Check);
            With<EnemyAbilityDefinition_GroundMovement>(Check);
            With<EnemyAbilityDefinition_GroundMovement_WithTarget>(Check);
            With<EnemyAbilityDefinition_FireProjectile>(Check);

            void Check(EnemyAbilityDefinition_Movement definition)
            {
                Assert.That(definition.TurnSpeed, Is.EqualTo(120f), definition.GetType().Name);
                Assert.That(definition.ForwardSpeed, Is.EqualTo(3f), definition.GetType().Name);
                Assert.That(definition.TurnSameDirectionCurve, Is.Null, definition.GetType().Name);
                Assert.That(definition.AccelerationCurve, Is.Null, definition.GetType().Name);
            }
        }

        // 0x54BCD0 writes only the speed pair, so the height restriction starts off with zero limits.
        [Test]
        public void AirMovementStartsWithoutAHeightRestriction()
        {
            With<EnemyAbilityDefinition_AirMovement>(definition =>
            {
                Assert.That(definition.RestrictHeight, Is.False);
                Assert.That(definition.RaycastLength, Is.EqualTo(0f));
                Assert.That(definition.RaycastTarget.value, Is.EqualTo(0));
                Assert.That(definition.MinHeight, Is.EqualTo(0f));
                Assert.That(definition.MaxHeight, Is.EqualTo(0f));
                Overwrite(definition, "{\"RestrictHeight\":true,\"RaycastLength\":4.5,\"MinHeight\":1.25,\"MaxHeight\":7.5}");
                Assert.That(definition.RestrictHeight, Is.True);
                Assert.That(definition.RaycastLength, Is.EqualTo(4.5f));
                Assert.That(definition.MinHeight, Is.EqualTo(1.25f));
                Assert.That(definition.MaxHeight, Is.EqualTo(7.5f));
            });
        }

        // 0x54BD80 stores 0x41200000 (10.0f); 0x54C1E0 stores 0x40000000 (2.0f); 0x54BFE0 stores 0x3EB33333 (0.35f).
        // Each getter returns its field (0x54BD70, 0x54C1D0), shown by writing a different authored value.
        [Test]
        public void DeadTauntAndGroundAttackStartWithTheirNativeTimes()
        {
            With<EnemyAbilityDefinition_Dead>(definition =>
            {
                Assert.That(definition.ReviveTime, Is.EqualTo(10f));
                Overwrite(definition, "{\"m_reviveTime\":3.5}");
                Assert.That(definition.ReviveTime, Is.EqualTo(3.5f));
            });
            With<EnemyAbilityDefinition_Taunt>(definition =>
            {
                Assert.That(definition.TauntTime, Is.EqualTo(2f));
                Overwrite(definition, "{\"m_tauntingAnimationLength\":1.75}");
                Assert.That(definition.TauntTime, Is.EqualTo(1.75f));
            });
            With<EnemyAbilityDefinition_GroundAttack>(definition =>
            {
                Assert.That(BitConverter.SingleToInt32Bits(definition.WindUpAttackTime), Is.EqualTo(0x3EB33333));
            });
        }

        // 0x54BF30: projectile type 0xD18F7945, wind up and down 1.0f (pair at 0x29A6C30), attach points 0x12C219F9 for
        // both, range 20.0f (0x41A00000), vertical limits +60.0f and -60.0f (pair at 0x29A7830). The accuracy is not
        // written, and the three cached values are not computed by the constructor.
        [Test]
        public void FireProjectileStartsWithTheNativeConstructorValues()
        {
            With<EnemyAbilityDefinition_FireProjectile>(definition =>
            {
                Assert.That((int)definition.ProjectileType, Is.EqualTo(unchecked((int)0xD18F7945)));
                Assert.That(definition.WindUpTime, Is.EqualTo(1f));
                Assert.That(definition.WindDownTime, Is.EqualTo(1f));
                Assert.That((int)definition.ShootFromOnEnemy, Is.EqualTo(0x12C219F9));
                Assert.That((int)definition.AimForOnTarget, Is.EqualTo(0x12C219F9));
                Assert.That(definition.AccuracyMetres, Is.EqualTo(0f));
                Assert.That(definition.RangeSquared, Is.EqualTo(0f));
                Assert.That(definition.VerticalAngleMaxSine, Is.EqualTo(0f));
                Assert.That(definition.VerticalAngleMinSine, Is.EqualTo(0f));
                definition.CalculateCachedValues();
                Assert.That(definition.RangeSquared, Is.EqualTo(400f));
                Assert.That(definition.VerticalAngleMaxSine, Is.EqualTo(0.8660254f).Within(SineTolerance));
                Assert.That(definition.VerticalAngleMinSine, Is.EqualTo(-0.8660254f).Within(SineTolerance));
            });
        }

        // 0x54BEC0: range squared, then the sine of the maximum angle (lane 0, stored at +0x88) and of the minimum angle
        // (lane 1, +0x8C), each after a multiply by Deg2Rad. Unequal limits show which angle feeds which cache.
        [Test]
        public void FireProjectileCachesSquareTheRangeAndTakeTheSineOfEachVerticalLimit()
        {
            With<EnemyAbilityDefinition_FireProjectile>(definition =>
            {
                Overwrite(definition, "{\"m_range\":3,\"m_verticalAngleMaxDegrees\":30,\"m_verticalAngleMinDegrees\":-45}");
                definition.CalculateCachedValues();
                Assert.That(definition.RangeSquared, Is.EqualTo(9f));
                Assert.That(definition.VerticalAngleMaxSine, Is.EqualTo(0.5f).Within(SineTolerance));
                Assert.That(definition.VerticalAngleMinSine, Is.EqualTo(-0.70710678f).Within(SineTolerance));
            });
        }

        // 0x54BF20 and 0x54BCC0 tail-call AbilityDefinition::OnValidate, which refreshes the cached values (0x06001970).
        [Test]
        public void ValidatingAFireProjectileDefinitionRefreshesItsCaches()
        {
            With<EnemyAbilityDefinition_FireProjectile>(definition =>
            {
                Overwrite(definition, "{\"m_range\":5,\"m_verticalAngleMaxDegrees\":90,\"m_verticalAngleMinDegrees\":0}");
                Validate(definition);
                Assert.That(definition.RangeSquared, Is.EqualTo(25f));
                Assert.That(definition.VerticalAngleMaxSine, Is.EqualTo(1f).Within(SineTolerance));
                Assert.That(definition.VerticalAngleMinSine, Is.EqualTo(0f).Within(SineTolerance));
            });
            With<EnemyAbilityDefinition_AirMovement>(definition => Assert.DoesNotThrow(() => Validate(definition)));
        }

        // The getters at 0x54BE00..0x54BE50 return the authored values unchanged.
        [Test]
        public void FireProjectileGettersReturnTheAuthoredValues()
        {
            With<EnemyAbilityDefinition_FireProjectile>(definition =>
            {
                Overwrite(definition, "{\"m_projectileType\":7,\"m_windUpTime\":2.5,\"m_windDownTime\":0.5,\"m_shootFromOnEnemy\":11,\"m_aimForOnTarget\":13,\"m_accuracyMetres\":4}");
                Assert.That((int)definition.ProjectileType, Is.EqualTo(7));
                Assert.That(definition.WindUpTime, Is.EqualTo(2.5f));
                Assert.That(definition.WindDownTime, Is.EqualTo(0.5f));
                Assert.That((int)definition.ShootFromOnEnemy, Is.EqualTo(11));
                Assert.That((int)definition.AimForOnTarget, Is.EqualTo(13));
                Assert.That(definition.AccuracyMetres, Is.EqualTo(4f));
            });
        }

        // Each get_ScriptType (0x54BC60, 0x54BD10, 0x54BDA0, 0x54BF80, 0x54C000, 0x54C080, 0x54C100, 0x54C170) loads the
        // Il2CppType of the ability class of the same suffix.
        [Test]
        public void EachDefinitionNamesTheAbilityScriptOfTheSameSuffix()
        {
            With<EnemyAbilityDefinition_AirMovement>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_AirMovement")));
            With<EnemyAbilityDefinition_Dead>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_Dead")));
            With<EnemyAbilityDefinition_FireProjectile>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_FireProjectile")));
            With<EnemyAbilityDefinition_GroundAttack>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_GroundAttack")));
            With<EnemyAbilityDefinition_GroundMovement>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_GroundMovement")));
            With<EnemyAbilityDefinition_GroundMovement_WithTarget>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_GroundMovement_WithTarget")));
            With<EnemyAbilityDefinition_Idle>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_Idle")));
            With<EnemyAbilityDefinition_Taunt>(definition => Assert.That(definition.ScriptType.Name, Is.EqualTo("EnemyAbility_Taunt")));
        }

        // The base types come from the dump; the movement base is abstract and the with-target definition extends the
        // plain ground movement one.
        [Test]
        public void DefinitionsKeepTheirBaseTypes()
        {
            Assert.That(typeof(EnemyAbilityDefinition_Movement).BaseType, Is.EqualTo(typeof(AbilityDefinition)));
            Assert.That(typeof(EnemyAbilityDefinition_Movement).IsAbstract, Is.True);
            foreach (Type type in new[] { typeof(EnemyAbilityDefinition_AirMovement), typeof(EnemyAbilityDefinition_FireProjectile), typeof(EnemyAbilityDefinition_GroundMovement) })
                Assert.That(type.BaseType, Is.EqualTo(typeof(EnemyAbilityDefinition_Movement)), type.Name);
            Assert.That(typeof(EnemyAbilityDefinition_GroundMovement_WithTarget).BaseType, Is.EqualTo(typeof(EnemyAbilityDefinition_GroundMovement)));
            foreach (Type type in new[] { typeof(EnemyAbilityDefinition_Dead), typeof(EnemyAbilityDefinition_GroundAttack), typeof(EnemyAbilityDefinition_Idle), typeof(EnemyAbilityDefinition_Taunt) })
                Assert.That(type.BaseType, Is.EqualTo(typeof(AbilityDefinition)), type.Name);
        }

        // CreateAssetMenu rows from the DummyDll metadata; the taunt file name keeps its original "Definition" spelling.
        [Test]
        public void DefinitionsKeepTheirAssetMenuEntries()
        {
            foreach ((Type type, string file, string menu) in new[]
            {
                (typeof(EnemyAbilityDefinition_AirMovement), "EnemyAbility_AirMovement", "EnemyAbility_AirMovement"),
                (typeof(EnemyAbilityDefinition_Dead), "EnemyAbility_Dead", "EnemyAbility_Dead"),
                (typeof(EnemyAbilityDefinition_FireProjectile), "EnemyAbility_FireProjectile", "EnemyAbility_FireProjectile"),
                (typeof(EnemyAbilityDefinition_GroundAttack), "EnemyAbility_GroundAttack", "EnemyAbility_GroundAttack"),
                (typeof(EnemyAbilityDefinition_GroundMovement), "EnemyAbility_GroundMovement", "EnemyAbility_GroundMovement"),
                (typeof(EnemyAbilityDefinition_GroundMovement_WithTarget), "EnemyAbility_GroundMovement_WithTarget", "EnemyAbility_GroundMovement_WithTarget"),
                (typeof(EnemyAbilityDefinition_Idle), "EnemyAbility_Idle", "EnemyAbility_Idle"),
                (typeof(EnemyAbilityDefinition_Taunt), "EnemyAbilityDefinition_Taunt", "EnemyAbility_Taunt"),
            })
            {
                CreateAssetMenuAttribute attribute = type.GetCustomAttributes(typeof(CreateAssetMenuAttribute), false).Cast<CreateAssetMenuAttribute>().Single();
                Assert.That(attribute.fileName, Is.EqualTo(file), type.Name);
                Assert.That(attribute.menuName, Is.EqualTo("HardlightProject/DefinitionData/AbilityDef/" + menu), type.Name);
                Assert.That(attribute.order, Is.EqualTo(1), type.Name);
            }
            Assert.That(typeof(EnemyAbilityDefinition_Movement).GetCustomAttributes(typeof(CreateAssetMenuAttribute), false), Is.Empty);
        }

        // Flags, field order and accessor flags from the DummyDll tables (Il2CppSetOption values in source order:
        // 1 = NullChecks, 2 = ArrayBoundsChecks).
        [Test]
        public void DefinitionsKeepTheirFlagsFieldsAndMethods()
        {
            AssertMembers(typeof(EnemyAbilityDefinition_Movement), 0x00100081,
                new[] { ("TurnSpeed", 0x0006), ("ForwardSpeed", 0x0006), ("TurnSameDirectionCurve", 0x0006), ("AccelerationCurve", 0x0006) },
                new[] { (".ctor", 0x1884) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_Movement), 2, 1);

            AssertMembers(typeof(EnemyAbilityDefinition_AirMovement), 0x00100001,
                new[] { ("RestrictHeight", 0x0006), ("RaycastLength", 0x0006), ("RaycastTarget", 0x0006), ("MinHeight", 0x0006), ("MaxHeight", 0x0006) },
                new[] { ("get_ScriptType", 0x08c6), ("OnValidate", 0x00c4), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_AirMovement), 1, 2);

            AssertMembers(typeof(EnemyAbilityDefinition_Dead), 0x00100001,
                new[] { ("m_reviveTime", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_ReviveTime", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_Dead), 1, 2);

            AssertMembers(typeof(EnemyAbilityDefinition_FireProjectile), 0x00100001,
                new[]
                {
                    ("m_projectileType", 0x0001), ("m_windUpTime", 0x0001), ("m_windDownTime", 0x0001), ("m_shootFromOnEnemy", 0x0001),
                    ("m_aimForOnTarget", 0x0001), ("m_range", 0x0001), ("m_accuracyMetres", 0x0001), ("m_verticalAngleMaxDegrees", 0x0001),
                    ("m_verticalAngleMinDegrees", 0x0001), ("<RangeSquared>k__BackingField", 0x0001), ("<VerticalAngleMaxSine>k__BackingField", 0x0001),
                    ("<VerticalAngleMinSine>k__BackingField", 0x0001),
                },
                new[]
                {
                    ("get_ScriptType", 0x08c6), ("get_ProjectileType", 0x0886), ("get_WindUpTime", 0x0886), ("get_WindDownTime", 0x0886),
                    ("get_ShootFromOnEnemy", 0x0886), ("get_AimForOnTarget", 0x0886), ("get_AccuracyMetres", 0x0886), ("get_RangeSquared", 0x0886),
                    ("set_RangeSquared", 0x0881), ("get_VerticalAngleMaxSine", 0x0886), ("set_VerticalAngleMaxSine", 0x0881),
                    ("get_VerticalAngleMinSine", 0x0886), ("set_VerticalAngleMinSine", 0x0881), ("CalculateCachedValues", 0x00c6),
                    ("OnValidate", 0x00c4), (".ctor", 0x1886),
                });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_FireProjectile), 1, 2);

            AssertMembers(typeof(EnemyAbilityDefinition_GroundAttack), 0x00100001,
                new[] { ("WindUpAttackTime", 0x0006) }, new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_GroundAttack), 1, 2);

            foreach (Type type in new[] { typeof(EnemyAbilityDefinition_GroundMovement), typeof(EnemyAbilityDefinition_GroundMovement_WithTarget) })
            {
                AssertMembers(type, 0x00100001, new (string, int)[0], new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
                AssertIl2CppOptions(type, 1, 2);
            }

            AssertMembers(typeof(EnemyAbilityDefinition_Idle), 0x00100001, new (string, int)[0], new[] { ("get_ScriptType", 0x08c6), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_Idle), 2, 1);

            AssertMembers(typeof(EnemyAbilityDefinition_Taunt), 0x00100001,
                new[] { ("m_tauntingAnimationLength", 0x0001) },
                new[] { ("get_ScriptType", 0x08c6), ("get_TauntTime", 0x0886), (".ctor", 0x1886) });
            AssertIl2CppOptions(typeof(EnemyAbilityDefinition_Taunt), 2, 1);
        }

        // Field attributes from the metadata (the tooltips, the Min / Range limits and the single Header).
        [Test]
        public void SerializedFieldsKeepTheirAuthoringAttributes()
        {
            FieldInfo turnSpeed = typeof(EnemyAbilityDefinition_Movement).GetField("TurnSpeed");
            Assert.That(Tooltip(turnSpeed), Is.EqualTo("Turning speed, degrees per second."));
            Assert.That(Tooltip(typeof(EnemyAbilityDefinition_Movement).GetField("ForwardSpeed")), Is.EqualTo("Forward speed when moving, units per second."));

            FieldInfo restrict = typeof(EnemyAbilityDefinition_AirMovement).GetField("RestrictHeight");
            Assert.That(restrict.GetCustomAttributes(typeof(HeaderAttribute), false).Cast<HeaderAttribute>().Single().header, Is.EqualTo("Height restrictions"));
            foreach (string name in new[] { "RaycastLength", "MinHeight", "MaxHeight" })
            {
                MinAttribute min = typeof(EnemyAbilityDefinition_AirMovement).GetField(name).GetCustomAttributes(typeof(MinAttribute), false).Cast<MinAttribute>().Single();
                Assert.That(min.min, Is.EqualTo(0f), name);
            }

            foreach (string name in new[] { "m_verticalAngleMaxDegrees", "m_verticalAngleMinDegrees" })
            {
                FieldInfo field = typeof(EnemyAbilityDefinition_FireProjectile).GetField(name, Instance);
                UnityEngine.RangeAttribute range = field.GetCustomAttributes(typeof(UnityEngine.RangeAttribute), false).Cast<UnityEngine.RangeAttribute>().Single();
                Assert.That((range.min, range.max), Is.EqualTo((-90f, 90f)), name);
                Assert.That(field.GetCustomAttributes(typeof(SerializeField), false), Has.Length.EqualTo(1), name);
            }
            Assert.That(typeof(EnemyAbilityDefinition_Dead).GetField("m_reviveTime", Instance).GetCustomAttributes(typeof(MinAttribute), false), Has.Length.EqualTo(1));
            Assert.That(typeof(EnemyAbilityDefinition_Taunt).GetField("m_tauntingAnimationLength", Instance).GetCustomAttributes(typeof(TooltipAttribute), false), Is.Empty);
        }
    }
}
