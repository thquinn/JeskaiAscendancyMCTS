using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JeskaiAscendancyMCTS {
    class Program {
        public static Random random = new Random();

        static void Main(string[] args) {
            float[] rewards = new float[] { 0, 0, 0, 0, 1, .9f, .75f, .5f, .4f, .3f, .2f, .1f };
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
            Console.WriteLine(state);
            while (true) {
                MCTS mcts = new MCTS(state, rewards);
                mcts.Rollout(10000);
                int bestMove = mcts.GetBestMove();
                Console.WriteLine(state.MoveToString(bestMove));
                Console.WriteLine();
                state.ExecuteMove(bestMove);
                Console.WriteLine();
                Console.WriteLine(state);
                Console.ReadLine();
            }
        }
    }
}