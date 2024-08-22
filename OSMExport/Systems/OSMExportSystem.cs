using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Colossal.Entities;
using Colossal.Logging;
using Colossal.Mathematics;
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
using OsmSharp.Streams;
using OsmSharp.Tags;

namespace OSMExport.Systems
{
    public partial class OSMExportSystem : GameSystemBase
    {
        internal static bool Activated = false, ExportPBF = false;

        public enum Direction
        {
            North,
            East,
            South,
            West
        }

        internal static string FileName = "export.osm";
        internal static Direction NorthOverride = Direction.North;
        internal static bool EnableMotorways = true;
        internal static bool EnableContours = false;
        internal static bool EnableNonstandardTransit = false;
        internal static bool EnableNonstandardTaxi = false;
        internal static bool EnableNonstandardBus = true;
        internal static bool EnableNonstandardTram = true;
        internal static bool EnableNonstandardTrain = true;
        internal static bool EnableNonstandardSubway = true;
        internal static bool EnableNonstandardShip = false;
        internal static bool EnableNonstandardAirplane = false;

        private EntityQuery m_EdgeQuery;
        private EntityQuery m_NodeQuery;
        private EntityQuery m_AreaQuery;
        private EntityQuery m_BuildingQuery;
        private EntityQuery m_TransportStopQuery;
        private EntityQuery m_TransportLineQuery;
        private EntityQuery m_TreeQuery;

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
                        ComponentType.ReadOnly<PrefabRef>(),
                    },
                Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Buildings.School>(),
                        ComponentType.ReadOnly<Game.Buildings.ParkingFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.IndustrialProperty>(),
                        ComponentType.ReadOnly<Game.Buildings.CommercialProperty>(),
                        ComponentType.ReadOnly<Game.Buildings.ResidentialProperty>(),
                        ComponentType.ReadOnly<Game.Buildings.TransportDepot>(),
                        ComponentType.ReadOnly<Game.Buildings.Hospital>(),
                        ComponentType.ReadOnly<Game.Buildings.PoliceStation>(),
                        ComponentType.ReadOnly<Game.Buildings.FireStation>(),
                        ComponentType.ReadOnly<Game.Buildings.PostFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.Prison>(),
                        ComponentType.ReadOnly<Game.Buildings.PublicTransportStation>(),
                        ComponentType.ReadOnly<Game.Buildings.CargoTransportStation>(),
                        ComponentType.ReadOnly<Game.Buildings.Park>(),
                        ComponentType.ReadOnly<Game.Buildings.GarbageFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.TelecomFacility>(),
                        ComponentType.ReadOnly<Game.Buildings.Transformer>(),
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
            m_TransportStopQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Routes.TransportStop>(),
                        ComponentType.ReadOnly<Game.Objects.Transform>(),
                        ComponentType.ReadOnly<PrefabRef>(),
                    },
                Any = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Routes.BusStop>(),
                        ComponentType.ReadOnly<Game.Routes.TramStop>(),
                        ComponentType.ReadOnly<Game.Routes.TaxiStand>(),
                        ComponentType.ReadOnly<Game.Routes.SubwayStop>(),
                        ComponentType.ReadOnly<Game.Routes.TrainStop>(),
                        ComponentType.ReadOnly<Game.Routes.ShipStop>(),
                        ComponentType.ReadOnly<Game.Routes.AirplaneStop>(),
                    },
                None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Deleted>(),
                        ComponentType.ReadOnly<Temp>(),
                        ComponentType.ReadOnly<Hidden>(),
                    }
            }
            );
            m_TransportLineQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Routes.TransportLine>(),
                        ComponentType.ReadOnly<PrefabRef>(),
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
            m_TreeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Game.Objects.Tree>(),
                        ComponentType.ReadOnly<Game.Objects.Transform>(),
                        ComponentType.ReadOnly<PrefabRef>(),
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
                switch (NorthOverride)
                {
                    case Direction.East:
                        return new GeoCoordinate(x / 111_000, -z / 111_000);
                    case Direction.North:
                        return new GeoCoordinate(z / 111_000, x / 111_000);
                    case Direction.West:
                        return new GeoCoordinate(-x / 111_000, z / 111_000);
                    case Direction.South:
                        return new GeoCoordinate(-z / 111_000, -x / 111_000);
                    default:
                        return new GeoCoordinate(z / 111_000, x / 111_000);
                }
                
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
            Service,
            Pedestrian,
            Road,
            NotHighway,
        }

        class Highway
        {
            public int Id { get; set; }
            public List<long> Nodes { get; set; }
            public TagsCollection Tags { get; set; }
            public HighwayType Type { get; set; }
            public bool IsLink { get; set; }
            public bool MightBeLink { get; set; }
            public List<int> NodeIds { get; set; }

            public Highway(int id)
            {
                Id = id;
                Nodes = new List<long>();
                Tags = new TagsCollection();
                Type = HighwayType.NotHighway;
                IsLink = false;
                MightBeLink = false;
                NodeIds = new List<int>();
            }

            private TagsCollection GetTags()
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
                    case HighwayType.Service:
                        highwayType = "service";
                        break;
                    case HighwayType.Pedestrian:
                        highwayType = "pedestrian";
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
                var returnTags = new TagsCollection(Tags);
                returnTags.AddOrReplace(new Tag() { Key = "highway", Value = highwayType });
                return returnTags;
            }

            public void ToXml(List<OsmSharp.Way> wayXml)
            {
                var id = CreateID(WAY, Id);
                wayXml.Add(NewWay(id, Nodes, GetTags()));
            }
        }

        private static OsmSharp.Node NewNode(long id, GeoCoordinate coords, TagsCollection tags)
        {
            return new OsmSharp.Node()
            {
                Id = id,
                Latitude = coords.Latitude,
                Longitude = coords.Longitude,
                Tags = tags,
                TimeStamp = System.DateTime.Now,
                Version = 1,
            };
        }

        private static OsmSharp.Way NewWay(long id, IEnumerable<long> nodes, TagsCollection tags)
        {
            return new OsmSharp.Way()
            {
                Id = id,
                Nodes = nodes.ToArray(),
                Tags = tags,
                TimeStamp = System.DateTime.Now,
                Version = 1,
            };
        }

        private static OsmSharp.Relation NewRelation(long id, IEnumerable<OsmSharp.RelationMember> members, TagsCollection tags)
        {
            return new OsmSharp.Relation()
            {
                Id = id,
                Members = members.ToArray(),
                Tags = tags,
                TimeStamp = System.DateTime.Now,
                Version = 1,
            };
        }

        private static OsmSharp.RelationMember NewMember(long item, OsmSharp.OsmGeoType type, string role)
        {
            return new OsmSharp.RelationMember()
            {
                Id = item,
                Type = type,
                Role = role,
            };
        }

        private static OsmSharp.RelationMember NewMember(OsmSharp.OsmGeo item, string role)
        {
            return new OsmSharp.RelationMember()
            {
                Id = item.Id ?? 0,
                Type = item.Type,
                Role = role,
            };
        }

        protected override void OnUpdate()
        {
            if (!Activated) return;
            Activated = false;

            IDs = new Dictionary<string, int>();
            IdCounter = 10000;

            var directory = Path.Combine(
                    EnvPath.kUserDataPath,
                    "ModsData",
                    "OSMExport");
            var dir = new DirectoryInfo(directory);

            if (!dir.Exists)
            {
                dir.Create();
            }

            m_Logger.Info("Exporting OSM...");

            var oldCulture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

            List<OsmSharp.Node> nodeXml = new List<OsmSharp.Node>();
            List<OsmSharp.Way> wayXml = new List<OsmSharp.Way>();
            List<OsmSharp.Relation> relationXml = new List<OsmSharp.Relation>();

            AddHighways(nodeXml, wayXml, relationXml);
            AddAreas(nodeXml, wayXml, relationXml);
            AddBuildinds(nodeXml, wayXml, relationXml);
            AddTransportStops(nodeXml, wayXml, relationXml);
            //AddTrees(nodeXml, wayXml, relationXml);
            AddWaterBodies(nodeXml, wayXml, relationXml);
            if (EnableContours)
            {
                AddContourLines(nodeXml, wayXml, relationXml);
            }

            m_Logger.Info("Generating complete xml...");

            Bounds bounds = m_TerrainSystem.GetTerrainBounds();

            GeoCoordinate minBounds = GeoCoordinate.FromGameCoordinages(bounds.min.x, bounds.min.z);
            GeoCoordinate maxBounds = GeoCoordinate.FromGameCoordinages(bounds.max.x, bounds.max.z);

            var osmSharpBounds = new OsmSharp.API.Bounds()
            {
                MinLatitude = (float)minBounds.Latitude,
                MaxLatitude = (float)maxBounds.Latitude,
                MinLongitude = (float)minBounds.Longitude,
                MaxLongitude = (float)maxBounds.Longitude,
            };

            m_Logger.Info("Saving to disk...");

            /*if (ExportPBF)
            {
                if (FileName.EndsWith(".osm"))
                {
                    FileName = FileName + ".pbf";
                }
                else if (!FileName.EndsWith(".pbf"))
                {
                    FileName = FileName + ".osm.pbf";
                }
                using (var fileStream = new FileStream(Path.Combine(directory, FileName), FileMode.Create))
                {
                    var target = new PBFOsmStreamTarget(fileStream, compress: false);
                    target.Initialize();
                    nodeXml.ForEach(target.AddNode);
                    wayXml.ForEach(target.AddWay);
                    relationXml.ForEach(target.AddRelation);
                    target.Flush();
                    target.Close();
                    fileStream.Flush();
                }
            }
            else*/
            {
                if (FileName.EndsWith(".osm.pbf"))
                {
                    FileName = FileName.Substring(0, FileName.Length - ".pbf".Length);
                }
                else if (!FileName.EndsWith(".osm")) {
                    FileName = FileName + ".osm";
                }
                using (var fileStream = new FileStream(Path.Combine(directory, FileName), FileMode.Create))
                {
                    var target = new XmlOsmStreamTarget(fileStream);
                    target.Initialize();
                    target.Bounds = osmSharpBounds;
                    nodeXml.ForEach(target.AddNode);
                    wayXml.ForEach(target.AddWay);
                    relationXml.ForEach(target.AddRelation);
                    target.Flush();
                    target.Close();
                }
            }

            CultureInfo.CurrentCulture = oldCulture;
        }

        private void AddHighways(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
        {

            NativeArray<Entity> nodeEntities = m_NodeQuery.ToEntityArray(Allocator.Temp);
            NativeArray<Entity> edgeEntities = m_EdgeQuery.ToEntityArray(Allocator.Temp);

            int bezierCounter = 0;

            m_Logger.Info("Generating net node xml...");

            for (int i = 0; i < nodeEntities.Length; i++)
            {
                var entity = nodeEntities[i];
                var position = EntityManager.GetComponentData<Game.Net.Node>(entity).m_Position;
                var coordinates = GeoCoordinate.FromGameCoordinages(position.x, position.z);
                var id = CreateID(NODE, entity.Index);

                var tags = new TagsCollection();

                if (EntityManager.HasComponent<Game.Net.Roundabout>(entity))
                {
                    tags.AddOrReplace("highway", "mini_roundabout");
                }

                nodeXml.Add(NewNode(id, coordinates, tags));
            }

            m_Logger.Info("Generating net ways...");

            List<Highway> ways = new List<Highway>();
            Dictionary<int, List<Highway>> nodeToHighways = new Dictionary<int, List<Highway>>();
            Dictionary<int, List<Highway>> aggregateToHighways = new Dictionary<int, List<Highway>>();

            for (int i = 0; i < edgeEntities.Length; i++)
            {
                var entity = edgeEntities[i];
                var edge = EntityManager.GetComponentData<Game.Net.Edge>(entity);
                var isAggregated = EntityManager.TryGetComponent<Game.Net.Aggregated>(entity, out var aggregated);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var subLanes = EntityManager.GetBuffer<Game.Net.SubLane>(entity, true);
                var hasOwner = EntityManager.HasComponent<Owner>(entity);
                var isElevated = EntityManager.TryGetComponent<Game.Net.Elevation>(entity, out var elevation);
                var aggregateLen = !isAggregated ? 0 : (EntityManager.TryGetBuffer<Game.Net.AggregateElement>(aggregated.m_Aggregate, true, out var aggregateElements) ? aggregateElements.Length : 0);

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
                            lanes.Add(lane.m_StartNode.GetLaneIndex() & 255);
                        }
                    }
                }

                int numLanes = lanes.Count;

                if (EntityManager.TryGetComponent<RoadData>(prefabRef, out var roadData))
                {
                    if (!isTwoWay && aggregateLen < 10)
                    {
                        way.MightBeLink = true;
                    }

                    int speedLimit = 0;
                    if (roadData.m_SpeedLimit > 40)
                    {
                        if (!isTwoWay && aggregateLen > 4)
                        {
                            way.Type = EnableMotorways ? HighwayType.Motorway : HighwayType.Trunk;
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
                        if (!isTwoWay && aggregateLen > 4)
                        {
                            way.Type = HighwayType.Trunk;
                        }
                        else
                        {
                            way.Type = HighwayType.Primary;
                        }
                        speedLimit = 60;
                    }
                    else if (roadData.m_SpeedLimit > 25)
                    {
                        if (!isTwoWay && aggregateLen > 4)
                        {
                            way.Type = HighwayType.Primary;
                        }
                        else
                        {
                            way.Type = HighwayType.Secondary;
                        }
                        speedLimit = 50;
                    }
                    else if (roadData.m_SpeedLimit > 20)
                    {

                        if (!isTwoWay && aggregateLen > 4)
                        {
                            way.Type = HighwayType.Secondary;
                        }
                        else
                        {
                            way.Type = HighwayType.Residential;
                        }
                        speedLimit = 40;
                    }
                    else if (roadData.m_SpeedLimit > 15)
                    {
                        way.Type = HighwayType.Service;
                        speedLimit = 30;
                    }
                    else if (roadData.m_SpeedLimit > 10)
                    {
                        way.Type = HighwayType.Pedestrian;
                        speedLimit = 30;
                    }
                    else
                    {
                        way.Type = HighwayType.Road;
                    }

                    way.Tags.AddOrReplace("maxspeed", speedLimit.ToString());

                    if (numLanes >= 1)
                    {
                        way.Tags.AddOrReplace("lanes", numLanes.ToString());
                        //way.Tags += $"<!-- {string.Join(";", lanes.ToList().Select(s => s.ToString()))} -->";
                    }

                    if (!isTwoWay)
                    {
                        way.Tags.AddOrReplace("oneway", "yes");
                    }

                    if (isAggregated)
                    {
                        var name = m_NameSystem.GetName(aggregated.m_Aggregate);
                        way.Tags.AddOrReplace("name", NameToString(name));
                    }
                }
                else if (EntityManager.TryGetComponent<PathwayData>(prefabRef, out var pathwayData))
                {

                    if (hasOwner)
                    {
                        continue;
                    }

                    way.Tags.AddOrReplace("highway", "footway");
                }
                else if (EntityManager.HasComponent<Game.Net.TramTrack>(entity))
                {
                    way.Tags.AddOrReplace("railway", "tram");
                }
                else if (EntityManager.HasComponent<Game.Net.SubwayTrack>(entity))
                {
                    way.Tags.AddOrReplace("railway", "subway");
                }
                else if (EntityManager.TryGetComponent<TrackData>(prefabRef, out var trackData))
                {
                    way.Tags.AddOrReplace("railway", "rail");
                    way.Tags.AddOrReplace("usage", "main");
                }
                else if (EntityManager.HasComponent<Game.Net.ElectricityConnection>(entity))
                {
                    way.Tags.AddOrReplace("power", "line");
                }
                else if (EntityManager.HasComponent<Game.Net.Taxiway>(entity))
                {
                    // Elevated runways are in the air, we don't want them
                    if (isElevated)
                    {
                        continue;
                    }
                    if (EntityManager.TryGetComponent<TaxiwayData>(prefabRef, out var taxiwayData) && (taxiwayData.m_Flags & TaxiwayFlags.Runway) != 0)
                    {
                        way.Tags.AddOrReplace("aeroway", "runway");
                    }
                    else
                    {
                        way.Tags.AddOrReplace("aeroway", "taxiway");
                    }
                }
                else if (EntityManager.HasComponent<Game.Net.Waterway>(entity))
                {
                    // TODO: Are the ships really ferries?
                    // Should this be e.g. separation_line?
                    way.Tags.AddOrReplace("seamark:type", "ferry_route");
                }
                else
                {
                    continue;
                }

                if (isElevated)
                {
                    if (elevation.m_Elevation.x > 0.1f)
                    {
                        way.Tags.AddOrReplace("bridge", "yes");
                        way.Tags.AddOrReplace("layer", "1");
                    }
                    else if (elevation.m_Elevation.x < -0.1f)
                    {
                        way.Tags.AddOrReplace("tunnel", "yes");
                        way.Tags.AddOrReplace("layer", "-1");
                    }
                }

                way.Nodes.Add(CreateID(NODE, edge.m_Start.Index));

                if (EntityManager.TryGetComponent<Game.Net.Curve>(entity, out var curve))
                {
                    for (int j = 1; j <= 2; j++)
                    {
                        float f = 0.3f * j;
                        var pos = MathUtils.Position(curve.m_Bezier, f);
                        var coordinates = GeoCoordinate.FromGameCoordinages(pos.x, pos.z);
                        var id = CreateID(BEZIER_NODE, bezierCounter++);

                        nodeXml.Add(NewNode(id, coordinates, new TagsCollection()));
                        way.Nodes.Add(id);
                    }
                }

                way.Nodes.Add(CreateID(NODE, edge.m_End.Index));

                if (!nodeToHighways.ContainsKey(edge.m_Start.Index)) nodeToHighways[edge.m_Start.Index] = new List<Highway>();
                nodeToHighways[edge.m_Start.Index].Add(way);
                if (!nodeToHighways.ContainsKey(edge.m_End.Index)) nodeToHighways[edge.m_End.Index] = new List<Highway>();
                nodeToHighways[edge.m_End.Index].Add(way);
                if (isAggregated)
                {
                    if (!aggregateToHighways.ContainsKey(aggregated.m_Aggregate.Index)) aggregateToHighways[aggregated.m_Aggregate.Index] = new List<Highway>();
                    aggregateToHighways[aggregated.m_Aggregate.Index].Add(way);
                }

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

        private void AddAreas(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
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
                    var id = CreateID(AREA, entity.Index, 0);
                    var name = m_NameSystem.GetName(entity);

                    nodeXml.Add(NewNode(id, coordinates, new TagsCollection(new Tag("place", "suburb"), new Tag("name", NameToString(name)))));
                }
                else if (EntityManager.HasComponent<Game.Areas.Lot>(entity))
                {
                    var tags = new TagsCollection();
                    tags.AddOrReplace("landuse", "farmland");
                    if (EntityManager.TryGetComponent<PrefabRef>(entity, out var prefabRef))
                    {
                        if (EntityManager.TryGetComponent<ExtractorAreaData>(prefabRef.m_Prefab, out var extractorAreaData))
                        {
                            switch (extractorAreaData.m_MapFeature)
                            {
                                case Game.Areas.MapFeature.FertileLand:
                                    tags.AddOrReplace("landuse", "farmland");
                                    break;
                                case Game.Areas.MapFeature.Forest:
                                    tags.AddOrReplace("landuse", "forest");
                                    break;
                                case Game.Areas.MapFeature.Oil:
                                    tags.AddOrReplace("landuse", "industrial");
                                    tags.AddOrReplace("industrial", "oil");
                                    break;
                                case Game.Areas.MapFeature.Ore:
                                    tags.AddOrReplace("landuse", "quarry");
                                    break;
                            }
                        }
                        else if (EntityManager.TryGetComponent<StorageAreaData>(prefabRef.m_Prefab, out var storageAreaData))
                        {
                            switch (storageAreaData.m_Resources)
                            {
                                case Game.Economy.Resource.Garbage:
                                    tags.AddOrReplace("landuse", "landfill");
                                    break;
                            }
                        }
                    }

                    for (int j = 0; j < nodes.Length; j++)
                    {
                        var node = nodes[j];
                        var position = node.m_Position;
                        var coordinates = GeoCoordinate.FromGameCoordinages(position.x, position.z);
                        var id = CreateID(AREA, entity.Index, 1, j);

                        nodeXml.Add(NewNode(id, coordinates, new TagsCollection()));
                    }

                    if (nodes.Length < 3) continue;

                    // Add the way

                    var wayId = CreateID(AREA, entity.Index, 1);

                    List<long> nodeIds = new List<long>();
                    for (int j = 0; j < nodes.Length; j++)
                    {
                        var id = CreateID(AREA, entity.Index, 1, j);
                        nodeIds.Add(id);
                    }
                    nodeIds.Add(CreateID(AREA, entity.Index, 1, 0));
                    wayXml.Add(NewWay(wayId, nodeIds, tags));
                }
            }

            areaEntities.Dispose();
        }

        private void AddBuildinds(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
        {
            m_Logger.Info("Generating buildings...");

            NativeArray<Entity> buildingEntities = m_BuildingQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < buildingEntities.Length; i++)
            {
                var entity = buildingEntities[i];
                var id = CreateID(BUILDING, entity.Index);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);
                var coordinates = GeoCoordinate.FromGameCoordinages(transform.m_Position.x, transform.m_Position.z);

                Entity companyEntity = Entity.Null;
                bool showName = true;

                var tags = new TagsCollection();
                if (EntityManager.HasComponent<Game.Buildings.School>(entity))
                {
                    // TODO: different types of schools
                    tags.AddOrReplace("amenity", "school");
                }
                else if (EntityManager.HasComponent<Game.Buildings.ParkingFacility>(entity))
                {
                    tags.AddOrReplace("amenity", "parking");
                }
                else if (EntityManager.HasComponent<Game.Buildings.IndustrialProperty>(entity))
                {
                    bool isOffice = false;
                    if (EntityManager.TryGetBuffer<Game.Buildings.Renter>(entity, true, out var renters) && renters.Length > 0)
                    {
                        foreach (var renter in renters)
                        {
                            if (EntityManager.TryGetComponent<PrefabRef>(renter, out var renterPrefabRef))
                            {
                                if (EntityManager.TryGetComponent<IndustrialProcessData>(renterPrefabRef.m_Prefab, out var industrialProcessData))
                                {
                                    companyEntity = renter.m_Renter;
                                    switch (industrialProcessData.m_Output.m_Resource)
                                    {
                                        case Game.Economy.Resource.Financial:
                                            isOffice = true;
                                            tags.AddOrReplace("office", "financial");
                                            break;
                                        case Game.Economy.Resource.Media:
                                            isOffice = true;
                                            // This is not that accurate but no better tag exists to my knowlege
                                            tags.AddOrReplace("office", "newspaper");
                                            break;
                                        case Game.Economy.Resource.Software:
                                            isOffice = true;
                                            tags.AddOrReplace("office", "it");
                                            break;
                                        case Game.Economy.Resource.Telecom:
                                            isOffice = true;
                                            tags.AddOrReplace("office", "telecommunication");
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    if (!isOffice)
                    {
                        if (EntityManager.HasComponent<WarehouseData>(prefabRef))
                        {
                            tags.AddOrReplace("landuse", "industrial");
                            tags.AddOrReplace("industrial", "warehouse");
                        }
                        else
                        {
                            tags.AddOrReplace("landuse", "industrial");
                            tags.AddOrReplace("industrial", "factory");
                        }
                    }
                }
                else if (EntityManager.HasComponent<Game.Buildings.CommercialProperty>(entity))
                {
                    tags.AddOrReplace("shop", "yes");
                    if (EntityManager.TryGetBuffer<Game.Buildings.Renter>(entity, true, out var renters) && renters.Length > 0)
                    {
                        foreach (var renter in renters)
                        {
                            if (EntityManager.TryGetComponent<PrefabRef>(renter, out var renterPrefabRef))
                            {
                                if (EntityManager.TryGetComponent<IndustrialProcessData>(renterPrefabRef.m_Prefab, out var industrialProcessData))
                                {
                                    companyEntity = renter.m_Renter;
                                    switch (industrialProcessData.m_Output.m_Resource)
                                    {
                                        case Game.Economy.Resource.Beverages:
                                            tags.AddOrReplace("shop", "alcohol");
                                            break;
                                        case Game.Economy.Resource.Chemicals:
                                            tags.AddOrReplace("shop", "chemist");
                                            break;
                                        case Game.Economy.Resource.ConvenienceFood:
                                            tags.AddOrReplace("shop", "convenience");
                                            break;
                                        case Game.Economy.Resource.Food:
                                            tags.AddOrReplace("shop", "food");
                                            break;
                                        case Game.Economy.Resource.Furniture:
                                            tags.AddOrReplace("shop", "furniture");
                                            break;
                                        case Game.Economy.Resource.Electronics:
                                            tags.AddOrReplace("shop", "electronics");
                                            break;
                                        case Game.Economy.Resource.Paper:
                                            tags.AddOrReplace("shop", "books");
                                            break;
                                        case Game.Economy.Resource.Petrochemicals:
                                            tags.AddOrReplace("amenity", "fuel");
                                            break;
                                        case Game.Economy.Resource.Pharmaceuticals:
                                            tags.AddOrReplace("amenity", "pharmacy");
                                            break;
                                        case Game.Economy.Resource.Plastics:
                                            tags.AddOrReplace("shop", "gift");
                                            break;
                                        case Game.Economy.Resource.Textiles:
                                            tags.AddOrReplace("shop", "clothes");
                                            break;
                                        case Game.Economy.Resource.Vehicles:
                                            tags.AddOrReplace("shop", "car");
                                            break;
                                        case Game.Economy.Resource.Meals:
                                            tags.AddOrReplace("amenity", "restaurant");
                                            break;
                                        case Game.Economy.Resource.Entertainment:
                                            tags.AddOrReplace("amenity", "bar");
                                            break;
                                        case Game.Economy.Resource.Recreation: // TODO this is not really accurate
                                            tags.AddOrReplace("amenity", "arts_centre");
                                            break;
                                        case Game.Economy.Resource.Lodging:
                                            tags.AddOrReplace("tourism", "hotel");
                                            break;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    tags.AddOrReplace("landuse", "commercial");
                }
                else if (EntityManager.HasComponent<Game.Buildings.ResidentialProperty>(entity))
                {
                    tags.AddOrReplace("landuse", "residential");
                    showName = false;
                }
                else if (EntityManager.HasComponent<Game.Buildings.TransportDepot>(entity))
                {
                    bool isTrainDepot = false;
                    if (EntityManager.TryGetBuffer<Game.Vehicles.OwnedVehicle>(entity, true, out var ownedVehicles))
                    {
                        foreach (var v in ownedVehicles)
                        {
                            if (EntityManager.HasComponent<Game.Vehicles.Train>(v.m_Vehicle))
                            {
                                isTrainDepot = true;
                                break;
                            }
                        }
                    }
                    if (isTrainDepot)
                    {
                        tags.AddOrReplace("landuse", "railway");
                        tags.AddOrReplace("railway", "depot");
                    }
                    else
                    {
                        tags.AddOrReplace("landuse", "industrial");
                        tags.AddOrReplace("industrial", "depot");
                    }
                }
                else if (EntityManager.HasComponent<Game.Buildings.Hospital>(entity))
                {
                    tags.AddOrReplace("amenity", "hospital");
                }
                else if (EntityManager.HasComponent<Game.Buildings.PoliceStation>(entity))
                {
                    tags.AddOrReplace("amenity", "police");
                }
                else if (EntityManager.HasComponent<Game.Buildings.FireStation>(entity))
                {
                    tags.AddOrReplace("amenity", "fire_station");
                }
                else if (EntityManager.HasComponent<Game.Buildings.PostFacility>(entity))
                {
                    tags.AddOrReplace("amenity", "post_office");
                }
                else if (EntityManager.HasComponent<Game.Buildings.Prison>(entity))
                {
                    tags.AddOrReplace("amenity", "prison");
                }
                else if (EntityManager.HasComponent<Game.Buildings.PublicTransportStation>(entity))
                {
                    bool isHarbour = false;
                    bool isSubwayStation = false;
                    bool isTrainStation = false;
                    bool isAirport = false;
                    bool isBusStation = false;
                    if (EntityManager.TryGetBuffer<Game.Objects.SubObject>(entity, true, out var subObjects))
                    {
                        foreach (var v in subObjects)
                        {
                            if (EntityManager.HasComponent<Game.Routes.ShipStop>(v.m_SubObject))
                            {
                                isHarbour = true;
                            }
                            if (EntityManager.HasComponent<Game.Routes.SubwayStop>(v.m_SubObject))
                            {
                                isHarbour = true;
                            }
                            if (EntityManager.HasComponent<Game.Routes.TrainStop>(v.m_SubObject))
                            {
                                isTrainStation = true;
                            }
                            if (EntityManager.HasComponent<Game.Routes.AirplaneStop>(v.m_SubObject))
                            {
                                isAirport = true;
                            }
                            if (EntityManager.HasComponent<Game.Routes.BusStop>(v.m_SubObject))
                            {
                                isBusStation = true;
                            }
                        }
                    }
                    if (isAirport)
                    {
                        tags.AddOrReplace("aeroway", "aerodrome");
                    }
                    else if (isTrainStation || isSubwayStation)
                    {
                        tags.AddOrReplace("public_transport", "station");
                        tags.AddOrReplace("landuse", "railway");
                        tags.AddOrReplace("railway", "station");
                    }
                    else if (isBusStation)
                    {
                        tags.AddOrReplace("public_transport", "station");
                        tags.AddOrReplace("amenity", "bus_station");
                    }
                    else
                    {
                        tags.AddOrReplace("public_transport", "station");
                    }
                }
                else if (EntityManager.HasComponent<Game.Buildings.CargoTransportStation>(entity))
                {
                    bool isCargoHarbour = false;
                    bool isTrainTerminal = false;
                    if (EntityManager.TryGetBuffer<Game.Objects.SubObject>(entity, true, out var subObjects))
                    {
                        foreach (var v in subObjects)
                        {
                            if (EntityManager.HasComponent<Game.Routes.ShipStop>(v.m_SubObject))
                            {
                                isCargoHarbour = true;
                            }
                            if (EntityManager.HasComponent<Game.Routes.TrainStop>(v.m_SubObject))
                            {
                                isTrainTerminal = true;
                            }
                        }
                    }
                    if (isCargoHarbour)
                    {
                        tags.AddOrReplace("landuse", "industrial");
                        tags.AddOrReplace("industrial", "port");
                        tags.AddOrReplace("port", "cargo");
                    }
                    else if (isTrainTerminal)
                    {
                        tags.AddOrReplace("landuse", "railway");
                        tags.AddOrReplace("railway", "station");
                    }
                }
                else if (EntityManager.HasComponent<Game.Buildings.Park>(entity))
                {
                    // TODO: recognize different types of parks
                    tags.AddOrReplace("leisure", "park");
                }
                else if (EntityManager.HasComponent<Game.Buildings.GarbageFacility>(entity))
                {
                    // TODO: different types of garbage facilities
                    tags.AddOrReplace("landuse", "industrial");
                }
                else if (EntityManager.HasComponent<Game.Buildings.TelecomFacility>(entity))
                {
                    // TODO: different types of garbage facilities
                    tags.AddOrReplace("landuse", "industrial");
                }
                else if (EntityManager.HasComponent<Game.Buildings.Transformer>(entity))
                {
                    tags.AddOrReplace("power", "substation");
                }
                else if (EntityManager.HasComponent<Game.Buildings.ElectricityProducer>(entity))
                {
                    tags.AddOrReplace("landuse", "industrial");
                    tags.AddOrReplace("power", "plant");
                }
                else
                {
                    continue;
                }

                if (showName)
                {
                    var name = m_NameSystem.GetName(companyEntity != Entity.Null ? companyEntity : entity);
                    tags.AddOrReplace("name", NameToString(name));
                }

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

                    var id1 = CreateID(BUILDING, entity.Index, 0, 1);
                    var id2 = CreateID(BUILDING, entity.Index, 0, 2);
                    var id3 = CreateID(BUILDING, entity.Index, 0, 3);
                    var id4 = CreateID(BUILDING, entity.Index, 0, 4);

                    nodeXml.Add(NewNode(id1, corner1, new TagsCollection()));
                    nodeXml.Add(NewNode(id2, corner2, new TagsCollection()));
                    nodeXml.Add(NewNode(id3, corner3, new TagsCollection()));
                    nodeXml.Add(NewNode(id4, corner4, new TagsCollection()));

                    // Add the way

                    var wayId = CreateID(BUILDING, entity.Index, 1);

                    wayXml.Add(NewWay(wayId, new long[] { id1, id2, id3, id4, id1 }, tags));
                }
                else
                {
                    nodeXml.Add(NewNode(id, coordinates, tags));
                }
            }

            buildingEntities.Dispose();
        }
        private void AddTransportStops(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
        {
            m_Logger.Info("Generating transport stops...");

            NativeArray<Entity> stopEntities = m_TransportStopQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < stopEntities.Length; i++)
            {
                var entity = stopEntities[i];
                var id = CreateID(TRANSPORT_STOP, entity.Index);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var isOwned = EntityManager.HasComponent<Owner>(entity);
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);
                var coordinates = GeoCoordinate.FromGameCoordinages(transform.m_Position.x, transform.m_Position.z);

                if (EntityManager.TryGetComponent<TransportStopData>(prefabRef.m_Prefab, out var transportStopData))
                {
                    // Check if we have a cargo stop
                    if (!transportStopData.m_PassengerTransport)
                    {
                        continue;
                    }
                }

                var tags = new TagsCollection();
                tags.AddOrReplace("public_transport", "platform");
                if (EntityManager.HasComponent<Game.Routes.BusStop>(entity))
                {
                    tags.AddOrReplace("highway", isOwned ? "platform" : "bus_stop");
                }
                else if (EntityManager.HasComponent<Game.Routes.TaxiStand>(entity))
                {
                    tags.AddOrReplace("amenity", "taxi");
                }
                else if (EntityManager.HasComponent<Game.Routes.TramStop>(entity) || EntityManager.HasComponent<Game.Routes.TrainStop>(entity) || EntityManager.HasComponent<Game.Routes.SubwayStop>(entity))
                {
                    tags.AddOrReplace("railway", "platform");
                }
                else
                {
                    continue;
                }

                var name = m_NameSystem.GetName(entity);
                tags.AddOrReplace("name", NameToString(name));

                nodeXml.Add(NewNode(id, coordinates, tags));

                // Non-standard stop node
                if (EnableNonstandardTransit)
                {
                    var nsId = CreateID(TRANSPORT_STOP, entity.Index, 1);
                    var nsTags = new TagsCollection();
                    var isNonstandardEnabledForThisType = true;
                    if (EntityManager.HasComponent<Game.Routes.BusStop>(entity))
                    {
                        nsTags.AddOrReplace("osm_export_stop", "bus");
                        isNonstandardEnabledForThisType = EnableNonstandardBus;
                    }
                    else if (EntityManager.HasComponent<Game.Routes.TaxiStand>(entity))
                    {
                        nsTags.AddOrReplace("osm_export_stop", "taxi");
                        isNonstandardEnabledForThisType = EnableNonstandardTaxi;
                    }
                    else if (EntityManager.HasComponent<Game.Routes.TramStop>(entity))
                    {
                        nsTags.AddOrReplace("osm_export_stop", "tram");
                        isNonstandardEnabledForThisType = EnableNonstandardTram;
                    }
                    else if (EntityManager.HasComponent<Game.Routes.TrainStop>(entity))
                    {
                        nsTags.AddOrReplace("osm_export_stop", "train");
                        isNonstandardEnabledForThisType = EnableNonstandardTrain;
                    }
                    else if (EntityManager.HasComponent<Game.Routes.SubwayStop>(entity))
                    {
                        nsTags.AddOrReplace("osm_export_stop", "subway");
                        isNonstandardEnabledForThisType = EnableNonstandardSubway;
                    }
                    if (isNonstandardEnabledForThisType)
                    {
                        nodeXml.Add(NewNode(nsId, coordinates, nsTags));
                    }
                }
            }

            stopEntities.Dispose();

            m_Logger.Info("Generating transport lines...");

            NativeArray<Entity> lineEntities = m_TransportLineQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < lineEntities.Length; i++)
            {
                var entity = lineEntities[i];
                var id = CreateID(TRANSPORT_LINE, entity.Index);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);

                string type = "";
                var isNonstandardEnabledForThisType = true;
                if (EntityManager.TryGetComponent<TransportLineData>(prefabRef.m_Prefab, out var transportLineData))
                {
                    // Check if we have a cargo line (TODO should these be supported?)
                    if (!transportLineData.m_PassengerTransport)
                    {
                        continue;
                    }

                    if (transportLineData.m_TransportType == TransportType.Bus)
                    {
                        type = "bus";
                        isNonstandardEnabledForThisType = EnableNonstandardBus;
                    }
                    else if (transportLineData.m_TransportType == TransportType.Tram)
                    {
                        type = "tram";
                        isNonstandardEnabledForThisType = EnableNonstandardTram;
                    }
                    else if (transportLineData.m_TransportType == TransportType.Train)
                    {
                        type = "train";
                        isNonstandardEnabledForThisType = EnableNonstandardTrain;
                    }
                    else if (transportLineData.m_TransportType == TransportType.Subway)
                    {
                        type = "subway";
                        isNonstandardEnabledForThisType = EnableNonstandardSubway;
                    }
                    else if (transportLineData.m_TransportType == TransportType.Ship)
                    {
                        // TODO: Are the ships really ferries?
                        type = "ferry";
                        isNonstandardEnabledForThisType = EnableNonstandardShip;
                    }
                    else if (transportLineData.m_TransportType == TransportType.Airplane)
                    {
                        type = "subway";
                        isNonstandardEnabledForThisType = EnableNonstandardAirplane;
                    }
                }

                string routeNum = "";
                if (EntityManager.TryGetComponent<Game.Routes.RouteNumber>(entity, out var routeNumber))
                {
                    routeNum = routeNumber.m_Number.ToString();
                }

                // Color
                string colorCode = "#000000";
                if (EntityManager.TryGetComponent<Game.Routes.Color>(entity, out var color))
                {
                    colorCode = "#" + ColorUtility.ToHtmlStringRGB(color.m_Color);
                }

                var name = m_NameSystem.GetName(entity);

                // Make the actual OSM relation
                var members = new List<OsmSharp.RelationMember>();

                var hasRouteWaypoints = EntityManager.TryGetBuffer<Game.Routes.RouteWaypoint>(entity, true, out var routeWaypoints);

                if (hasRouteWaypoints)
                {
                    foreach (var waypoint in routeWaypoints)
                    {
                        if (EntityManager.TryGetComponent<Game.Routes.Connected>(waypoint.m_Waypoint, out var connected))
                        {
                            members.Add(NewMember(CreateID(TRANSPORT_STOP, connected.m_Connected.Index), OsmSharp.OsmGeoType.Node, "platform"));
                        }
                    }
                }

                var hasRouteSegments = EntityManager.TryGetBuffer<Game.Routes.RouteSegment>(entity, true, out var routeSegments);

                if (hasRouteSegments)
                {
                    foreach (var segment in routeSegments)
                    {
                        if (EntityManager.TryGetBuffer<Game.Pathfind.PathElement>(segment.m_Segment, true, out var pathElements))
                        {
                            foreach (var pathElement in pathElements)
                            {
                                if (EntityManager.TryGetComponent<Owner>(pathElement.m_Target, out var owner))
                                {
                                    members.Add(NewMember(CreateID(WAY, owner.m_Owner.Index), OsmSharp.OsmGeoType.Way, null));
                                }
                            }
                        }
                    }
                }

                if (members.Count > 0)
                {
                    var tags = new TagsCollection();
                    tags.AddOrReplace("type", "route");
                    tags.AddOrReplace("route", type);
                    tags.AddOrReplace("ref", routeNum);
                    tags.AddOrReplace("name", NameToString(name));
                    tags.AddOrReplace("roundtrip", "yes");
                    relationXml.Add(NewRelation(CreateID(TRANSPORT_LINE, entity.Index, 0), members, tags));
                }

                // Make a non-standard way for easily displaying the route in Maperitive
                if (EnableNonstandardTransit && isNonstandardEnabledForThisType && hasRouteSegments)
                {
                    int seg = 0;
                    foreach (var segment in routeSegments)
                    {
                        if (EntityManager.TryGetBuffer<Game.Routes.CurveElement>(segment.m_Segment, true, out var curveElements))
                        {
                            if (curveElements.Length > 0)
                            {
                                var curvePoints = new List<(float, float)>
                                {
                                    (curveElements[0].m_Curve.a.x, curveElements[0].m_Curve.a.z)
                                };
                                foreach (var curveElement in curveElements)
                                {
                                    curvePoints.Add((curveElement.m_Curve.d.x, curveElement.m_Curve.d.z));
                                }

                                var nodeRefs = new List<long>();
                                for (int j = 0; j < curvePoints.Count; j++)
                                {
                                    var coordinates = GeoCoordinate.FromGameCoordinages(curvePoints[j].Item1, curvePoints[j].Item2);
                                    var curvePointId = CreateID(TRANSPORT_LINE, entity.Index, 1, seg, j);
                                    nodeXml.Add(NewNode(curvePointId, coordinates, new TagsCollection()));
                                    nodeRefs.Add(curvePointId);
                                }

                                var wayId = CreateID(TRANSPORT_LINE, entity.Index, 1, seg);
                                var tags = new TagsCollection();
                                tags.AddOrReplace("osm_export_route", type);
                                tags.AddOrReplace("osm_export_route_color", colorCode);
                                tags.AddOrReplace("osm_export_route_ref", routeNum);
                                tags.AddOrReplace("osm_export_route_name", NameToString(name));
                                tags.AddOrReplace("layer", "5");
                                wayXml.Add(NewWay(wayId, nodeRefs, tags));
                            }
                        }
                        seg++;
                    }
                }
                
            }

            lineEntities.Dispose();
        }

        private void AddTrees(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
        {
            var trees = m_TreeQuery.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < trees.Length; i++)
            {
                var entity = trees[i];
                var transform = EntityManager.GetComponentData<Game.Objects.Transform>(entity);
                var prefabRef = EntityManager.GetComponentData<PrefabRef>(entity);
                var coordinates = GeoCoordinate.FromGameCoordinages(transform.m_Position.x, transform.m_Position.z);
                var tags = new TagsCollection();
                tags.AddOrReplace("natural", "tree");

                nodeXml.Add(NewNode(CreateID(TREE, entity.Index), coordinates, tags));
            }

            trees.Dispose();
        }
        private void AddWaterBodies(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
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

                sortedInnerEdges = sortedInnerEdges.Where(s => s.Count > 2).ToList();

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
                    var id = CreateID(WATER, wb, 0, node.Item1, node.Item2);

                    nodeXml.Add(NewNode(id, coordinates, new TagsCollection()));
                }

                var outerWayId = CreateID(WATER, wb, 1, 0);
                var outerNodeRefs = new List<long>();
                var outerTags = new TagsCollection();
                foreach (var node in sortedOuterEdges)
                {
                    outerNodeRefs.Add(CreateID(WATER, wb, 0, node.Item1, node.Item2));
                }
                outerNodeRefs.Add(CreateID(WATER, wb, 0, sortedOuterEdges[0].Item1, sortedOuterEdges[0].Item2));

                if (sortedInnerEdges.Count == 0)
                {
                    outerTags.AddOrReplace("natural", "water");
                }

                wayXml.Add(NewWay(outerWayId, outerNodeRefs, outerTags));

                if (sortedInnerEdges.Count > 0)
                {
                    var innerIds = new List<long>();
                    for (int i = 0; i < sortedInnerEdges.Count; i++)
                    {
                        var innerWayId = CreateID(WATER, wb, 1, i + 1);
                        var innerNodeRefs = new List<long>();
                        var innerTags = new TagsCollection();
                        foreach (var node in sortedInnerEdges[i])
                        {
                            innerNodeRefs.Add(CreateID(WATER, wb, 0, node.Item1, node.Item2));
                        }
                        innerNodeRefs.Add(CreateID(WATER, wb, 0, sortedInnerEdges[i][0].Item1, sortedInnerEdges[i][0].Item2));

                        wayXml.Add(NewWay(innerWayId, innerNodeRefs, innerTags));
                        innerIds.Add(innerWayId);
                    }

                    // Add the multipolygon relation

                    var relationId = CreateID(WATER, wb, 2);
                    var members = new List<OsmSharp.RelationMember>();
                    var tags = new TagsCollection();
                    members.Add(NewMember(outerWayId, OsmSharp.OsmGeoType.Way, "outer"));
                    foreach (var innerWayId in innerIds)
                    {
                        members.Add(NewMember(innerWayId, OsmSharp.OsmGeoType.Way, "inner"));
                    }
                    tags.AddOrReplace("natural", "water");
                    if (sortedInnerEdges.Count > 0)
                    {
                        tags.AddOrReplace("type", "multipolygon");
                    }
                    relationXml.Add(NewRelation(relationId, members, tags));
                }
            }
        }

        private bool IsUnderwater(WaterSurfaceData waterSurfaceData, TerrainHeightData terrainHeightData, float3 position, out float height, out float depth)
        {
            height = TerrainUtils.SampleHeight(ref terrainHeightData, position);
            depth = WaterUtils.SampleDepth(ref waterSurfaceData, position);
            return depth > 0f;
        }

        private void AddContourLines(List<OsmSharp.Node> nodeXml, List<OsmSharp.Way> wayXml, List<OsmSharp.Relation> relationXml)
        {
            m_Logger.Info("Generating contour lines...");

            TerrainHeightData heightData = m_TerrainSystem.GetHeightData();
            Bounds bounds = m_TerrainSystem.GetTerrainBounds();

            const int rectSize = 20;

            int maxSizeX = 0, maxSizeZ = 0;
            for (float x = bounds.min.x; x < bounds.max.x; x += rectSize, maxSizeX++) ;
            for (float z = bounds.min.z; z < bounds.max.z; z += rectSize, maxSizeZ++) ;

            float[,] heightMap = new float[maxSizeX, maxSizeZ];
            float maxHeight = -10000;
            float minHeight = 10000;

            int xi = 0;
            for (float x = bounds.min.x; x < bounds.max.x; x += rectSize, xi += 1)
            {
                int zi = 0;
                for (float z = bounds.min.z; z < bounds.max.z; z += rectSize, zi += 1)
                {
                    var position = new float3(x, 0, z);
                    var height = TerrainUtils.SampleHeight(ref heightData, position);
                    heightMap[xi, zi] = height;
                    if (height < minHeight) minHeight = height;
                    if (height > maxHeight) maxHeight = height;
                }
            }

            int minLine = (int)minHeight / 20;
            int maxLine = (int)maxHeight / 20;

            HashSet<(int, int)> points = new HashSet<(int, int)> ();

            // Marching squares
            for (int line = minLine; line < maxLine; line++)
            {
                float height = line * 20;
                var tags = new TagsCollection();
                tags.AddOrReplace("contour", "elevation");
                tags.AddOrReplace("ele", $"{line*20}");
                m_Logger.Info($"Generating contour line {height}...");
                for (int x = 0; x < maxSizeX-1; x += 1)
                {
                    for (int z = 0; z < maxSizeZ - 1; z += 1)
                    {
                        bool tl = heightMap[x, z] >= height;
                        bool tr = heightMap[x + 1, z] >= height;
                        bool bl = heightMap[x, z + 1] >= height;
                        bool br = heightMap[x + 1, z + 1] >= height;

                        if ((tl && tr && !bl && br) || (!tl && !tr && bl && !br))
                        {
                            points.Add((x * 2 + 0, z * 2 + 1));
                            points.Add((x * 2 + 1, z * 2 + 2));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+0, z*2+1),
                                    CreateID(CONTOUR, 1, x*2+1, z*2+2),
                                },
                                tags
                            ));
                        }
                        else if (tl && tr && bl && !br || (!tl && !tr && !bl && br))
                        {
                            points.Add((x * 2 + 2, z * 2 + 1));
                            points.Add((x * 2 + 1, z * 2 + 2));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+2, z*2+1),
                                    CreateID(CONTOUR, 1, x*2+1, z*2+2),
                                },
                                tags
                            ));
                        }
                        else if ((tl && tr && !bl && !br) || (!tl && !tr && bl && br))
                        {
                            points.Add((x * 2 + 0, z * 2 + 1));
                            points.Add((x * 2 + 2, z * 2 + 1));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+0, z*2+1),
                                    CreateID(CONTOUR, 1, x*2+2, z*2+1),
                                },
                                tags
                            ));
                        }
                        else if ((tl && !tr && bl && br) || (!tl && tr && !bl && !br))
                        {
                            points.Add((x * 2 + 1, z * 2 + 0));
                            points.Add((x * 2 + 2, z * 2 + 1));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+0),
                                    CreateID(CONTOUR, 1, x*2+2, z*2+1),
                                },
                                tags
                            ));
                        }
                        else if (tl && !tr && !bl && br)
                        {
                            points.Add((x * 2 + 1, z * 2 + 0));
                            points.Add((x * 2 + 0, z * 2 + 1));
                            points.Add((x * 2 + 1, z * 2 + 2));
                            points.Add((x * 2 + 2, z * 2 + 1));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z, 1),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+0),
                                    CreateID(CONTOUR, 1, x*2+0, z*2+1),
                                },
                                tags
                            ));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z, 2),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+2),
                                    CreateID(CONTOUR, 1, x*2+2, z*2+1),
                                },
                                tags
                            ));
                        }
                        else if (!tl && tr && bl && !br)
                        {
                            points.Add((x * 2 + 0, z * 2 + 1));
                            points.Add((x * 2 + 1, z * 2 + 2));
                            points.Add((x * 2 + 1, z * 2 + 0));
                            points.Add((x * 2 + 2, z * 2 + 1));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z, 1),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+0, z*2+1),
                                    CreateID(CONTOUR, 1, x*2+1, z*2+2),
                                },
                                tags
                            ));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z, 2),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+0),
                                    CreateID(CONTOUR, 1, x*2+2, z*2+1),
                                },
                                tags
                            ));
                        }
                        else if ((tl && !tr && bl && !br) || (!tl && tr && !bl && br))
                        {
                            points.Add((x * 2 + 1, z * 2 + 0));
                            points.Add((x * 2 + 1, z * 2 + 2));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+0),
                                    CreateID(CONTOUR, 1, x*2+1, z*2+2),
                                },
                                tags
                            ));
                        }
                        else if ((tl && !tr && !bl && !br) || (!tl && tr && bl && br))
                        {
                            points.Add((x * 2 + 1, z * 2 + 0));
                            points.Add((x * 2 + 0, z * 2 + 1));
                            wayXml.Add(NewWay(
                                CreateID(CONTOUR, 0, x, z),
                                new long[] {
                                    CreateID(CONTOUR, 1, x*2+1, z*2+0),
                                    CreateID(CONTOUR, 1, x*2+0, z*2+1),
                                },
                                tags
                            ));
                        }
                    }
                }
            }

            foreach (var (x, z) in points)
            {
                var coords = GeoCoordinate.FromGameCoordinages(bounds.min.x + x * rectSize / 2, bounds.min.z + z * rectSize / 2);
                nodeXml.Add(NewNode(CreateID(CONTOUR, 1, x, z), coords, new TagsCollection()));
            }
        }

        private string NameToString(NameSystem.Name name)
        {
            // Read private fields m_NameID and m_NameType
            var nameID = (string)name.GetType().GetField("m_NameID", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);
            var nameType = (NameSystem.NameType)name.GetType().GetField("m_NameType", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);
            var nameArgs = (string[])name.GetType().GetField("m_NameArgs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(name);

            if (nameID == null)
            {
                return "";
            }

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
                if (nameArgs == null)
                {
                    nameArgs = new string[0];
                }
                for (int i = 0; i < nameArgs.Length; i++)
                {
                    if (nameArgs[i] != null && GameManager.instance.localizationManager.activeDictionary.TryGetValue(nameArgs[i], out var localizedArg))
                    {
                        nameArgs[i] = localizedArg;
                    }
                }
                if (GameManager.instance.localizationManager.activeDictionary.TryGetValue(nameID, out var localized))
                {
                    for (int i = 0; i < nameArgs.Length - 1; i += 2)
                    {
                        if (nameArgs[i] != null && nameArgs[i + 1] != null)
                        {
                            localized = localized.Replace("{" + nameArgs[i] + "}", nameArgs[i + 1]);
                        }
                    }
                    output = localized;
                }
            }
            return output;
        }

        private float3 Rotate(float3 point, quaternion q)
        {
            quaternion p = new quaternion(0, point.x, point.y, point.z);
            quaternion qi = new quaternion(q.value.x, -q.value.y, -q.value.z, -q.value.w);
            return math.mul(math.mul(q, p), qi).value.yzw;
        }

        private const int NODE = 10;
        private const int BEZIER_NODE = 11;
        private const int WAY = 20;
        private const int AREA = 30;
        private const int BUILDING = 60;
        private const int TRANSPORT_STOP = 70;
        private const int TRANSPORT_LINE = 80;
        private const int WATER = 40;
        private const int CONTOUR = 90;
        private const int TREE = 100;

        private static Dictionary<string, int> IDs;
        private static int IdCounter = 0;

        private static long CreateID(params int[] parts)
        {
            string ans = "";
            foreach (int part in parts)
            {
                var partStr = part.ToString();
                var len = partStr.Length.ToString("00");
                ans += len + partStr;
            }
            if (IDs.ContainsKey(ans))
            {
                return IDs[ans];
            }
            IDs[ans] = ++IdCounter;
            return IdCounter;
        }
    }
}
