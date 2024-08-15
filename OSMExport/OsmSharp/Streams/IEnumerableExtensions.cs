using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OsmSharp.Streams
{
    /// <summary>
    /// Extension methods to easily save/load OSM data.
    /// </summary>
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Creates a new file and write the data as an OSM-XML file.
        /// </summary>
        /// <param name="data">The data to write.</param>
        /// <param name="file">The file to create.</param>
        public static void CreateAsOsmFile(this IEnumerable<OsmGeo> data, string file)
        {
            using var stream = File.Create(file);
            using var outputStream = new OsmSharp.Streams.XmlOsmStreamTarget(stream);
            outputStream.RegisterSource(data);
            outputStream.Pull();
            outputStream.Flush();
        }
    }
}