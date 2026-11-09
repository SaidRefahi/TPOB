#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Core.Lifecycle
{
    /// <summary>
    /// Implements selection persistence across domain reloads using UnityEditor.SessionState.
    /// SessionState persists in Unity's C++ editor process memory across C# AppDomain teardowns.
    /// Uses JsonUtility to serialize SelectionMemento without heap overhead or native pointer locks.
    /// Complies strictly with AGY0001, AGY0002, and AGY0003.
    /// </summary>
    public sealed class SessionStateSelectionPersistence : ISelectionStatePersistence
    {
        private const string DefaultSessionStateKey = "Game.Editor.Core.Lifecycle.SelectionMemento.State";

        private readonly string _sessionKey;

        public SessionStateSelectionPersistence(string sessionKey = null)
        {
            _sessionKey = string.IsNullOrEmpty(sessionKey) ? DefaultSessionStateKey : sessionKey;
        }

        public bool HasMemento
        {
            get
            {
                try
                {
                    string json = SessionState.GetString(_sessionKey, string.Empty);
                    return !string.IsNullOrEmpty(json);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SessionStateSelectionPersistence] Error checking SessionState: {ex.Message}");
                    return false;
                }
            }
        }

        public void SaveMemento(SelectionMemento memento)
        {
            try
            {
                if (memento == null || memento.IsEmpty)
                {
                    ClearMemento();
                    return;
                }

                string json = JsonUtility.ToJson(memento);
                SessionState.SetString(_sessionKey, json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionStateSelectionPersistence] Failed serializing SelectionMemento: {ex.Message}");
            }
        }

        public SelectionMemento LoadMemento()
        {
            try
            {
                string json = SessionState.GetString(_sessionKey, string.Empty);
                if (string.IsNullOrEmpty(json))
                {
                    return SelectionMemento.Empty;
                }

                var memento = JsonUtility.FromJson<SelectionMemento>(json);
                return memento ?? SelectionMemento.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionStateSelectionPersistence] Failed deserializing SelectionMemento: {ex.Message}");
                return SelectionMemento.Empty;
            }
        }

        public void ClearMemento()
        {
            try
            {
                SessionState.EraseString(_sessionKey);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionStateSelectionPersistence] Error clearing SessionState: {ex.Message}");
            }
        }
    }
}
#endif
