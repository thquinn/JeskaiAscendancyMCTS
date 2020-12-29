using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    public static class Util {
        static string SAVE_FILE = "dmcts.txt";

        public static void Save(DecklistMCTS dmcts, int move) {
            List<string> lines = new List<string>();
            if (File.Exists(SAVE_FILE)) {
                lines.Add("");
            }
            lines.AddRange(SplitNewLines(dmcts.ToString()));
            DMCTSSaveState state = new DMCTSSaveState(dmcts, move);
            foreach (var kvp in dmcts.startingDecklist) {
                lines.Add(string.Format("{0} {1}", kvp.Value, State.CARD_NAMES[kvp.Key]));
            }
            lines.Add("");
            lines.Add(string.Join(" ", state.decklist.Select(kvp => string.Format("{0} {1}", (int)kvp.Key, kvp.Value))));
            lines.Add(string.Join(" ", state.additions));
            lines.Add(string.Join(" ", state.deletions));
            lines.Add(state.lastMove.ToString());
            File.AppendAllLines(SAVE_FILE, lines);
        }
        public static DMCTSSaveState Load() {
            if (!File.Exists(SAVE_FILE)) return null;
            string[] lines = File.ReadAllLines(SAVE_FILE);
            return new DMCTSSaveState(lines[lines.Length - 4], lines[lines.Length - 3], lines[lines.Length - 2], int.Parse(lines[lines.Length - 1]));
        }

        public static string[] SplitNewLines(string text) {
            return Regex.Split(text, "\n|\r|\r\n");
        }
    }

    public class DMCTSSaveState {
        public Dictionary<Card, int> decklist;
        public int[] additions, deletions;
        public int lastMove;

        public DMCTSSaveState(DecklistMCTS dmcts, int move) {
            decklist = dmcts.startingDecklist;
            lastMove = move;
            Card card = (Card)Math.Abs(move);
            if (move > 0) {
                if (decklist.ContainsKey(card)) {
                    decklist[card]++;
                } else {
                    decklist[card] = 1;
                }
                HashSet<int> validAdditions = new HashSet<int>(dmcts.startingAdditions);
                if (decklist[card] == DecklistMCTS.CARD_QUANTITY_LIMITS[card].Item2) {
                    validAdditions.Remove(move);
                }
                HashSet<int> validDeletions = new HashSet<int>(dmcts.startingDeletions);
                validDeletions.Remove(-move);
                additions = validAdditions.ToArray();
                deletions = validDeletions.ToArray();
            } else if (move < 0) {
                if (decklist[card] == 1) {
                    decklist.Remove(card);
                } else {
                    decklist[card]--;
                }
                HashSet<int> validAdditions = new HashSet<int>(dmcts.startingAdditions);
                validAdditions.Remove(-move);
                HashSet<int> validDeletions = new HashSet<int>(dmcts.startingDeletions);
                if (decklist[card] == DecklistMCTS.CARD_QUANTITY_LIMITS[card].Item1) {
                    validDeletions.Remove(move);
                }
                additions = validAdditions.ToArray();
                deletions = validDeletions.ToArray();
            } else {
                additions = new int[0];
                deletions = new int[0];
            }
        }
        public DMCTSSaveState(string decklistLine, string additionsLine, string deletionsLine, int lastMove) {
            int[] deckInts = decklistLine.Split(' ').Select(s => int.Parse(s)).ToArray();
            decklist = new Dictionary<Card, int>();
            for (int i = 0; i < deckInts.Length; i += 2) {
                decklist[(Card)deckInts[i]] = deckInts[i + 1];
            }
            if (additionsLine.Length > 0) {
                additions = additionsLine.Split(' ').Select(s => int.Parse(s)).ToArray();
                deletions = deletionsLine.Split(' ').Select(s => int.Parse(s)).ToArray();
            } else {
                additions = new int[0];
                deletions = new int[0];
            }
            this.lastMove = lastMove;
        }
    }

    public static class ArrayExtensions {
        public static T[] Shuffle<T>(this T[] array) {
            int n = array.Length;
            for (int i = 0; i < n; i++) {
                int r = i + StaticRandom.Next(n - i);
                T t = array[r];
                array[r] = array[i];
                array[i] = t;
            }
            return array;
        }
    }
}
