using System;
using System.Collections.Generic;
using System.Linq;
using query_suggestion.Models;

namespace query_suggestion.Services
{
    public class TrieService
    {
        private readonly TrieNode _trie;

        public TrieService(int threshold = 10)
        {
            _trie = new TrieNode(threshold);
        }

        public void Insert(string title, int popularity)
        {
            _trie.Insert(title, popularity);
        }

        /* Get the search titles from the list */
        public List<(string title, int popularity)> Search(string prefix)
        {
            var node = _trie;
            foreach (var ch in prefix)
            {
                if (!node.Children.ContainsKey(ch))
                {
                    return new List<(string, int)>();
                }
                node = node.Children[ch];
            }
            return node.GetTitles().Select(t => (prefix + t.title, t.popularity)).ToList();
        }

        /* adds levenshtein searching if search returns less than 10 items */
        public List<(string title, int popularity)> GetSuggestionsWithLevenshtein(string query)
        {
            var exactMatches = Search(query);
            var additionalSuggestions = new List<(string title, int popularity)>();

            if (exactMatches.Count < 10)
            {
                additionalSuggestions = FindSimilarTitles(query)
                    .Select(w => (title: w, popularity: GetPopularityFromTrie(w)))
                    .Where(w => !exactMatches.Any(s => s.title == w.title))
                    .Take(10 - exactMatches.Count)
                    .ToList();
            }

            return exactMatches
                    .Concat(additionalSuggestions)
                    .OrderByDescending(s => s.popularity)
                    .ThenBy(s => Levenshtein(query, s.title))
                    .ToList();
        }

        /* Get popularity of each titles */
        private int GetPopularityFromTrie(string title)
        {
            return _trie.Search(title, out int popularity) ? popularity : 0;
        }

        /* Get similarities from the list and the input and edit with levenshtein with the edit distance of 2 */
        private List<string> FindSimilarTitles(string input)
        {
            var results = new List<string>();
            foreach (var title in GetAllTitles(_trie, ""))
            {
                if (Levenshtein(input, title) <= 2)
                {
                    results.Add(title);
                }
            }
            return results;
        }

        /* Get all the titles */
        public List<string> GetAllTitles(TrieNode node, string prefix)
        {
            var titles = new List<string>();
            foreach (var (title, _) in node.GetTitles())
            {
                titles.Add(prefix + title);
            }
            return titles;
        }

        /* Levenshtein Distance*/
        private int Levenshtein(string input, string target)
        {
            if (input == target)
            {
                return 0;
            }

            if (input.Length == 0)
            {
                return target.Length;
            }

            if (target.Length == 0)
            {
                return input.Length;
            }

            var distance = new int[input.Length + 1, target.Length + 1];
            for (int i = 0; i <= input.Length; i++)
            {
                distance[i, 0] = i;
            }

            for (int j = 0; j <= target.Length; j++)
            {
                distance[0, j] = j;
            }

            for (int i = 1; i <= input.Length; i++)
            {
                for (int j = 1; j <= target.Length; j++)
                {
                    int cost = input[i - 1] == target[j - 1] ? 0 : 1;
                    distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
                }
            }
            return distance[input.Length, target.Length];
        }

        /* Get All Titles in the list */
        public List<(string title, int popularity)> GetAlltitlesWithPopularity()
        {

            var titles = GetAlltitlesWithPopularity(_trie, "");
            return titles.OrderByDescending(s => s.popularity).ThenBy(s => s.title).ToList();
        }

        private List<(string title, int popularity)> GetAlltitlesWithPopularity(TrieNode node, string prefix)
        {
            var titles = new List<(string title, int popularity)>();
            if (node.IsEndOfWord)
            {
                titles.Add((prefix, node.Popularity));
            }
            foreach (var child in node.Children)
            {
                titles.AddRange(GetAlltitlesWithPopularity(child.Value, prefix + child.Key));
            }
            return titles;
        }

        /* Get specific name in trie */
        public (string title, int popularity)? GettitleByName(string title)
        {
            var node = _trie;
            foreach (var ch in title)
            {
                if (!node.Children.ContainsKey(ch))
                {
                    return null;
                }
                node = node.Children[ch];
            }

            if (node.IsEndOfWord)
            {
                return (title, node.Popularity);
            }

            return null;
        }


        /* Add popularity */
        public void IncrementPopularity(string title, int add)
        {
            var node = _trie;
            foreach (var ch in title)
            {
                if (!node.Children.ContainsKey(ch))
                {
                    return;
                }
                node = node.Children[ch];
            }

            if (node.IsEndOfWord)
            {
                node.Popularity += add;
            }

        }

        /* Pagination */
        public List<(string title, int popularity)> GetPaginatedList(int pageNumber, int pageSize)
        {
            var alltitles = GetAlltitlesWithPopularity();
            return alltitles.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        }

        public List<(string title, int popularity)> GetPaginatedSearch(string title, int pageNumber, int pageSize)
        {
            var searchTitles = GetSuggestionsWithLevenshtein(title);
            return searchTitles.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        }

        /* List size counting */
        public int GetAllCount()
        {
            return GetAlltitlesWithPopularity().Count;
        }

        public int GetSearchCount(string title)
        {
            return GetSuggestionsWithLevenshtein(title).Count;
        }
    }
}