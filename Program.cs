using System;

namespace SqlDumper
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var sqlString = @"Server=.;Database=Test;User Id=Test;Password=11111";
            var dumper = new Dumper(sqlString);
            dumper.IgnoredTableNames = new[] { "" };
            dumper.Dump(Console.Out);
        }
    }
}
