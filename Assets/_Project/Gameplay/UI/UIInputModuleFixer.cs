using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.UI
{
    /// <summary>
    /// Asegura que el InputSystemUIInputModule tenga acciones asignadas válidas en runtime
    /// (especialmente en builds standalone donde los assets de paquete pueden quedar desvinculados),
    /// garantiza la persistencia del EventSystem entre escenas y mantiene el cursor libre y visible.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000)]
    public sealed class UIInputModuleFixer : MonoBehaviour
    {
        private static UIInputModuleFixer _instance;

        public static UIInputModuleFixer Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                var es = GetComponent<UnityEngine.EventSystems.EventSystem>();
                if (es != null)
                {
                    es.enabled = false;
                }
                Destroy(gameObject);
                return;
            }

            var activeEs = UnityEngine.EventSystems.EventSystem.current;
            if (activeEs != null && activeEs.gameObject != gameObject)
            {
                var myEs = GetComponent<UnityEngine.EventSystems.EventSystem>();
                if (myEs != null)
                {
                    myEs.enabled = false;
                }
                Destroy(gameObject);
                return;
            }

            _instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            CleanupDuplicateEventSystems();
            EnsureValidUIInput();
            EnsureCursorVisible();
        }

        private void OnEnable()
        {
            if (_instance != null && _instance != this)
            {
                var es = GetComponent<UnityEngine.EventSystems.EventSystem>();
                if (es != null)
                {
                    es.enabled = false;
                }
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;

            EnsureValidUIInput();
            EnsureCursorVisible();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CleanupDuplicateEventSystems();
        }

        public void CleanupDuplicateEventSystems()
        {
            var allSystems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

            for (int i = 0; i < allSystems.Length; i++)
            {
                var sys = allSystems[i];
                if (sys != null && sys.gameObject != gameObject)
                {
                    sys.enabled = false;
                    Destroy(sys.gameObject);
                }
            }
        }

        public void EnsureValidUIInput()
        {
            var uiModule = GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = gameObject.AddComponent<InputSystemUIInputModule>();
            }

            if (uiModule != null)
            {
                uiModule.enabled = true;

                bool needsDefaultActions = uiModule.actionsAsset == null
                    || uiModule.point == null || uiModule.point.action == null
                    || uiModule.leftClick == null || uiModule.leftClick.action == null;

                if (needsDefaultActions)
                {
                    try
                    {
                        uiModule.enabled = false;
                        uiModule.AssignDefaultActions();
                        uiModule.enabled = true;
                    }
                    catch
                    {
                        // Fallback if AssignDefaultActions fails in current context
                    }
                }
                else
                {
                    if (uiModule.point != null && uiModule.point.action != null && !uiModule.point.action.enabled)
                        uiModule.point.action.Enable();
                    if (uiModule.leftClick != null && uiModule.leftClick.action != null && !uiModule.leftClick.action.enabled)
                        uiModule.leftClick.action.Enable();
                }

                if (uiModule.actionsAsset != null)
                {
                    uiModule.actionsAsset.Enable();
                }
            }
        }

        public void EnsureCursorVisible()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void EnsureUIFocusAndInput()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            var eventSystem = UnityEngine.EventSystems.EventSystem.current;
            if (eventSystem == null)
            {
                eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
                if (eventSystem == null)
                {
                    var esGo = new GameObject("EventSystem");
                    eventSystem = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                    var inputModule = esGo.AddComponent<InputSystemUIInputModule>();
                    try { inputModule.AssignDefaultActions(); } catch { }
                    esGo.AddComponent<UIInputModuleFixer>();
                    DontDestroyOnLoad(esGo);
                }
            }

            if (eventSystem != null)
            {
                eventSystem.enabled = true;
                eventSystem.SetSelectedGameObject(null);

                var uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
                if (uiModule == null)
                {
                    uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
                }

                if (uiModule != null)
                {
                    uiModule.enabled = true;
                    bool needsDefaultActions = uiModule.actionsAsset == null
                        || uiModule.point == null || uiModule.point.action == null
                        || uiModule.leftClick == null || uiModule.leftClick.action == null;

                    if (needsDefaultActions)
                    {
                        try
                        {
                            uiModule.enabled = false;
                            uiModule.AssignDefaultActions();
                            uiModule.enabled = true;
                        }
                        catch
                        {
                            // Fallback
                        }
                    }
                    else
                    {
                        if (uiModule.point != null && uiModule.point.action != null && !uiModule.point.action.enabled)
                            uiModule.point.action.Enable();
                        if (uiModule.leftClick != null && uiModule.leftClick.action != null && !uiModule.leftClick.action.enabled)
                            uiModule.leftClick.action.Enable();
                    }

                    if (uiModule.actionsAsset != null)
                    {
                        uiModule.actionsAsset.Enable();
                    }
                }
            }
        }
    }
}
