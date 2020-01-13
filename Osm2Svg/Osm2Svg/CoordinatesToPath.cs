using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using OsmSharp;

namespace Osm2Svg
{
    public class CoordinatesToPath
    {
        private readonly uint _maxX;
        private readonly uint _maxY;
        private readonly (double minLon, double minLat, double maxLon, double maxLat) _bbox;

        public CoordinatesToPath(uint maxX, uint maxY,
            (double minLon, double minLat, double maxLon, double maxLat) bbox)
        {
            _maxX = maxX;
            _maxY = maxY;
            _bbox = bbox;
        }

        [SuppressMessage("ReSharper", "PossibleInvalidOperationException")]
        public (int x, int y) Wgs84ToXY(Node n)
        {
            var x =
                _maxX * (n.Longitude.Value - _bbox.minLon)
                / (_bbox.maxLon - _bbox.minLon);

            var y = _maxY * (n.Latitude.Value - _bbox.minLat)
                    / (_bbox.maxLat - _bbox.minLat);

            return ((int) x, (int) (_maxY - y));
        }

        public List<(int x, int y)> Wgs84ToPath(List<Node> coors)
        {
            return coors.Select(Wgs84ToXY).ToList();
        }

        public string PathToSvg(List<(int x, int y)> coors, string style)
        {
            var current = coors[0];
            var path = "M " + current.x + "," + current.y;
            var addedSegments = 0;
            for (var i = 1; i < coors.Count; i++)
            {
                var elem = coors[i];
                var totalDiff = Math.Abs(elem.x - current.x) + Math.Abs(elem.y - current.y);
                if (totalDiff == 0)
                {
                    continue;
                }

                addedSegments++;
                path += " l " + (elem.x - current.x) + "," + (elem.y - current.y);
                current = elem;
            }

            if (addedSegments == 0)
            {
                return null;
            }

            return $"<path {style} d=\"{path}\" />";
        }
    }
}