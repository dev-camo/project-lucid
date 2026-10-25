using Hardlight;
using HardlightProject;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ProjectLucid.Offline
{
    // D14: the release's level-results menu can reactivate its separate More Games
    // button through both a value binder and a visibility group. Keep that authored
    // owner hidden synchronously on re-enable, without changing either original binder.
    public sealed class OfflineCrossPromoEntryPoint : MonoBehaviour
    {
        private void OnEnable()
        {
            // Offline hook H-079: a previously hidden button can be reactivated by the menu.
            if (OfflineAdapters.Active)
                gameObject.SetActive(false);
        }

        internal static void HideLauncher(CrossPromoLaunchButtonSDT launcher)
        {
            UIContainer container = launcher.GetComponentInParent<UIContainer>();
            if (container != null)
            {
                foreach (Button button in container.GetComponentsInChildren<Button>(true))
                {
                    // Nested containers own their own controls; callback identity also excludes unrelated buttons.
                    if (button.GetComponentInParent<UIContainer>(true) != container || !TargetsLauncher(button.onClick, launcher))
                        continue;
                    if (button.gameObject != launcher.gameObject && button.GetComponent<OfflineCrossPromoEntryPoint>() == null)
                        button.gameObject.AddComponent<OfflineCrossPromoEntryPoint>();
                    button.gameObject.SetActive(false);
                }
            }
            // Base OnEnable has already acquired its references. This runs the original
            // OnDisable, including removal of a pending startup or Initialised callback.
            launcher.gameObject.SetActive(false);
        }

        internal static bool HideInputEntryPoint(GameObject owner, UnityEvent action)
        {
            if (action == null) return false;
            for (int i = 0; i < action.GetPersistentEventCount(); ++i)
            {
                if (action.GetPersistentTarget(i) is CrossPromoLaunchButton &&
                    action.GetPersistentMethodName(i) == nameof(CrossPromoLaunchButton.Action_LaunchCrossPromo))
                {
                    owner.SetActive(false);
                    return true;
                }
            }
            return false;
        }

        private static bool TargetsLauncher(UnityEvent action, CrossPromoLaunchButtonSDT launcher)
        {
            for (int i = 0; i < action.GetPersistentEventCount(); ++i)
                if (action.GetPersistentTarget(i) == launcher &&
                    action.GetPersistentMethodName(i) == nameof(CrossPromoLaunchButtonSDT.Action_LaunchCrossPromo))
                    return true;
            return false;
        }
    }
}
