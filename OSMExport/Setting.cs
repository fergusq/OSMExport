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
        public const string kOSMExportButtonGroup = "OSM Export Buttons";

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kSection, kOSMExportGroup)]
        public OSMExportSystem.Direction NorthOverride
        {
            get
            {
                return OSMExportSystem.NorthOverride;
            }
            set
            {
                OSMExportSystem.NorthOverride = value;
            }
        }

        [SettingsUISection(kSection, kOSMExportGroup)]
        public bool EnableMotorways {
            get
            {
                return OSMExportSystem.EnableMotorways;
            }
            set
            {
                OSMExportSystem.EnableMotorways = value;
            }
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
        [SettingsUIButtonGroup(kOSMExportButtonGroup)]
        public bool ExportOSM
        {
            set
            {
                OSMExportSystem.Activated = true;
                OSMExportSystem.ExportPBF = false;
            }
        }

        [SettingsUISection(kSection, kOSMExportGroup)]
        [SettingsUIButtonGroup(kOSMExportButtonGroup)]
        [SettingsUIHidden]
        public bool ExportPBF
        {
            set
            {
                OSMExportSystem.Activated = true;
                OSMExportSystem.ExportPBF = true;
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
                { m_Setting.GetSettingsLocaleID(), "OSM Export" },
                { m_Setting.GetOptionTabLocaleID(Setting.kSection), "Main" },

                { m_Setting.GetOptionGroupLocaleID(Setting.kOSMExportGroup), "OSM Export" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.NorthOverride)), "North override" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.NorthOverride)), $"Select which direction will be north in the OSM file. For example, setting this to West will rotate the map 90° clockwise." },

                { m_Setting.GetEnumValueLocaleID(OSMExportSystem.Direction.North), "North (0°)" },
                { m_Setting.GetEnumValueLocaleID(OSMExportSystem.Direction.West), "West (90°)" },
                { m_Setting.GetEnumValueLocaleID(OSMExportSystem.Direction.South), "South (180°)" },
                { m_Setting.GetEnumValueLocaleID(OSMExportSystem.Direction.East), "East (270°)" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableMotorways)), "Enable motorways" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableMotorways)), $"If enabled, the mod will try to deduce which oneway highways are normal highways (green in the default Maperitive ruleset) and which are motorways (blue). Generally works well, but sometimes motorway ramps might be misclassified as highways or vice versa, leading to inconsistent look. If disabled, all highways will be normal (green) highways." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.FileName)), "Output file name" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.FileName)), $"The file name should have the .osm file suffix." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportOSM)), "Export to OSM" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportOSM)), $"A file will be created in the ModsData directory. This may take up to a minute!" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportPBF)), "Export to PBF" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportPBF)), $"A file will be created in the ModsData directory. This may take up to a minute!\n\nIf the file name does not end in .osm.pbf, the .pbf extension is added automatically." },

            };
        }

        public void Unload()
        {

        }
    }
}
