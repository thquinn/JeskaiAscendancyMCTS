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
                { Card.Island, 50 },
                { Card.Mountain, 1 },
                { Card.Plains, 1 },
                { Card.MysticMonastery, 4 },
                { Card.EvolvingWilds, 4 },
            }, 7);
            Console.WriteLine(state);
            Console.WriteLine(string.Join(", ", state.GetMoves()));
            Console.ReadLine();
            state.SanityCheck();
        }
    }
}