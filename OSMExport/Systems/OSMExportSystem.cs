using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Colossal.Entities;
using Colossal.Logging;
using Colossal.PSI.Environment;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.SceneFlow;
using Game.Simulation;
using Game.Tools;
using Game.UI;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OSMExport.Systems
{
    internal partial class OSMExportSystem : GameSystemBase
    {
        internal static bool activated;

        private EntityQuery m_EdgeQuery;
        private EntityQuery m_NodeQuery;
        private EntityQuery m_AreaQuery;
        private EntityQuery m_BuildingQuery;

        private TerrainSystem m_TerrainSystem;
        private WaterSystem m_WaterSystem;
        private NameSystem m_NameSystem;

        private ILog m_Logger;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_EdgeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Net.Edge>(),
                        ComponentType.ReadOnly<Game.Net.Aggregated>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                        ComponentType.ReadOnly<Game.Net.SubLane>(),
                    },
                Any = new ComponentType[]
                    {
                    },
                None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Hidden>(),
                    }
            }
            );
            m_NodeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Net.Node>(),
                    },
                Any = new ComponentType[]
                    {

                    },
                None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Hidden>(),
                    }
            }
            );
            m_AreaQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Areas.Area>(),
                    },
                Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Areas.Lot>(),
                        ComponentType.ReadOnly<Game.Areas.District>(),
                    },
                None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Hidden>(),
                    }
            }
            );
            m_BuildingQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Buildings.Building>(),
                        ComponentType.ReadOnly<Game.Objects.Transform>(),
                    },
                Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Buildings.School>(),
                        ComponentType.ReadOnly<Game.Buildings.ParkingFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.IndustrialProperty>(),
                        ComponentType.ReadOnly<Game.Buildings.CommercialProperty>(),
                        ComponentType.ReadOnly<Game.Buildings.TransportDepot>(),
                        ComponentType.ReadOnly<Game.Buildings.Hospital>(),
                        ComponentType.ReadOnly<Game.Buildings.PoliceStation>(),
                        ComponentType.ReadOnly<Game.Buildings.FireStation>(),
                        ComponentType.ReadOnly<Game.Buildings.PostFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.Prison>(),
                        ComponentType.ReadOnly<Game.Buildings.ElectricityProducer>(),
                    },
                None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Hidden>(),
                    }
            }
            );

            m_TerrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = World.GetOrCreateSystemManaged<WaterSystem>();
            m_NameSystem = World.GetOrCreateSystemManaged<NameSystem>();

            m_Logger = LogManager.GetLogger($"{nameof(OSMExport)}.{nameof(OSMExportSystem)}");
        }
        readonly struct GeoCoordinate
        {
            public double Latitude { get; }
            public double Longitude { get; }

            public GeoCoordinate(double latitude, double longitude)
            {
                Latitude = latitude;
                Longitude = longitude;
            }

            public static GeoCoordinate FromGameCoordinages(float x, float z)
            {
                return new GeoCoordinate(-x / 111_000, z / 111_000);
            }
        }

        enum HighwayType
        {
            Motorway,
            Trunk,
            Primary,
            Secondary,
            Tertiary,
            Unclassified,
            Residential,
            Road,
            NotHighway,
        }

        class Highway
        {
            public int Id { get; set; }
            public List<string> Nodes { get; set; }
            public string Tags { get; set; }
            public HighwayType Type { get; set; }
            public bool IsLink { get; set; }
            public bool MightBeLink { get; set; }
            public List<int> NodeIds { get; set; }

            public Highway(int id)
            {
                Id = id;
                Nodes = new List<string>();
                Type = HighwayType.NotHighway;
                IsLink = false;
                MightBeLink = false;
                NodeIds = new List<int>();
            }

            private string GetTags()
            {
                if (Type == HighwayType.NotHighway) return Tags;
                string highwayType;
                switch (Type)
                {
                    case HighwayType.Motorway:
                        highwayType = "motorway";
                        break;
                    case HighwayType.Trunk:
                        highwayType = "trunk";
                        break;
                    case HighwayType.Primary:
                        highwayType = "primary";
                        break;
                    case HighwayType.Secondary:
                        highwayType = "secondary";
                        break;
                    case HighwayType.Tertiary:
                        highwayType = "tertiary";
                        break;
                    case HighwayType.Unclassified:
                        highwayType = "unclassified";
                        break;
                    case HighwayType.Residential:
                        highwayType = "residential";
                        break;
                    case HighwayType.Road:
                        highwayType = "road";
                        break;
                    default:
                        highwayType = "road";
                        break;
                }
                if (IsLink)
                {
                    highwayType += "_link";
                }
                return Tags + $"<tag k=\"highway\" v=\"{highwayType}\" />";
            }

            public void ToXml(List<string> wayXml)
            {
                var id = $"20{Id}";
                wayXml.Add($"<way id=\"{id}\" version=\"5\">");
                foreach (var node in Nodes)
                {
                    wayXml.Add(node);
                }
                wayXml.Add(GetTags());
                wayXml.Add("</way>");
            }
        }

        public static string FileName = "export.osm";

        protected override void OnUpdate()
        {
            if (!activated) return;
            activated = false;

            var directory = Path.Combine(
                    EnvPath.kUserDataPath,
                    "ModsData",
                    "MapMaker");
            var dir = new DirectoryInfo(directory);

            if (!dir.Exists)
            {
                dir.Create();
            }

            m_Logger.Info("Exporting OSM...");

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            List<string> nodeXml = new List<string>();
            List<string> wayXml = new List<string>();
            List<string> relationXml = new List<string>();

            AddHighways(nodeXml, wayXml, relationXml);
            AddAreas(nodeXml, wayXml, relationXml);
            AddBuildinds(nodeXml, wayXml, relationXml);
            AddWaterBodies(nodeXml, wayXml, relationXml);

            m_Logger.Info("Generating complete xml...");

            Bounds bounds = m_TerrainSystem.GetTerrainBounds();

            GeoCoordinate minBounds = GeoCoordinate.FromGameCoordinages(bounds.min.x, bounds.min.z);
            GeoCoordinate maxBounds = GeoCoordinate.FromGameCoordinages(bounds.max.x, bounds.max.z);

            string xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                         "<osm version=\"0.6\" generator=\"MapMaker\">\n" +
                         "<bounds minlat=\"" + minBounds.Latitude + "\" minlon=\"" + minBounds.Longitude + "\" maxlat=\"" + maxBounds.Latitude + "\" maxlon=\"" + maxBounds.Longitude + "\"/>\n" +
                         string.Join("\n", nodeXml) + "\n" +
                         string.Join("\n", wayXml) + "\n" +
                         string.Join("\n", relationXml) + "\n" +
                         "</osm>";

            m_Logger.Info("Saving to disk...");

            File.WriteAllText(Path.Combine(directory, FileName), xml);

            CultureInfo.CurrentCulture = oldCulture;
        }

        private void AddHighways(List<string> nodeXml, List<string> wayXml, List<string> relationXml)
        {

            NativeArray<Entity> nodeEntities = m_NodeQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> edgeEntities = m_EdgeQuery.ToEntityArray(Allocator.Temp);

            m_Logger.Info("Generating net node xml...");

            for (int i = 0; i < nodeEntities.Length; i++)
            {
                var entity = nodeEntities[i];
                var position = EntityManager.GetComponentData<Game.Net.Node>(entity).m_Position;
                var coordinates = GeoCoordinate.FromGameCoordinages(position.x, position.z);
                var id = $"10{entity.Index}";

                nodeXml.Add($"<node id=\"{id}\" lat=\"{coordinates.Latitude}\" lon=\"{coordinates.Longitude}\" version=\"1\"></node>");
            }

            m_Logger.Info("Generating net ways...");

            List<Highway> ways = new List<Highway>();
            Dictionary<int, List<Highway>> nodeToHighways = new Dictionary<int, List<Highway>>();
            Dictionary<int, List<Highway>> aggregateToHighways = new Dictionary<int, List<Highway>>();

            for (int i = 0; i < edgeEntities.Length; i++)
            {
                var entity = edgeEntities[i];
                var edge = EntityManager.GetComponentData<Game.Net.Edge>(entity);
                var aggregated = EntityManager.GetComponentData<Game.Net.Aggregated>(entity);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var subLanes = EntityManager.GetBuffer<Game.Net.SubLane>(entity, true);
                var hasOwner = EntityManager.HasComponent<Owner>(entity);
                var isElevated = EntityManager.TryGetComponent<Game.Net.Elevation>(entity, out var elevation);
                var aggregateLen = EntityManager.TryGetBuffer<Game.Net.AggregateElement>(aggregated.m_Aggregate, true, out var aggregateElements) ? aggregateElements.Length : 0;

                Highway way = new Highway(entity.Index);

                bool isTwoWay = false;
                HashSet<int> lanes = new HashSet<int>();

                foreach (var subLane in subLanes)
                {
                    if (EntityManager.TryGetComponent<Game.Net.CarLane>(subLane.m_SubLane, out var carLane))
                    {
                        if ((carLane.m_Flags & (Game.Net.CarLaneFlags.Twoway | Game.Net.CarLaneFlags.Invert)) != 0)
                        {
                            isTwoWay = true;
                        }
                        if (EntityManager.TryGetComponent<Game.Net.Lane>(subLane.m_SubLane, out var lane))
                        {
                            lanes.Add(lane.m_StartNode.GetLaneIndex());
                        }
                    }
                }

                int numLanes = lanes.Count;

                if (EntityManager.TryGetComponent<RoadData>(prefabRef, out var roadData))
                {
                    if (!isTwoWay && aggregateLen < 8)
                    {
                        way.MightBeLink = true;
                    }

                    int speedLimit = 0;
                    if (roadData.m_SpeedLimit > 40)
                    {
                        if (roadData.m_SpeedLimit > 40 && !isTwoWay)
                        {
                            way.Type = HighwayType.Motorway;
                        }
                        else
                        {
                            way.Type = HighwayType.Trunk;
                        }
                        if (roadData.m_SpeedLimit > 50)
                        {
                            speedLimit = 100;
                        }
                        else
                        {
                            speedLimit = 80;
                        }
                    }
                    else if (roadData.m_SpeedLimit > 30)
                    {
                        way.Type = HighwayType.Primary;
                        speedLimit = 60;
                    }
                    else if (roadData.m_SpeedLimit > 25)
                    {
                        way.Type = HighwayType.Secondary;
                        speedLimit = 50;
                    }
                    else if (roadData.m_SpeedLimit > 20)
                    {
                        way.Type = HighwayType.Residential;
                        speedLimit = 40;
                    }
                    else if (roadData.m_SpeedLimit > 10)
                    {
                        way.Type = HighwayType.Residential;
                        speedLimit = 30;
                    }
                    else
                    {
                        way.Type = HighwayType.Road;
                    }

                    way.Tags += $"<tag k=\"maxspeed\" v=\"{speedLimit}\"/>";

                    if (numLanes >= 1)
                    {
                        way.Tags += $"<!-- <tag k=\"lanes\" v=\"{numLanes}\"/> -->";
                        way.Tags += $"<!-- {string.Join(";", lanes.ToList().Select(s => s.ToString()))} -->";
                    }

                    if (!isTwoWay)
                    {
                        way.Tags += "<tag k=\"oneway\" v=\"yes\"/>";
                    }

                    var name = m_NameSystem.GetName(aggregated.m_Aggregate);
                    way.Tags += "<tag k=\"name\" v=\"" + NameToString(name) + "\"/>";
                }
                else if (EntityManager.TryGetComponent<PathwayData>(prefabRef, out var pathwayData))
                {

                    if (hasOwner)
                    {
                        continue;
                    }

                    way.Tags += "<tag k=\"highway\" v=\"footway\"/>";
                }
                else if (EntityManager.HasComponent<Game.Net.TramTrack>(entity))
                {
                    way.Tags += "<tag k=\"railway\" v=\"tram\"/>";
                }
                else if (EntityManager.HasComponent<Game.Net.SubwayTrack>(entity))
                {
                    way.Tags += "<tag k=\"railway\" v=\"subway\"/>";
                }
                else if (EntityManager.TryGetComponent<TrackData>(prefabRef, out var trackData))
                {
                    way.Tags += "<tag k=\"railway\" v=\"rail\"/><tag k=\"usage\" v=\"main\"/>";
                }
                else if (EntityManager.HasComponent<Game.Net.ElectricityConnection>(entity))
                {
                    way.Tags += "<tag k=\"power\" v=\"line\"/>";
                }
                else
                {
                    continue;
                }

                if (isElevated)
                {
                    if (elevation.m_Elevation.x > 0.1f)
                    {
                        way.Tags += "<tag k=\"bridge\" v=\"yes\"/><tag k=\"layer\" v=\"1\"/>";
                    }
                    else if (elevation.m_Elevation.x < -0.1f)
                    {
                        way.Tags += "<tag k=\"tunnel\" v=\"yes\"/><tag k=\"layer\" v=\"-1\"/>";
                    }
                }

                way.Nodes.Add($"<nd ref=\"10{edge.m_Start.Index}\"/>");
                way.Nodes.Add($"<nd ref=\"10{edge.m_End.Index}\"/>");

                if (!nodeToHighways.ContainsKey(edge.m_Start.Index)) nodeToHighways[edge.m_Start.Index] = new List<Highway>();
                nodeToHighways[edge.m_Start.Index].Add(way);
                if (!nodeToHighways.ContainsKey(edge.m_End.Index)) nodeToHighways[edge.m_End.Index] = new List<Highway>();
                nodeToHighways[edge.m_End.Index].Add(way);
                if (!aggregateToHighways.ContainsKey(aggregated.m_Aggregate.Index)) aggregateToHighways[aggregated.m_Aggregate.Index] = new List<Highway>();
                aggregateToHighways[aggregated.m_Aggregate.Index].Add(way);

                way.NodeIds = new List<int>() { edge.m_Start.Index, edge.m_End.Index };

                ways.Add(way);
            }

            m_Logger.Info("Updating link road types");

            // This will update ways which are "link-like" to be link roads of appropriate type
            foreach (var aggregate in aggregateToHighways.Values)
            {
                var aggregateIds = aggregate.Select(way => way.Id);
                var neighbours = aggregate.SelectMany(way => way.NodeIds).SelectMany(nodeId => nodeToHighways[nodeId]).Where(way => !aggregateIds.Contains(way.Id) && !way.MightBeLink);
                foreach (var way in aggregate)
                {
                    if (way.MightBeLink)
                    {
                        if (neighbours.Any(w => w.Type == HighwayType.Motorway))
                        {
                            way.IsLink = true;
                            way.Type = HighwayType.Motorway;
                        }
                        else if (neighbours.Any(w => w.Type == HighwayType.Trunk))
                        {
                            way.IsLink = true;
                            way.Type = HighwayType.Trunk;
                        }
                        else if (neighbours.Any(w => w.Type == HighwayType.Primary))
                        {
                            way.IsLink = true;
                            way.Type = HighwayType.Primary;
                        }
                        else if (neighbours.Any(w => w.Type == HighwayType.Secondary))
                        {
                            way.IsLink = true;
                            way.Type = HighwayType.Secondary;
                        }
                    }
                }
            }

            m_Logger.Info("Generating net way xml...");

            foreach (var way in ways)
            {
                if (way.Nodes.Count < 2) continue;

                way.ToXml(wayXml);
            }

            nodeEntities.Dispose();
            edgeEntities.Dispose();
        }

        private void AddAreas(List<string> nodeXml, List<string> wayXml, List<string> relationXml)
        {
            m_Logger.Info("Generating areas...");

            NativeArray<Entity> areaEntities = m_AreaQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < areaEntities.Length; i++)
            {
                var entity = areaEntities[i];
                var nodes = EntityManager.GetBuffer<Game.Areas.Node>(entity, true);

                if (EntityManager.HasComponent<Game.Areas.District>(entity))
                {
                    // Find the center of the district
                    float3 center = new float3(0, 0, 0);
                    for (int j = 0; j < nodes.Length; j++)
                    {
                        center += nodes[j].m_Position;
                    }
                    center /= nodes.Length;

                    var coordinates = GeoCoordinate.FromGameCoordinages(center.x, center.z);
                    var id = $"30{entity.Index}00";
                    var name = m_NameSystem.GetName(entity);

                    nodeXml.Add($"<node id=\"{id}\" lat=\"{coordinates.Latitude}\" lon=\"{coordinates.Longitude}\" version=\"1\">");
                    nodeXml.Add($"<tag k=\"place\" v=\"suburb\"/><tag k=\"name\" v=\"{NameToString(name)}\"/></node>");
                }
                else if (EntityManager.HasComponent<Game.Areas.Lot>(entity))
                {
                    string tag = "<tag k=\"landuse\" v=\"farmland\" />";
                    if (EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
                    {
                        if (EntityManager.TryGetComponent<ExtractorAreaData>(prefabRef.m_Prefab, out var extractorAreaData))
                        {
                            switch (extractorAreaData.m_MapFeature)
                            {
                                case Game.Areas.MapFeature.FertileLand:
                                    tag = "<tag k=\"landuse\" v=\"farmland\" />";
                                    break;
                                case Game.Areas.MapFeature.Forest:
                                    tag = "<tag k=\"landuse\" v=\"forest\" />";
                                    break;
                                case Game.Areas.MapFeature.Oil:
                                    tag = "<tag k=\"landuse\" v=\"industrial\" /><tag k=\"industrial\" v=\"oil\" />";
                                    break;
                                case Game.Areas.MapFeature.Ore:
                                    tag = "<tag k=\"landuse\" v=\"quarry\" />";
                                    break;
                            }
                        }
                        else if (EntityManager.TryGetComponent<StorageAreaData>(prefabRef.m_Prefab, out var storageAreaData))
                        {
                            switch (storageAreaData.m_Resources)
                            {
                                case Game.Economy.Resource.Garbage:
                                    tag = "<tag k=\"landuse\" v=\"landfill\" />";
                                    break;
                            }
                        }
                    }

                    for (int j = 0; j < nodes.Length; j++)
                    {
                        var node = nodes[j];
                        var position = node.m_Position;
                        var coordinates = GeoCoordinate.FromGameCoordinages(position.x, position.z);
                        var id = $"30{entity.Index}00{j}";

                        nodeXml.Add($"<node id=\"{id}\" lat=\"{coordinates.Latitude}\" lon=\"{coordinates.Longitude}\" version=\"1\"></node>");
                    }

                    if (nodes.Length < 3) continue;

                    // Add the way

                    var wayId = $"30{entity.Index}01";

                    wayXml.Add($"<way id=\"{wayId}\" version=\"5\">");
                    for (int j = 0; j < nodes.Length; j++)
                    {
                        var id = $"30{entity.Index}00{j}";
                        wayXml.Add($"<nd ref=\"{id}\" />");
                    }
                    wayXml.Add($"<nd ref=\"30{entity.Index}00{0}\" />");
                    wayXml.Add(tag);
                    wayXml.Add("</way>");
                }
            }

            areaEntities.Dispose();
        }

        private void AddBuildinds(List<string> nodeXml, List<string> wayXml, List<string> relationXml)
        {
            m_Logger.Info("Generating buildings...");

            NativeArray<Entity> buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < buildingEntities.Length; i++)
            {
                var entity = buildingEntities[i];
                var id = $"60{entity.Index}";
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);
                var coordinates = GeoCoordinate.FromGameCoordinages(transform.m_Position.x, transform.m_Position.z);

                var tag = "";
                if (EntityManager.HasComponent<Game.Buildings.School>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"school\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.ParkingFacility>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"parking\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.IndustrialProperty>(entity))
                {
                    // TODO: warehouses
                    tag = "<tag k=\"landuse\" v=\"industrial\"/><tag k=\"industrial\" v=\"factory\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.CommercialProperty>(entity))
                {
                    tag = "<tag k=\"shop\" v=\"yes\"/>";
                    if (EntityManager.TryGetBuffer<Game.Buildings.Renter>(entity, true, out var renters) && renters.Length > 0)
                    {
                        foreach (var renter in renters)
                        {
                            if (EntityManager.TryGetComponent<PrefabRef>(renter, out var renterPrefabRef))
                            {
                                if (EntityManager.TryGetComponent<IndustrialProcessData>(renterPrefabRef.m_Prefab, out var industrialProcessData))
                                {
                                    switch (industrialProcessData.m_Output.m_Resource)
                                    {
                                        case Game.Economy.Resource.Beverages:
                                            tag = "<tag k=\"shop\" v=\"alcohol\"/>";
                                            break;
                                        case Game.Economy.Resource.Chemicals:
                                            tag = "<tag k=\"shop\" v=\"chemist\"/>";
                                            break;
                                        case Game.Economy.Resource.ConvenienceFood:
                                            tag = "<tag k=\"shop\" v=\"convenience\"/>";
                                            break;
                                        case Game.Economy.Resource.Food:
                                            tag = "<tag k=\"shop\" v=\"food\"/>";
                                            break;
                                        case Game.Economy.Resource.Furniture:
                                            tag = "<tag k=\"shop\" v=\"furniture\"/>";
                                            break;
                                        case Game.Economy.Resource.Electronics:
                                            tag = "<tag k=\"shop\" v=\"electronics\"/>";
                                            break;
                                        case Game.Economy.Resource.Paper:
                                            tag = "<tag k=\"shop\" v=\"books\"/>";
                                            break;
                                        case Game.Economy.Resource.Petrochemicals:
                                            tag = "<tag k=\"amenity\" v=\"fuel\"/>";
                                            break;
                                        case Game.Economy.Resource.Pharmaceuticals:
                                            tag = "<tag k=\"amenity\" v=\"pharmacy\"/>";
                                            break;
                                        case Game.Economy.Resource.Plastics:
                                            tag = "<tag k=\"shop\" v=\"gift\"/>";
                                            break;
                                        case Game.Economy.Resource.Textiles:
                                            tag = "<tag k=\"shop\" v=\"clothes\"/>";
                                            break;
                                        case Game.Economy.Resource.Vehicles:
                                            tag = "<tag k=\"shop\" v=\"car\"/>";
                                            break;
                                        case Game.Economy.Resource.Meals:
                                            tag = "<tag k=\"amenity\" v=\"restaurant\"/>";
                                            break;
                                        case Game.Economy.Resource.Entertainment:
                                            tag = "<tag k=\"amenity\" v=\"bar\"/>";
                                            break;
                                        case Game.Economy.Resource.Recreation: // TODO this is not really accurate
                                            tag = "<tag k=\"amenity\" v=\"arts_centre\"/>";
                                            break;
                                        case Game.Economy.Resource.Lodging:
                                            tag = "<tag k=\"tourism\" v=\"hotel\"/>";
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
                else if (EntityManager.HasComponent<Game.Buildings.TransportDepot>(entity))
                {
                    tag = "<tag k=\"landuse\" v=\"industrial\"/><tag k=\"industrial\" v=\"depot\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.Hospital>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"hospital\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.PoliceStation>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"police\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.FireStation>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"fire_station\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.PostFacility>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"post_office\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.Prison>(entity))
                {
                    tag = "<tag k=\"amenity\" v=\"prison\"/>";
                }
                else if (EntityManager.HasComponent<Game.Buildings.ElectricityProducer>(entity))
                {
                    tag = "<tag k=\"landuse\" v=\"industrial\"/><tag k=\"power\" v=\"plant\"/>";
                }
                else
                {
                    continue;
                }

                var name = m_NameSystem.GetName(entity);
                tag += $"<tag k=\"name\" v=\"{NameToString(name)}\"/>";

                if (EntityManager.TryGetComponent<BuildingData>(prefabRef, out var buildingData))
                {
                    float halfWidth = 4f * buildingData.m_LotSize.x; // Lot size is 8 m, half is 4 m
                    float halfLength = 4f * buildingData.m_LotSize.y;

                    float3 corner1pos = Rotate(new float3(-halfWidth, 0, -halfLength), transform.m_Rotation) + transform.m_Position;
                    float3 corner2pos = Rotate(new float3(halfWidth, 0, -halfLength), transform.m_Rotation) + transform.m_Position;
                    float3 corner3pos = Rotate(new float3(halfWidth, 0, halfLength), transform.m_Rotation) + transform.m_Position;
                    float3 corner4pos = Rotate(new float3(-halfWidth, 0, halfLength), transform.m_Rotation) + transform.m_Position;

                    var corner1 = GeoCoordinate.FromGameCoordinages(corner1pos.x, corner1pos.z);
                    var corner2 = GeoCoordinate.FromGameCoordinages(corner2pos.x, corner2pos.z);
                    var corner3 = GeoCoordinate.FromGameCoordinages(corner3pos.x, corner3pos.z);
                    var corner4 = GeoCoordinate.FromGameCoordinages(corner4pos.x, corner4pos.z);

                    nodeXml.Add($"<node id=\"{id}001\" lat=\"{corner1.Latitude}\" lon=\"{corner1.Longitude}\" version=\"1\" />");
                    nodeXml.Add($"<node id=\"{id}002\" lat=\"{corner2.Latitude}\" lon=\"{corner2.Longitude}\" version=\"1\" />");
                    nodeXml.Add($"<node id=\"{id}003\" lat=\"{corner3.Latitude}\" lon=\"{corner3.Longitude}\" version=\"1\" />");
                    nodeXml.Add($"<node id=\"{id}004\" lat=\"{corner4.Latitude}\" lon=\"{corner4.Longitude}\" version=\"1\" />");

                    // Add the way

                    var wayId = $"60{entity.Index}01";

                    wayXml.Add($"<way id=\"{wayId}\" version=\"5\">");
                    wayXml.Add($"<nd ref=\"{id}001\" />");
                    wayXml.Add($"<nd ref=\"{id}002\" />");
                    wayXml.Add($"<nd ref=\"{id}003\" />");
                    wayXml.Add($"<nd ref=\"{id}004\" />");
                    wayXml.Add($"<nd ref=\"{id}001\" />");
                    wayXml.Add(tag);
                    wayXml.Add("</way>");
                }
                else
                {
                    nodeXml.Add($"<node id=\"{id}\" lat=\"{coordinates.Latitude}\" lon=\"{coordinates.Longitude}\" version=\"1\">{tag}</node>");
                }
            }

            buildingEntities.Dispose();
        }

        private void AddWaterBodies(List<string> nodeXml, List<string> wayXml, List<string> relationXml)
        {
            m_Logger.Info("Generating water bodies...");

            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            WaterSurfaceData waterSurfaceData = m_WaterSystem.GetSurfaceData(out var deps);
            Bounds bounds = m_TerrainSystem.GetTerrainBounds();

            const int rectSize = 20;

            HashSet<(int, int)> waterNodes = new HashSet<(int, int)>();

            int xi = 0;
            for (float x = bounds.min.x; x < bounds.max.x; x += rectSize, xi += 3)
            {
                int zi = 0;
                for (float z = bounds.min.z; z < bounds.max.z; z += rectSize, zi += 3)
                {
                    float3 position = new float3(x, 0, z);
                    if (IsUnderwater(waterSurfaceData, heightData, position, out var height, out var depth))
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                waterNodes.Add((xi + i, zi + j));
                            }
                        }
                    }
                }
            }

            m_Logger.Info($"Found {waterNodes.Count} water nodes!");

            int waterBodies = 0;

            m_Logger.Info("Finding water bodies...");

            Dictionary<(int, int), int> waterNodeToWaterBody = new Dictionary<(int, int), int>();

            // Find all water bodies
            while (waterNodes.Count > 0)
            {
                var wb = waterBodies++;

                var node = waterNodes.First();
                waterNodes.Remove(node);

                Queue<(int, int)> queue = new Queue<(int, int)>();
                queue.Enqueue(node);

                int count = 0;
                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    waterNodeToWaterBody[current] = wb;
                    count++;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (math.abs(dx) + math.abs(dy) != 1) continue;
                            var next = (current.Item1 + dx, current.Item2 + dy);

                            if (waterNodes.Contains(next))
                            {
                                waterNodes.Remove(next);
                                queue.Enqueue(next);
                            }
                        }
                    }
                }
                m_Logger.Info($"Found water body {wb} with {count} nodes!");
            }

            m_Logger.Info($"Found {waterBodies} water bodies!");

            for (int wb = 0; wb < waterBodies; wb++)
            {

                m_Logger.Info($"Finding nodes for wb {wb}...");

                // Find all nodes in this water body
                var waterBodyNodes = waterNodeToWaterBody.Where(kv => kv.Value == wb).Select(kv => kv.Key).ToList();
                var waterBodyNodeSet = new HashSet<(int, int)>(waterBodyNodes);

                m_Logger.Info($"Finding edges for wb {wb}...");

                // Construct a list of edges
                HashSet<(int, int)> edges = new HashSet<(int, int)>();
                for (int i = 0; i < waterBodyNodes.Count; i++)
                {
                    var node = waterBodyNodes[i];
                    if (!waterBodyNodeSet.Contains((node.Item1 - 1, node.Item2)) ||
                        !waterBodyNodeSet.Contains((node.Item1 + 1, node.Item2)) ||
                        !waterBodyNodeSet.Contains((node.Item1, node.Item2 - 1)) ||
                        !waterBodyNodeSet.Contains((node.Item1, node.Item2 + 1)))
                    {
                        edges.Add(node);
                    }
                }
                /*
                var maxX = edges.Max(x => x.Item1);
                var maxY = edges.Max(x => x.Item2);

                var wbMap = new char[maxX, maxY];
                for (int x = 0; x < maxX; x++)
                {
                    for (int y = 0; y < maxY; y++)
                    {
                        wbMap[x, y] = edges.Contains((x, y)) ? 'E' : waterBodyNodeSet.Contains((x, y)) ? '0' : ' ';
                    }
                }

                // Print wbMap to file

                using (var writer = new StreamWriter(Path.Combine(directory, $"wb_{wb}.txt")))
                {
                    for (int y = 0; y < maxY; y++)
                    {
                        for (int x = 0; x < maxX; x++)
                        {
                            writer.Write(wbMap[x, y]);
                        }
                        writer.WriteLine();
                    }
                }*/

                m_Logger.Info($"Sorting {edges.Count} edges for wb {wb}...");

                // The outerEdges array contains both the "outer" edge and the "inner" edge (i.e. the holes)
                // We must first find a node that belongs to the outer edge

                m_Logger.Info($"Sorting outer edge for wb {wb}...");

                var outerEdgesList = edges.ToList();
                outerEdgesList.Sort((x1, x2) => x2.Item1 - x1.Item1);
                var currentEdge = outerEdgesList.First();

                edges.Remove(currentEdge);

                var sortedOuterEdges = new List<(int, int)> { currentEdge };
                var sortedInnerEdges = new List<List<(int, int)>>();
                var currentSortedEdges = sortedOuterEdges;

                (int, int)[] directions = new (int, int)[]
                {
                    (1, -1),
                    (0, -1),
                    (-1, -1),
                    (-1, 0),
                    (-1, 1),
                    (0, 1),
                    (1, 1),
                    (1, 0),
                };

                // Find the outer outer edges
                while (true)
                {
                    var direction = 4;
                    var canSkip = 0;
                    while (true)
                    {
                        // Find the next edge
                        bool found = false;
                        for (int i = direction; i < direction + 8; i++)
                        {
                            var (dx, dy) = directions[i % 8];
                            var nextEdge = (currentEdge.Item1 + dx, currentEdge.Item2 + dy);
                            if (edges.Contains(nextEdge))
                            {
                                if (direction == i % 8)
                                {
                                    // We are continuing the same line, so no need for the previous node
                                    currentSortedEdges.RemoveAt(currentSortedEdges.Count - 1);
                                }
                                if (canSkip > 0)
                                {
                                    canSkip--;
                                }
                                else
                                {
                                    currentSortedEdges.Add(nextEdge);
                                    canSkip = 3;
                                }
                                currentEdge = nextEdge;
                                edges.Remove(nextEdge);
                                direction = (i + 8 - 3) % 8;
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            break;
                        }
                    }

                    if (edges.Count == 0) break;

                    m_Logger.Info($"Sorting inner edge {sortedInnerEdges.Count} for wb {wb}...");

                    // All of the remaining edges are inner edges 
                    List<(int, int)> innerEdgeSorted = new List<(int, int)>();
                    sortedInnerEdges.Add(innerEdgeSorted);
                    currentSortedEdges = innerEdgeSorted;

                    var innerEdgesList = edges.ToList();
                    innerEdgesList.Sort((x1, x2) => x2.Item1 - x1.Item1);
                    currentEdge = innerEdgesList.First();

                    edges.Remove(currentEdge);
                    innerEdgeSorted.Add(currentEdge);
                }



                m_Logger.Info($"Sorted 1 outer edge and {sortedInnerEdges.Count} inner edges for wb {wb}!");

                m_Logger.Info($"Generating water body xml for wb {wb}...");

                // Construct the XML
                var allEdges = new List<(int, int)>();
                allEdges.AddRange(sortedOuterEdges);
                allEdges.AddRange(sortedInnerEdges.SelectMany(s => s));
                for (int i = 0; i < allEdges.Count; i++)
                {
                    var node = allEdges[i];
                    var x = bounds.min.x + node.Item1 * rectSize / 3;
                    var z = bounds.min.z + node.Item2 * rectSize / 3;
                    var coordinates = GeoCoordinate.FromGameCoordinages(x, z);
                    var id = $"40{wb}00{node.Item1}00{node.Item2}";

                    nodeXml.Add($"<node id=\"{id}\" lat=\"{coordinates.Latitude}\" lon=\"{coordinates.Longitude}\" version=\"1\" />");
                }

                wayXml.Add("<way id=\"40" + wb + "01\" version=\"5\">");
                foreach (var node in sortedOuterEdges)
                {
                    wayXml.Add($"<nd ref=\"40{wb}00{node.Item1}00{node.Item2}\" />");
                }
                wayXml.Add($"<nd ref=\"40{wb}00{sortedOuterEdges[0].Item1}00{sortedOuterEdges[0].Item2}\" />");
                wayXml.Add("</way>");

                for (int i = 0; i < sortedInnerEdges.Count; i++)
                {
                    wayXml.Add("<way id=\"40" + wb + "0" + (i + 2) + "\" version=\"5\">");
                    foreach (var node in sortedInnerEdges[i])
                    {
                        wayXml.Add($"<nd ref=\"40{wb}00{node.Item1}00{node.Item2}\" />");
                    }
                    wayXml.Add($"<nd ref=\"40{wb}00{sortedInnerEdges[i][0].Item1}00{sortedInnerEdges[i][0].Item2}\" />");
                    wayXml.Add("</way>");
                }

                // Add the multipolygon relation

                relationXml.Add("<relation id=\"40" + wb + "0\" version=\"7\">");
                relationXml.Add("<member type=\"way\" ref=\"40" + wb + "01\" role=\"outer\" />");
                for (int i = 0; i < sortedInnerEdges.Count; i++)
                {
                    relationXml.Add("<member type=\"way\" ref=\"40" + wb + "0" + (i + 2) + "\" role=\"inner\" />");
                }
                relationXml.Add("<tag k=\"natural\" v=\"water\" />");
                if (sortedInnerEdges.Count > 0)
                {
                    relationXml.Add("<tag k=\"type\" v=\"multipolygon\" />");
                }
                relationXml.Add("</relation>");
            }

            // Add coastline around the whole map

            /*m_Logger.Info("Generating coastline...");

            List<string> coastlineXml = new List<string>();

            GeoCoordinate coastlineTopLeft = GeoCoordinate.FromGameCoordinages(bounds.min.x, bounds.min.z);
            GeoCoordinate coastlineTopRight = GeoCoordinate.FromGameCoordinages(bounds.max.x, bounds.min.z);
            GeoCoordinate coastlineBottomRight = GeoCoordinate.FromGameCoordinages(bounds.max.x, bounds.max.z);
            GeoCoordinate coastlineBottomLeft = GeoCoordinate.FromGameCoordinages(bounds.min.x, bounds.max.z);

            coastlineXml.Add($"<node id=\"500000\" lat=\"{coastlineTopLeft.Latitude}\" lon=\"{coastlineTopLeft.Longitude}\" version=\"1\" />");
            coastlineXml.Add($"<node id=\"500001\" lat=\"{coastlineTopRight.Latitude}\" lon=\"{coastlineTopRight.Longitude}\" version=\"1\" />");
            coastlineXml.Add($"<node id=\"500002\" lat=\"{coastlineBottomRight.Latitude}\" lon=\"{coastlineBottomRight.Longitude}\" version=\"1\" />");
            coastlineXml.Add($"<node id=\"500003\" lat=\"{coastlineBottomLeft.Latitude}\" lon=\"{coastlineBottomLeft.Longitude}\" version=\"1\" />");

            coastlineXml.Add("<way id=\"500004\" version=\"5\">");
            coastlineXml.Add("<nd ref=\"500000\" />");
            coastlineXml.Add("<nd ref=\"500001\" />");
            coastlineXml.Add("<nd ref=\"500002\" />");
            coastlineXml.Add("<nd ref=\"500003\" />");
            coastlineXml.Add("<nd ref=\"500000\" />");
            coastlineXml.Add("<tag k=\"natural\" v=\"coastline\" />");
            coastlineXml.Add("</way>");

            terrainXml.AddRange(coastlineXml);*/
        }

        private bool IsUnderwater(WaterSurfaceData waterSurfaceData, TerrainHeightData terrainHeightData, float3 position, out float height, out float depth)
        {
            height = TerrainUtils.SampleHeight(ref terrainHeightData, position);
            depth = WaterUtils.SampleDepth(ref waterSurfaceData, position);
            return depth > 0f;
        }

        private string NameToString(NameSystem.Name name)
        {
            // Read private fields m_NameID and m_NameType
            var nameID = (string)name.GetType().GetField("m_NameID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);
            var nameType = (NameSystem.NameType)name.GetType().GetField("m_NameType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);
            var nameArgs = (string[])name.GetType().GetField("m_NameArgs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);

            var output = nameID;
            if (nameType == NameSystem.NameType.Localized)
            {
                if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(nameID, out var localized))
                {
                    output = localized;
                }
            }
            else if (nameType == NameSystem.NameType.Formatted)
            {
                for (int i = 0; i < nameArgs.Length; i++)
                {
                    if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(nameArgs[i], out var localizedArg))
                    {
                        nameArgs[i] = localizedArg;
                    }
                }
                if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(nameID, out var localized))
                {
                    for (int i = 0; i < nameArgs.Length - 1; i += 2)
                    {
                        localized = localized.Replace("{" + nameArgs[i] + "}", nameArgs[i + 1]);
                    }
                    output = localized;
                }
            }
            output = output.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
            return output;
        }

        private float3 Rotate(float3 point, quaternion q)
        {
            quaternion p = new quaternion(0, point.x, point.y, point.z);
            quaternion qi = new quaternion(q.value.x, -q.value.y, -q.value.z, -q.value.w);
            return math.mul(math.mul(q, p), qi).value.yzw;
        }
    }
}
