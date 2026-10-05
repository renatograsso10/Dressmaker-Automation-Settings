using System;
using UnityEngine;

namespace DressmakerAutomation.SettingsUI
{
    public static class Bootstrap
    {
        private static AutomationPanel instance;

        public static void Initialize(int patchStatus)
        {
            if (instance != null) return;
            GameObject host = null;
            try
            {
                host = new GameObject("Dressmaker Automation Settings");
                UnityEngine.Object.DontDestroyOnLoad(host);
                instance = host.AddComponent<AutomationPanel>();
                instance.Configure(patchStatus);
                Debug.Log("[DressmakerAutomation.UI] Settings ready. Menu opens at startup; F8 toggles it.");
            }
            catch (Exception error)
            {
                if (host != null) UnityEngine.Object.Destroy(host);
                Debug.LogError("[DressmakerAutomation.UI] Could not initialize settings: " + error);
            }
        }
    }
}
