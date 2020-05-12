using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DummyExecutable
{
    class DummyResponseEntry
    {
        // Argument strings starting with this will receive this response
        public string ArgumentPrefix { get; set; }

        // If supplied, write these lines to the standard out stream before returning
        public string[] StandardOutputResponseLines { get; set; }

        // If supplied, write these lines to the standard error stream before returning
        public string[] StandardErrorResponseLines { get; set; }

        // Wait this many seconds before returning (If desired, for simulating work that takes a while)
        public int DelayTimeInSeconds { get; set; } = 0;

        public int ExitCode { get; set; } = 0;
    }
}
