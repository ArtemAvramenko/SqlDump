// <copyright file="Dumper.cs">
//   SqlDump - Simple SQL Server database dumper
//   (c) 2023 Artem Avramenko. https://github.com/ArtemAvramenko/SqlDump
//   License: MIT
// </copyright>

using System;

namespace SqlDumper
{
    public sealed class DumperEventArgs : EventArgs
    {
        private readonly string _text;

        public DumperEventArgs(string text)
        {
            _text = text;
        }

        public string Text => _text;
    }
}
