using System;
using System.Collections.Generic;
using System.Linq;

namespace Osm2Svg
{
    public class LineMerger
    {
        private Dictionary<(int x, int y), Stack<Line>> _byFirstPoint = new Dictionary<(int x, int y), Stack<Line>>();
        private Dictionary<(int x, int y), Stack<Line>> _byLastPoint = new Dictionary<(int x, int y), Stack<Line>>();

        private uint _intake = 0;


        public void AddWay(List<(int x, int y)> w)
        {
            _intake++;
            var last = w.Count - 1;

            if (_byLastPoint.TryGetValue(w[0], out var firstPointMatch) &&
                firstPointMatch.Any())
            {
                var match = firstPointMatch.Pop();
                match.Points = match.Points.Concat(w).ToList();
                AddMatch(match, match.Last, _byLastPoint);
                return;
            }
            if (_byFirstPoint.TryGetValue(w[last], out var lastPointMatch) &&
                lastPointMatch.Any())
            {
                var match = lastPointMatch.Pop();
                match.Points = w.Concat(match.Points).ToList();
                AddMatch(match, match.First, _byFirstPoint);
                return;
            }

            if (_byFirstPoint.TryGetValue(w[0], out var firstMatch) && firstMatch.Any())
            {
                var match = firstMatch.Pop();
                w = w.Select(c => c).Reverse().ToList();
                match.Points = w.Concat(match.Points).ToList(); // Keep the last point intact
                AddMatch(match, match.First, _byFirstPoint);
                return;
            }

            if (_byLastPoint.TryGetValue(w[last], out var lastMatch) && lastMatch.Any())
            {
                var match = lastMatch.Pop();
                w = w.Select(c => c).Reverse().ToList();
                match.Points = match.Points.Concat(w).ToList(); // Keep the first point intacts
                AddMatch(match, match.Last, _byLastPoint);
                return;
            }

            // Totally new entry
            var line = new Line(w); // This MUST be the same object
            AddMatch(line, w[0], _byFirstPoint);
            AddMatch(line, w[last], _byLastPoint);
        }


        private void AddMatch(Line w, (int x, int y) nodeId,
            IDictionary<(int x, int y), Stack<Line>> dict)
        {
            if (dict.TryGetValue(nodeId, out var stack))
            {
                stack.Push(w);
            }
            else
            {
                var nwStack = new Stack<Line>();
                nwStack.Push(w);
                dict[nodeId] = nwStack;
            }
        }

        public List<List<(int x, int y)>> GetWays()
        {
            var ways = _byFirstPoint.Values
                .SelectMany(list => list)
                .Select(line => line.Points).ToList();
            Console.WriteLine($"Intake: {_intake}, outgoing: {ways.Count}");
            return ways;
        }
    }

    class Line
    {
        public List<(int x, int y)> Points;

        public (int x, int y) First => Points[0];
        public (int x, int y) Last => Points[Points.Count - 1];

        public Line(List<(int x, int y)> line)
        {
            Points = line;
        }
    }
}