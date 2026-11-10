using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Localization
{
    public enum AppLanguage
    {
        Spanish = 0,
        English = 1
    }

    public static class LocalizationService
    {
        private const string PrefKey = "TPOB_Language";

        public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Spanish;
        public static event Action OnLanguageChanged;

        static LocalizationService()
        {
            int saved = PlayerPrefs.GetInt(PrefKey, 0);
            CurrentLanguage = saved == 1 ? AppLanguage.English : AppLanguage.Spanish;
        }

        public static void SetLanguage(AppLanguage language)
        {
            if (CurrentLanguage == language)
            {
                return;
            }

            CurrentLanguage = language;
            PlayerPrefs.SetInt(PrefKey, (int)CurrentLanguage);
            PlayerPrefs.Save();
            OnLanguageChanged?.Invoke();
        }

        public static void ToggleLanguage()
        {
            SetLanguage(CurrentLanguage == AppLanguage.Spanish ? AppLanguage.English : AppLanguage.Spanish);
        }

        public static string Get(string key)
        {
            if (Translations.TryGetValue(key, out var dict))
            {
                if (dict.TryGetValue(CurrentLanguage, out var val))
                {
                    return val;
                }
            }
            return key;
        }

        private static readonly Dictionary<string, Dictionary<AppLanguage, string>> Translations = new()
        {
            ["LocalPlay"] = new()
            {
                [AppLanguage.Spanish] = "MODO LOCAL",
                [AppLanguage.English] = "LOCAL PLAY"
            },
            ["HostGame"] = new()
            {
                [AppLanguage.Spanish] = "CREAR PARTIDA (HOST)",
                [AppLanguage.English] = "HOST GAME"
            },
            ["JoinGame"] = new()
            {
                [AppLanguage.Spanish] = "UNIRSE A PARTIDA",
                [AppLanguage.English] = "JOIN GAME"
            },
            ["Options"] = new()
            {
                [AppLanguage.Spanish] = "OPCIONES",
                [AppLanguage.English] = "OPTIONS"
            },
            ["ExitGame"] = new()
            {
                [AppLanguage.Spanish] = "SALIR DEL JUEGO",
                [AppLanguage.English] = "EXIT GAME"
            },
            ["CopyCode"] = new()
            {
                [AppLanguage.Spanish] = "COPIAR",
                [AppLanguage.English] = "COPY"
            },
            ["RoomCode"] = new()
            {
                [AppLanguage.Spanish] = "CÓDIGO DE SALA",
                [AppLanguage.English] = "ROOM CODE"
            },
            ["Connect"] = new()
            {
                [AppLanguage.Spanish] = "CONECTAR",
                [AppLanguage.English] = "CONNECT"
            },
            ["Cancel"] = new()
            {
                [AppLanguage.Spanish] = "CANCELAR",
                [AppLanguage.English] = "CANCEL"
            },
            ["ChooseLegs"] = new()
            {
                [AppLanguage.Spanish] = "ELEGIR PIERNAS",
                [AppLanguage.English] = "CHOOSE LEGS"
            },
            ["ChooseTorso"] = new()
            {
                [AppLanguage.Spanish] = "ELEGIR TORSO",
                [AppLanguage.English] = "CHOOSE TORSO"
            },
            ["Ready"] = new()
            {
                [AppLanguage.Spanish] = "CONFIRMAR (LISTO)",
                [AppLanguage.English] = "READY"
            },
            ["CancelReady"] = new()
            {
                [AppLanguage.Spanish] = "CANCELAR LISTO",
                [AppLanguage.English] = "CANCEL READY"
            },
            ["StartGame"] = new()
            {
                [AppLanguage.Spanish] = "INICIAR PARTIDA",
                [AppLanguage.English] = "START GAME"
            },
            ["LeaveLobby"] = new()
            {
                [AppLanguage.Spanish] = "VOLVER AL MENÚ",
                [AppLanguage.English] = "LEAVE LOBBY"
            },
            ["Resume"] = new()
            {
                [AppLanguage.Spanish] = "REANUDAR",
                [AppLanguage.English] = "RESUME"
            },
            ["MainMenu"] = new()
            {
                [AppLanguage.Spanish] = "MENÚ PRINCIPAL",
                [AppLanguage.English] = "MAIN MENU"
            },
            ["ExitDesktop"] = new()
            {
                [AppLanguage.Spanish] = "SALIR AL ESCRITORIO",
                [AppLanguage.English] = "EXIT TO DESKTOP"
            },
            ["TabGraphics"] = new()
            {
                [AppLanguage.Spanish] = "GRÁFICOS",
                [AppLanguage.English] = "GRAPHICS"
            },
            ["TabAudio"] = new()
            {
                [AppLanguage.Spanish] = "SONIDO",
                [AppLanguage.English] = "AUDIO"
            },
            ["TabControls"] = new()
            {
                [AppLanguage.Spanish] = "CONTROLES",
                [AppLanguage.English] = "CONTROLS"
            },
            ["ResetDefaults"] = new()
            {
                [AppLanguage.Spanish] = "RESTAURAR POR DEFECTO",
                [AppLanguage.English] = "RESTORE DEFAULTS"
            },
            ["SaveClose"] = new()
            {
                [AppLanguage.Spanish] = "GUARDAR Y CERRAR",
                [AppLanguage.English] = "SAVE & CLOSE"
            },
            ["HostLan"] = new()
            {
                [AppLanguage.Spanish] = "CREAR SERVIDOR LOCAL (LAN)",
                [AppLanguage.English] = "HOST LAN (WIFI)"
            },
            ["mainmenu_connect_help"] = new()
            {
                [AppLanguage.Spanish] = "Ingresa el código de sala online (ej. TPOB-1234) o la IP de tu amigo en red local para unirte.",
                [AppLanguage.English] = "Enter online room code (e.g. TPOB-1234) or local friend's IP to connect."
            },
            ["LanGameFound"] = new()
            {
                [AppLanguage.Spanish] = "🟢 PARTIDA DETECTADA EN RED LOCAL (CLIC PARA ENTRAR)",
                [AppLanguage.English] = "🟢 LOCAL LAN GAME DETECTED (CLICK TO JOIN)"
            },
            ["Player1Controls"] = new()
            {
                [AppLanguage.Spanish] = "JUGADOR 1 (PIERNAS / ONLINE)",
                [AppLanguage.English] = "PLAYER 1 (LEGS / ONLINE)"
            },
            ["Player2Controls"] = new()
            {
                [AppLanguage.Spanish] = "JUGADOR 2 (TORSO - MODO LOCAL)",
                [AppLanguage.English] = "PLAYER 2 (TORSO - LOCAL CO-OP)"
            },
            ["P2PDirect"] = new()
            {
                [AppLanguage.Spanish] = "P2P Directo",
                [AppLanguage.English] = "Direct P2P"
            },
            ["LocalLan"] = new()
            {
                [AppLanguage.Spanish] = "Red Local (LAN)",
                [AppLanguage.English] = "Local Network (LAN)"
            },
            ["RelaySession"] = new()
            {
                [AppLanguage.Spanish] = "Relay",
                [AppLanguage.English] = "Relay"
            },
            ["LangToggle"] = new()
            {
                [AppLanguage.Spanish] = "IDIOMA: ESPAÑOL",
                [AppLanguage.English] = "LANGUAGE: ENGLISH"
            }
        };
    }
}
