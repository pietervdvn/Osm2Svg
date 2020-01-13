using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using OsmSharp;

namespace Osm2Svg
{
    public class StyleSheet
    {
        private readonly List<(string style, IStyleRule rule)> _stylings;

        private StyleSheet(List<(string style, IStyleRule rule)> stylings)
        {
            _stylings = stylings;
        }

        public List<string> GetStylesInOrder()
        {
            return _stylings.Select(style => style.style).ToList();
        }

        /// <summary>
        /// Gives stylerules which gives relations of which members might be matched
        /// </summary>
        /// <returns></returns>
        public IEnumerable<IStyleRule> NeededRelations()
        {
            return _stylings.SelectMany(rule => rule.rule.NeededRelations());
        }

        public string Matches(OsmGeo obj, Dictionary<(OsmGeoType, long), List<OsmGeo>> memberships)
        {
            foreach (var (style, rule) in _stylings)
            {
                if (rule.Matches((obj.Type, obj.Id.Value, obj.Tags), memberships))
                {
                    return style;
                }
            }

            return null;
        }

        private static List<(string style, IStyleRule rule)> RulesFromPath(string path)
        {
            var json = JObject.Parse(File.ReadAllText(path));

            var rules = new List<(string style, IStyleRule rule)>();

            if (json.ContainsKey("import"))
            {
                foreach (var imported in (JArray) json["import"])
                {
                    rules = RulesFromPath(imported.Value<string>() + ".json").Concat(rules).ToList();
                }
            }

            var knownStyles = new Dictionary<string, string>();
            if (json.ContainsKey("styles"))
            {
                foreach (var (name, style) in (JObject) json["styles"])
                {
                    var styleString = CreateStyling(style, knownStyles);
                    knownStyles[name] = styleString;
                }
            }

            if (json.ContainsKey("rules"))
            {
                foreach (var rule in (JArray) json["rules"])
                {
                    var style = rule["style"];
                    var styleString = CreateStyling(style, knownStyles);

                    var matcher = StyleRule.FromJson((JObject) rule["if"]);
                    rules.Add((styleString, matcher));
                }
            }

            return rules;
        }

        public static StyleSheet FromPath(string path)
        {
            return new StyleSheet(RulesFromPath(path));
        }

        private static string CreateStyling(JToken style, Dictionary<string, string> knownStyles)
        {
            if (style.Type.Equals(JTokenType.String))
            {
                var key = style.Value<string>();
                return knownStyles[key];
            }


            var styleParts = new List<string>();
            foreach (var (key, value) in (JObject) style)
            {
                styleParts.Add(key + "=\"" + value + "\"");
            }

            return string.Join(" ", styleParts);
        }
    }
}