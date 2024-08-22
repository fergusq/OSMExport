using System.IO;
using System.Reflection;
using Colossal.IO.AssetDatabase;
using Colossal.Logging;
using Game;
using Game.Modding;
using Game.SceneFlow;
using OSMExport.Systems;

namespace OSMExport
{
    public class Mod : IMod
    {
        public static ILog Log { get; } = LogManager.GetLogger($"{nameof(OSMExport)}.{nameof(Mod)}").SetShowsErrorsInUI(false);
        private Setting m_Setting;

        // Copied from https://github.com/algernon-A/UnifiedIconLibrary/blob/master/Code/Mod.cs
        // By algernon licensed under the Apache 2.0 license
        private static string s_assemblyPath;
        public static string AssemblyPath
        {
            get
            {
                // Update cached path if the existing one is invalid.
                if (string.IsNullOrWhiteSpace(s_assemblyPath))
                {
                    // No path cached - find current executable asset.
                    string assemblyName = Assembly.GetExecutingAssembly().FullName;
                    ExecutableAsset modAsset = AssetDatabase.global.GetAsset(SearchFilter<ExecutableAsset>.ByCondition(x => x.definition?.FullName == assemblyName));
                    if (modAsset is null)
                    {
                        Log.Error("mod executable asset not found");
                        return null;
                    }

                    // Update cached path.
                    s_assemblyPath = Path.GetDirectoryName(modAsset.GetMeta().path);
                }

                // Return cached path.
                return s_assemblyPath;
            }
        }

        public void OnLoad(UpdateSystem updateSystem)
        {
            Log.Info(nameof(OnLoad));

            if (GameManager.instance.modManager.TryGetExecutableAsset(this, out var asset))
                Log.Info($"Current mod asset at {asset.path}");

            m_Setting = new Setting(this);
            m_Setting.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US", new LocaleEN(m_Setting));

            updateSystem.UpdateAfter<OSMExportSystem>(SystemUpdatePhase.UIUpdate);

            AssetDatabase.global.LoadSettings(nameof(OSMExport), m_Setting, new Setting(this));
        }

        public void OnDispose()
        {
            Log.Info(nameof(OnDispose));
            if (m_Setting != null)
            {
                m_Setting.UnregisterInOptionsUI();
                m_Setting = null;
            }
        }
    }
}
