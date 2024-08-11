using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Modding;
using Game.Settings;
using OSMExport.Systems;

namespace OSMExport
{
    [FileLocation(nameof(OSMExport))]
    [SettingsUIGroupOrder(kOSMExportGroup)]
    [SettingsUIShowGroupName(kOSMExportGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";

        public const string kOSMExportGroup = "OSM Export";

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kSection, kOSMExportGroup)]
        [SettingsUITextInput]
        public string FileName
        {
            get
            {
                return OSMExportSystem.FileName;
            }
            set
            {
                OSMExportSystem.FileName = value;
            }
        }

        [SettingsUISection(kSection, kOSMExportGroup)]
        public bool ExportOSM
        {
            set
            {
                OSMExportSystem.activated = true;
            }
        }



        public override void SetDefaults()
        {

        }
    }

    public class LocaleEN : IDictionarySource
    {
        private readonly Setting m_Setting;
        public LocaleEN(Setting setting)
        {
            m_Setting = setting;
        }
        public IEnumerable<KeyValuePair<string, string>> ReadEntries(IList<IDictionaryEntryError> errors, Dictionary<string, int> indexCounts)
        {
            return new Dictionary<string, string>
            {
                { m_Setting.GetSettingsLocaleID(), "OSM Exporter" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kOSMExportGroup), "OSM Export" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FileName)), "Output file name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FileName)), $"The file name should have the .osm file suffix." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportOSM)), "Export to OSM" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportOSM)), $"A file will be created in the ModsData directory. This may take up to a minute!" },

            };
        }

        public void Unload()
        {

        }
    }
}
