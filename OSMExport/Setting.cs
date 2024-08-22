using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Game.Settings;
using OSMExport.Systems;

namespace OSMExport
{
    [FileLocation(nameof(OSMExport))]
    [SettingsUIGroupOrder(kCommonExportGroup, kOSMExportGroup, kImageExportGroup, kAdvancedGroup, kTransitGroup)]
    [SettingsUIShowGroupName(kOSMExportGroup, kImageExportGroup, kTransitGroup)]
    public class Setting : ModSetting
    {
        public const string kSection = "Main";
        public const string kSectionAdvanced = "Advanced";

        public const string kCommonExportGroup = "Common Export";

        public const string kOSMExportGroup = "OSM Export";
        public const string kOSMExportButtonGroup = "OSM Export Buttons";

        public const string kImageExportGroup = "Image Export";
        public const string kImageExportButtonGroup = "Image Export Buttons";

        public const string kAdvancedGroup = "Advanced Options";
        public const string kTransitGroup = "Transport Lines";

        public Setting(IMod mod) : base(mod)
        {

        }

        [SettingsUISection(kSection, kCommonExportGroup)]
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

        [SettingsUISection(kSection, kCommonExportGroup)]
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

        [SettingsUISection(kSection, kCommonExportGroup)]
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
        [SettingsUIDisableByCondition(typeof(Setting), nameof(NotInGameOrEditor))]
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
        [SettingsUIDisableByCondition(typeof(Setting), nameof(NotInGameOrEditor))]
        [SettingsUIHidden]
        public bool ExportPBF
        {
            set
            {
                OSMExportSystem.Activated = true;
                OSMExportSystem.ExportPBF = true;
            }
        }

        [SettingsUISection(kSection, kImageExportGroup)]
        [SettingsUISlider(min = 11, max = 18, step = 1)]
        public int ZoomLevel
        {
            get
            {
                return OSMExportSystem.ZoomLevel;
            }
            set
            {
                OSMExportSystem.ZoomLevel = value;
            }
        }

        [SettingsUISection(kSection, kImageExportGroup)]
        [SettingsUIButtonGroup(kImageExportButtonGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(NotInGameOrEditor))]
        public bool ExportPDF
        {
            set
            {
                OSMExportSystem.Activated = true;
                OSMExportSystem.ExportPDF = true;
            }
        }

        // Advanced

        [SettingsUISection(kSectionAdvanced, kAdvancedGroup)]
        public bool EnableContours
        {
            get
            {
                return OSMExportSystem.EnableContours;
            }
            set
            {
                OSMExportSystem.EnableContours = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        public bool EnableNonstandardTransit
        {
            get
            {
                return OSMExportSystem.EnableNonstandardTransit;
            }
            set
            {
                OSMExportSystem.EnableNonstandardTransit = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        public bool EnableNonstandardTaxi
        {
            get
            {
                return OSMExportSystem.EnableNonstandardTaxi;
            }
            set
            {
                OSMExportSystem.EnableNonstandardTaxi = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        public bool EnableNonstandardBus
        {
            get
            {
                return OSMExportSystem.EnableNonstandardBus;
            }
            set
            {
                OSMExportSystem.EnableNonstandardBus = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        public bool EnableNonstandardTram
        {
            get
            {
                return OSMExportSystem.EnableNonstandardTram;
            }
            set
            {
                OSMExportSystem.EnableNonstandardTram = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        public bool EnableNonstandardTrain
        {
            get
            {
                return OSMExportSystem.EnableNonstandardTrain;
            }
            set
            {
                OSMExportSystem.EnableNonstandardTrain = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        public bool EnableNonstandardSubway
        {
            get
            {
                return OSMExportSystem.EnableNonstandardSubway;
            }
            set
            {
                OSMExportSystem.EnableNonstandardSubway = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        [SettingsUIHidden] // TODO
        public bool EnableNonstandardShip
        {
            get
            {
                return OSMExportSystem.EnableNonstandardShip;
            }
            set
            {
                OSMExportSystem.EnableNonstandardShip = value;
            }
        }

        [SettingsUISection(kSectionAdvanced, kTransitGroup)]
        [SettingsUIDisableByCondition(typeof(Setting), nameof(DisableNonstandardTransit))]
        [SettingsUIHidden] // TODO
        public bool EnableNonstandardAirplane
        {
            get
            {
                return OSMExportSystem.EnableNonstandardAirplane;
            }
            set
            {
                OSMExportSystem.EnableNonstandardAirplane = value;
            }
        }

        [SettingsUIHidden]
        public bool NotInGameOrEditor => !GameMode.GameOrEditor.HasFlag(GameManager.instance.gameMode);

        [SettingsUIHidden]
        public bool DisableNonstandardTransit => !EnableNonstandardTransit;


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

                { m_Setting.GetOptionGroupLocaleID(Setting.kCommonExportGroup), "Common Export Options" },

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

                { m_Setting.GetOptionGroupLocaleID(Setting.kImageExportGroup), "PDF Export" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ZoomLevel)), "Zoom level" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ZoomLevel)), $"11 makes a small image with almost no details. 18 makes a very large image with a lot of details." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.ExportPDF)), "Generate PDF" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.ExportPDF)), $"A file will be created in the ModsData directory. This may take up to a minute!\n\nIf the file name does not end in .pdf, the .pdf extension is added automatically.\n\nThis feature is experimental! It might have graphical issues and other bugs." },

                { m_Setting.GetOptionTabLocaleID(Setting.kSectionAdvanced), "Experimental" },
                { m_Setting.GetOptionGroupLocaleID(Setting.kAdvancedGroup), "Experimental options" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableContours)), "Enable contours" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableContours)), $"If enabled, contour lines will be exported as ways tagged with contour=elevation.\n\nIf the contours are not showing in Maperitive, update your ruleset to the latest version of the ruleset linked on the mod page." },

                { m_Setting.GetOptionGroupLocaleID(Setting.kTransitGroup), "Transport lines" },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardTransit)), "Enable transport lines" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardTransit)), $"Transport lines are always exported in the standard OSM way to export them. However, many programs, including Maperitive, cannot display them. If this option is enabled, transport lines will be exported as custom non-standard nodes and ways that are possible to render in most program using custom rules.\n\nIf the lines are not showing in Maperitive, update your ruleset to the latest version of the ruleset linked on the mod page." },

                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardTaxi)), "Taxi stands" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardTaxi)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardBus)), "Bus lines and stops" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardBus)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardTram)), "Tram lines and stops" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardTram)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardTrain)), "Train lines and stops" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardTrain)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardSubway)), "Subway lines and stops" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardSubway)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardShip)), "Ship lines" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardShip)), "" },
                { m_Setting.GetOptionLabelLocaleID(nameof(Setting.EnableNonstandardAirplane)), "Airplane lines" },
                { m_Setting.GetOptionDescLocaleID(nameof(Setting.EnableNonstandardAirplane)), "" },

            };
        }

        public void Unload()
        {

        }
    }
}
