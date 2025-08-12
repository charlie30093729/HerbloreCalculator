using System;

namespace HerbloreCalculator.Utils
{
    public static class ConsoleHelper
    {
        public static void SetColor(double value)
        {
            Console.ForegroundColor = value >= 0 ? ConsoleColor.Green : ConsoleColor.Red;
        }
    }
}
