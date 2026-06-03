using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace AccessTheObelisk
{
    internal static class TextInputFocusHelper
    {
        internal static bool IsTextInputFocused()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null)
            {
                return false;
            }

            GameObject selected = EventSystem.current.currentSelectedGameObject;
            TMP_InputField tmpInput = selected.GetComponent<TMP_InputField>() ?? selected.GetComponentInParent<TMP_InputField>();
            if (tmpInput != null && tmpInput.isFocused)
            {
                return true;
            }

            UnityEngine.UI.InputField unityInput = selected.GetComponent<UnityEngine.UI.InputField>() ?? selected.GetComponentInParent<UnityEngine.UI.InputField>();
            return unityInput != null && unityInput.isFocused;
        }
    }
}
