using System.Collections.Generic;

namespace query_suggestion.Models
{
    public class TrieNode
    {
        public Dictionary<char, TrieNode> Children { get; private set; } = new Dictionary<char, TrieNode>();
        public bool IsEndOfWord { get; set; } = false;
        public int Popularity { get; set; } = 0;

        private int Threshold;
        public List<(string title, int popularity)> Entries;

        public TrieNode(int threshold = 1000)
        {
            Threshold = threshold;
            Entries = new List<(string title, int popularity)>();
        }
        /* Insert data to list */
        public void Insert(string title, int popularity)
        {
            if (Entries != null)
            {
                Entries.Add((title, popularity));
                /* Convert to trie if Threshold reached*/
                if (Entries.Count > Threshold)
                {
                    ConvertToTrie();
                }
            }
            else
            {
                InsertIntoTrie(title, popularity);
            }
        }
        /* Insert to trie if data of list are empty */
        private void InsertIntoTrie(string title, int popularity)
        {
            var node = this;
            foreach (var ch in title)
            {
                if (!node.Children.ContainsKey(ch))
                {
                    node.Children[ch] = new TrieNode(Threshold);
                }
                node = node.Children[ch];
            }
            node.IsEndOfWord = true;
            node.Popularity = popularity;
        }
        /* Convert to trie once list reaches the given threshold */
        private void ConvertToTrie()
        {
            foreach (var entry in Entries)
            {
                InsertIntoTrie(entry.title, entry.popularity);
            }
            Entries = new List<(string title, int popularity)>();
        }

        /* Search in the list */
        public bool Search(string title, out int popularity)
        {
            popularity = 0;
            if (Entries != null)
            {
                var found = Entries.Find(e => e.title == title);
                if (!string.IsNullOrEmpty(found.title))
                {
                    popularity = found.popularity;
                    return true;
                }
                return false;
            }
            else
            {
                return SearchInTrie(title, out popularity);
            }
        }
        
        /* Search in tries if list are empty */
        private bool SearchInTrie(string title, out int popularity)
        {
            popularity = 0;
            var node = this;
            foreach (var ch in title)
            {
                if (!node.Children.ContainsKey(ch))
                {
                    return false;
                }
                node = node.Children[ch];
            }
            if (node.IsEndOfWord)
            {
                popularity = node.Popularity;
                return true;
            }
            return false;
        }

        /* Get the data titles and popularity*/
        public List<(string title, int popularity)> GetTitles()
        {
            var titles = new List<(string title, int popularity)>();
            if (IsEndOfWord)
            {
                titles.Add((string.Empty, Popularity));
            }

            foreach (var child in Children)
            {
                var childTitles = child.Value.GetTitles();
                titles.AddRange(childTitles.Select(t => (child.Key + t.title, t.popularity)));
            }

            return titles.OrderBy(s => s.title).ThenByDescending(s => s.popularity).ToList();
        }
    }
}