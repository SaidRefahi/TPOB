using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace Game.Gameplay.UI
{
    /// <summary>
    /// Asegura que el InputSystemUIInputModule tenga acciones asignadas válidas en runtime
    /// (especialmente en builds standalone donde los assets de paquete pueden quedar desvinculados)
    /// y garantiza que el cursor del ratón sea visible e interactuable.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class UIInputModuleFixer : MonoBehaviour
    {
        private void Awake()
        {
            EnsureValidUIInput();
            EnsureCursorVisible();
        }

        private void OnEnable()
        {
            EnsureValidUIInput();
            EnsureCursorVisible();
        }

        private void EnsureValidUIInput()
        {
            var uiModule = GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (uiModule != null)
            {
                // Si alguna de las acciones primarias está nula o no resuelta, regenerar acciones por defecto en memoria
                bool needsDefaultActions = uiModule.actionsAsset == null
                    || uiModule.point == null || uiModule.point.action == null
                    || uiModule.leftClick == null || uiModule.leftClick.action == null;

                if (needsDefaultActions)
                {
                    uiModule.enabled = false;
                    uiModule.AssignDefaultActions();
                    uiModule.enabled = true;
                }
                else
                {
                    // Garantizar que las acciones de clic y punto estén habilitadas
                    if (uiModule.point?.action != null && !uiModule.point.action.enabled)
                        uiModule.point.action.Enable();
                    if (uiModule.leftClick?.action != null && !uiModule.leftClick.action.enabled)
                        uiModule.leftClick.action.Enable();
                }
            }
        }

        private void EnsureCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
