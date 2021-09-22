using System;
using System.Collections.Generic;
using System.Text;

namespace SqlDumper
{
    public sealed class DumperEventArgs : EventArgs
    {
        private readonly string _Text;

        public DumperEventArgs(string text)
        {
            _Text = text;
        }

        public string Text => _Text;
    }
}
