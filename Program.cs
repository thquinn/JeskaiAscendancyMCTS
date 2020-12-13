using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        public static Random random = new Random();

        static void Main(string[] args) {
            State state = new State(new Dictionary<Card, int>() {
                { Card.Plains, 1 },
                { Card.Island, 12 },
                { Card.Mountain, 1 },
                { Card.MysticMonastery, 4 },
                { Card.EvolvingWilds, 4 },
                { Card.Brainstorm, 4 },
                { Card.CeruleanWisps, 2 },
                { Card.Fatestitcher, 4 },
                { Card.FranticInventory, 4 },
                { Card.FranticSearch, 4 },
                { Card.GitaxianProbe, 4 },
                { Card.JeskaiAscendancy, 4 },
                { Card.ObsessiveSearch, 2 },
                { Card.Opt, 4 },
                { Card.Ponder, 4 },
                { Card.TreasureCruise, 2 },
            }, 7);

            int decisions = 0;
            while (!state.IsWon()) {
                Console.WriteLine(state);
                state.SanityCheck();
                int[] moves = state.GetMoves();
                Console.WriteLine(string.Join("\n", moves.Select(s => s + ": " + state.MoveToString(s))));
                Console.WriteLine();
                int move = int.MinValue;
                while (!moves.Contains(move)) {
                    if (!int.TryParse(Console.ReadLine(), out move)) move = int.MinValue;
                }
                if (moves.Length > 1) decisions++;
                float probability = state.ExecuteMove(move);
                Console.WriteLine("Resulting state had {0}% probability.", (probability * 100).ToString("N1"));
                Console.WriteLine();
            }

            Console.WriteLine(state);
            Console.WriteLine("Won on turn {0} with {1} decisions!", state.turn, decisions);
            Console.ReadLine();
        }
    }
}