using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using OsmSharp;
using OsmSharp.Tags;

namespace Osm2Svg
{
    public interface IStyleRule
    {
        bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) osmGeo,
            Dictionary<(OsmGeoType, long), List<OsmGeo>> memberOf);
        IEnumerable<IStyleRule> NeededRelations();
    }

    public static class StyleRule
    {
        public static IStyleRule FromJson(JObject json)
        {
            var allRules = new List<IStyleRule>();
            foreach (var (key, value) in json)
            {
                switch (key)
                {
                    case "$type":
                        allRules.Add(new TypeRule(value.Value<string>()));
                        break;
                    case "$member":
                        allRules.Add(new IsMemberOf(FromJson((JObject) value)));
                        break;
                    case "$or":
                        var subrules = new List<IStyleRule>();
                        foreach (var subElem in (JArray) value)
                        {
                            subrules.Add(FromJson((JObject) subElem));
                        }
                        allRules.Add(new OrRule(subrules));
                        break;
                    default:
                        if (key.StartsWith("$"))
                        {
                            throw new Exception("Unknown meta key: " + key);
                        }

                        allRules.Add(new HasTagCombination(key, value.Value<string>()));
                        break;
                }
            }


            if (allRules.Count == 0)
            {
                throw new Exception("NO rules given");
            }

            if (allRules.Count == 1)
            {
                return allRules[0];
            }

            return new IfRule(allRules);
        }
    }

    public class IsMemberOf : IStyleRule
    {
        private readonly IStyleRule _relationToMatch;

        public IsMemberOf(IStyleRule relationToMatch)
        {
            _relationToMatch = relationToMatch;
        }

        public bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) geo, Dictionary<(OsmGeoType, long), List<OsmGeo>> memberships)
        {
            if (memberships == null)
            {
                return true; // We cheat a little
            }
            
            if (!memberships.TryGetValue((geo.Type, geo.Id), out var memberOf))
            {
                return false;
            }
            foreach (var relation in memberOf)
            {
                if (_relationToMatch.Matches((OsmGeoType.Relation, relation.Id.Value, relation.Tags), memberships))
                {
                    return true;
                }
            }

            return false;
        }

        public IEnumerable<IStyleRule> NeededRelations()
        {
            return new List<IStyleRule> {_relationToMatch}.Concat(_relationToMatch.NeededRelations());
        }

        public override string ToString()
        {
            return "$member of :\n| " + _relationToMatch.ToString().Replace("\n", "\n|  ");
        }
    }

    public class HasTagCombination : IStyleRule
    {
        private readonly string _key;
        private readonly string _value;

        public HasTagCombination(string key, string value)
        {
            _key = key;
            _value = value;
        }

        public bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) osmGeo, Dictionary<(OsmGeoType, long), List<OsmGeo>> _)
        {
            var tags = osmGeo.Tags;
            if (tags == null)
            {
                return false;
            }

            if (tags.TryGetValue(_key, out var value))
            {
                if (_value.Equals("*"))
                {
                    return true;
                }
                return _value.Equals(value);
            }

            return false;
        }

        public IEnumerable<IStyleRule> NeededRelations()
        {
            return new List<IStyleRule>();
        }

        public override string ToString()
        {
            return _key + " = " + _value;
        }
    }

    public class TypeRule : IStyleRule
    {
        private readonly OsmGeoType _typeToMatch;

        public TypeRule(string typeToMatch)
        {
            switch (typeToMatch)
            {
                case "node":
                    _typeToMatch = OsmGeoType.Node;
                    break;
                case "way":
                    _typeToMatch = OsmGeoType.Way;
                    break;
                case "relation":
                    _typeToMatch = OsmGeoType.Relation;
                    break;
                default: throw new ArgumentException("Unknown $type: " + typeToMatch);
            }
        }

        public bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) osmGeo, Dictionary<(OsmGeoType, long), List<OsmGeo>> _)
        {
            return osmGeo.Type == _typeToMatch;
        }

        public IEnumerable<IStyleRule> NeededRelations()
        {
            return new List<IStyleRule>();
        }

        public override string ToString()
        {
            return "$type = " + _typeToMatch;
        }
    }
    
    public class OrRule : IStyleRule
    {
        private readonly List<IStyleRule> _rules;

        public OrRule(List<IStyleRule> rules)
        {
            _rules = rules;
        }
        public bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) osmGeo, Dictionary<(OsmGeoType, long), List<OsmGeo>> memberOf)
        {
           return _rules.Any(r => r.Matches(osmGeo, memberOf));
        }

        public IEnumerable<IStyleRule> NeededRelations()
        {
            return _rules.SelectMany(r => r.NeededRelations());
        }
    }

   
    public class IfRule : IStyleRule
    {
        private readonly List<IStyleRule> _subrules;

        public IfRule(List<IStyleRule> subrules)
        {
            _subrules = subrules;
        }


        public bool Matches((OsmGeoType Type, long Id, TagsCollectionBase Tags) osmGeo, Dictionary<(OsmGeoType, long), List<OsmGeo>> memberOf)
        {
            foreach (var rule in _subrules)
            {
                if (!rule.Matches(osmGeo, memberOf))
                {
                    return false;
                }
            }

            return true;
        }


        public IEnumerable<IStyleRule> NeededRelations()
        {
            return _subrules.SelectMany(rule => rule.NeededRelations());
        }

        public override string ToString()
        {
            return string.Join(",\n", _subrules.Select(r => r.ToString()));
        }
    }
}