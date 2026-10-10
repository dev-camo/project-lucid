// Complete original Actor form-handler source candidate. Private and unaccepted.
// The real Actor/character dependency graph is required; no substitutes are supplied.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ActorFormHandler : MonoBehaviour
    {
        [SerializeField] private List<FormModifier> m_formModifiers = new List<FormModifier>();
        public ActorFormType CurrentFormType { get; private set; } = ActorFormType.None;
        private const ActorFormType DefaultForm = ActorFormType.Default;
        private Actor m_actor;
        private readonly Dictionary<ActorFormType, FormTraits> m_formTraitsLookup =
            new Dictionary<ActorFormType, FormTraits>(HardlightEnumComparers.ActorFormTypeComparer);
        private readonly Dictionary<ActorFormType, FormModifier> m_formModifierLookup =
            new Dictionary<ActorFormType, FormModifier>(HardlightEnumComparers.ActorFormTypeComparer);

        // Original060003de: add without clearing, so repeated/duplicate forms fail.
        public void Initialise(Actor actor)
        {
            m_actor = actor;
            foreach (FormTraits formTraits in m_actor.Forms)
                m_formTraitsLookup.Add(formTraits.FormType, formTraits);
            foreach (FormModifier formModifier in m_formModifiers)
                m_formModifierLookup.Add(formModifier.FormType, formModifier);
            SwitchToForm(DefaultForm);
        }

        public void Close() // original060003df, retain current form and authored modifier list.
        {
            m_actor = null;
            m_formTraitsLookup.Clear();
            m_formModifierLookup.Clear();
        }

        // Original060003e0: callbacks observe the old CurrentFormType. It changes
        // only after exit, collider resizing and enter callbacks return normally.
        public void SwitchToForm(ActorFormType formType)
        {
            if (CurrentFormType == formType || m_actor.IsFormLocked())
                return;
            if (m_formTraitsLookup.TryGetValue(formType, out FormTraits formTraits))
            {
                ExitForm(CurrentFormType);
                EnterForm(formType, formTraits);
                CurrentFormType = formType;
            }
        }

        public void ResetToDefault() { SwitchToForm(DefaultForm); } // original060003e1

        public void ToggleFormRenderers(bool on) // original060003e2: all registered forms.
        {
            foreach (KeyValuePair<ActorFormType, FormModifier> pair in m_formModifierLookup)
                pair.Value.ToggleRenderers(on);
        }

        private void EnterForm(ActorFormType formType, FormTraits formTraits) // original060003e3
        {
            m_actor.SetColliderCollisionSize(formTraits.ColliderRadius, formTraits.ColliderHeight);
            if (m_formModifierLookup.TryGetValue(formType, out FormModifier formModifier))
                formModifier.OnEnterForm.Invoke();
        }

        private void ExitForm(ActorFormType formType) // original060003e4
        {
            if (m_formModifierLookup.TryGetValue(formType, out FormModifier formModifier))
                formModifier.OnExitForm.Invoke();
        }

        public ActorFormHandler() { } // original060003e5: genuine field initializers precede base.

        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class FormModifier : ISerializationCallbackReceiver
        {
            [HideInInspector] public string Name;
            public ActorFormType FormType = DefaultForm;
            public UnityEvent OnEnterForm;
            public UnityEvent OnExitForm;
            public Renderer[] Renderers;

            public void ToggleRenderers(bool on) // original060003e6
            {
                foreach (Renderer renderer in Renderers)
                    renderer.gameObject.SetActive(on);
            }

            public void OnBeforeSerialize() { Name = FormType.GetString(); } // original060003e7
            public void OnAfterDeserialize() { Name = FormType.GetString(); } // original060003e8
            public FormModifier() { } // original060003e9: only FormType is initialized.
        }
    }
}
