// This file is part of OSMRender.
// Copyright (c) 2024 Iikka Hauhio
//
// OSMRender is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// OSMRender is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with OSMRender. If not, see <https://www.gnu.org/licenses/>.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Xml.Serialization;
using OSMRender.Geo;
using OSMRender.Logging;
using OSMRender.Rules;
using OsmSharp.API;
using OsmSharp.Tags;

namespace OSMExport.OSMRenderUtils;

public class Reader
{

    private readonly ILogger Logger;

    public Reader(ILogger logger)
    {
        Logger = logger;
    }

    public (GeoDocument, OSMRender.Geo.Bounds) ReadOSM(string path)
    {
        Osm osm;
        GeoDocument doc;

        using (var stream = new FileStream(path, FileMode.Open))
        {
            XmlSerializer serializer = new(typeof(Osm));
            osm = (Osm)(serializer.Deserialize(stream) ?? throw new NullReferenceException());
            doc = OSMToGeoDocument(osm);
        }

        Logger.Debug($"Found {doc.Points.Count} points");
        Logger.Debug($"Found {doc.Lines.Count} lines");
        Logger.Debug($"Found {doc.Areas.Count} areas");
        Logger.Debug($"Found {doc.Relations.Count} relations");

        var bounds = osm.Bounds is not null && osm.Bounds.MinLatitude is not null ? OSMBoundsToBounds(osm.Bounds) : doc.Bounds;

        return (doc, bounds);
    }

    public Ruleset ReadRules(string path)
    {
        var ruleCode = File.ReadAllText(path);
        var rules = Parser.ParseRules(ruleCode, Logger);
        return rules;
    }

    /// <summary>
    /// Creates a new GeoDocument from OSM data. Adds nodes as Points, ways as Lines and Areas, and relations as Areas or Relations.
    /// If a way has the same start and end point, it is added both as a Line and an Area. Similarly, multipolygon relations are added both as Relations and Areas.
    /// </summary>
    /// <param name="source">the OSM data</param>
    /// <returns>the GeoDocument</returns>
    public GeoDocument OSMToGeoDocument(Osm source)
    {

        Dictionary<long, Point> points = new();
        Dictionary<long, OSMRender.Geo.Area> areas = new();
        Dictionary<long, Line> lines = new();
        Dictionary<long, Relation> relations = new();

        long maxPointId = 0;
        foreach (var node in source.Nodes)
        {
            if (node is null) continue;
            points[node.Id ?? 0] = new Point(node.Id ?? 0, TagsToDictionary(node.Tags), node.Latitude ?? 0, node.Longitude ?? 0);
            if ((node.Id ?? 0) > maxPointId)
            {
                maxPointId = node.Id ?? 0;
            }
        }

        var bounds = points.Select(p => p.Value.Bounds).Aggregate((a, b) => a.MergeWith(b));

        long maxWayId = 0;
        foreach (var way in source.Ways)
        {
            if (way is null) continue;
            lines[way.Id ?? 0] = new Line(way.Id ?? 0, TagsToDictionary(way.Tags));
            foreach (var nodeRef in way.Nodes)
            {
                lines[way.Id ?? 0].Nodes.Add(points[nodeRef]);
            }

            if (way.Nodes.First() == way.Nodes.Last())
            {
                // Add also as an area
                var area = new OSMRender.Geo.Area(way.Id ?? 0, TagsToDictionary(way.Tags));
                area.OuterEdges.Add(new());
                foreach (var nodeRef in way.Nodes)
                {
                    area.OuterEdges[0].Add(points[nodeRef]);
                }
                areas[area.Id] = area;
            }

            if ((way.Id ?? 0) > maxWayId)
            {
                maxWayId = way.Id ?? 0;
            }
        }

        foreach (var relation in source.Relations)
        {
            if (relation is null) continue;
            relations[relation.Id ?? 0] = new Relation(relation.Id ?? 0, TagsToDictionary(relation.Tags));
            /*foreach (var member in relation.Members) {
                relations[relation.Id ?? 0].Members.Add(new Relation.Member {
                    Role = member.Role,
                    Value = member.Type == OsmSharp.OsmGeoType.Node ? points[member.Id] :
                        member.Type == OsmSharp.OsmGeoType.Way ? lines[member.Id] :
                        relations[member.Id],
                });
            }*/

            // Add also as an area
            if (relation.Tags.Contains(new Tag("type", "multipolygon")))
            {
                var area = new OSMRender.Geo.Area(relation.Id ?? 0, TagsToDictionary(relation.Tags));
                foreach (var member in relation.Members)
                {
                    if (!lines.ContainsKey(member.Id))
                    {
                        Logger.Warning($"Relation {relation.Id} is missing member {member.Id}");
                        continue;
                    }
                    if (member.Role == "outer")
                    {
                        area.OuterEdges.Add(lines[member.Id].Nodes);
                    }
                    else if (member.Role == "inner")
                    {
                        area.InnerEdges.Add(lines[member.Id].Nodes);
                    }
                }
                if (area.OuterEdges.Count > 0)
                {
                    areas[area.Id] = area;
                }
            }
        }

        return new GeoDocument(points, areas, lines, relations);
    }

    private static Dictionary<string, string> TagsToDictionary(TagsCollectionBase? tags)
    {
        return tags?.ToDictionary(t => t.Key, t => t.Value) ?? new();
    }

    /// <summary>
    /// Converts OsmSharp Bounds objects to OSMRender Bounds objects.
    /// </summary>
    /// <param name="bounds">an OsmSharp.API.Bounds object</param>
    /// <returns>an OSMRender.Geo.Bounds object</returns>
    public static OSMRender.Geo.Bounds OSMBoundsToBounds(OsmSharp.API.Bounds bounds)
    {
        return OSMRender.Geo.Bounds.From(bounds.MinLatitude ?? 0f, bounds.MaxLatitude ?? 0f, bounds.MinLongitude ?? 0f, bounds.MaxLongitude ?? 0f);
    }
}