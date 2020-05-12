using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace DummyExecutable
{
    class Program
    {
        private static string _responseOutputFolder;

        static int Main(string[] args)
        {
            // This exe is meant to stand in for others, e.g. adb or mlaunch, so it can't 
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DUMMY_EXE_OUTPUT_FOLDER")))
            {
                _responseOutputFolder = Environment.GetEnvironmentVariable("DUMMY_EXE_OUTPUT_FOLDER");
            }
            else
            {
                _responseOutputFolder = Path.Combine(Environment.CurrentDirectory, "dummy-output");
            }

            // If there's a real reason to support this argument we can explore other means
            // (such as environment variables).  Specifying this makes it fast to generate response files.
            if (args[0].Equals("--make-entry"))
            {
                return RecordProcessOutputsForTestUsage(args);
            }


            string responseFilePath = Path.Combine(Environment.CurrentDirectory, "responses.json");

            if (File.Exists(responseFilePath))
            {
                return ProduceResponseFromConfigFileIfPresent(JsonSerializer.Deserialize<List<DummyResponseEntry>>(File.ReadAllText(responseFilePath)), args);
            }
            else
            {
                RecordSimulatedProcessInvocation(new string[] { $"Dummy Exe Called with Args: {string.Join(' ', args)}" }, new string[] { }, 0);
                return 0;
            }
        }



        public static int ProduceResponseFromConfigFileIfPresent(List<DummyResponseEntry> responses, string[] args)
        {
            string argumentsString = string.Join(' ', args);

            DummyResponseEntry match = responses.Where(r => argumentsString.StartsWith(r.ArgumentPrefix)).SingleOrDefault();

            if (match != null)
            {
                RecordSimulatedProcessInvocation(match.StandardOutputResponseLines, match.StandardErrorResponseLines, match.ExitCode);

                // Now, actually respond to the caller with the pre-determined lines
                foreach (string line in match.StandardOutputResponseLines)
                {
                    Console.WriteLine(line);
                }
                foreach (string line in match.StandardErrorResponseLines)
                {
                    Console.Error.WriteLine(line);
                }
                return match.ExitCode;
            }

            RecordSimulatedProcessInvocation(new string[] { $"Dummy Exe Called with Args: {string.Join(' ', args)}" }, new string[] { }, 0);
            return 0;
        }

        public static void RecordSimulatedProcessInvocation(string[] stdOutLines, string[] stdErrLines, int exitCode)
        {
            Directory.CreateDirectory(_responseOutputFolder);
            int epochTime = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
            string outputEntryName = Path.Combine(_responseOutputFolder, $"dummy-exe-{epochTime}.json");
        }

        private static int RecordProcessOutputsForTestUsage(string[] parameters)
        {
            string fileToRun = parameters[1];
            string arguments = parameters.Length > 2 ? string.Join(' ', parameters, 2, parameters.Length - 2) : "";

            if (!File.Exists(fileToRun))
            {
                throw new FileNotFoundException($"Cannot find '{fileToRun}'");
            }

            ProcessStartInfo processStartInfo = new ProcessStartInfo
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(fileToRun),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                FileName = fileToRun,
                Arguments = arguments
            };
            var p = new Process() { StartInfo = processStartInfo };
            var standardOut = new StringBuilder();
            var standardErr = new StringBuilder();

            p.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardOut)
                {
                    if (e.Data != null)
                    {
                        standardOut.AppendLine(e.Data);
                        Console.WriteLine(e.Data);
                    }
                }
            };

            p.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
            {
                lock (standardErr)
                {
                    if (e.Data != null)
                    {
                        standardErr.AppendLine(e.Data);
                        Console.Error.WriteLine(e.Data);
                    }
                }
            };

            p.Start();

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            // Allow the process time to send messages to the above delegates
            // if the process exits very quickly
            System.Threading.Thread.Sleep(1000);

            p.WaitForExit();

            WriteProcessInvocationEntry(arguments, standardOut.ToString(), standardErr.ToString(), p.ExitCode);

            return p.ExitCode;
        }

        private static void WriteProcessInvocationEntry(string arguments, string stdOut, string stdErr, int exitCode)
        {
            string dummyOutputJson = Path.Combine(_responseOutputFolder, "generated-test-outputs.json");

            List<DummyResponseEntry> entries = new List<DummyResponseEntry>();
            if (File.Exists(dummyOutputJson))
            {
                entries.AddRange(JsonSerializer.Deserialize<List<DummyResponseEntry>>(File.ReadAllText(dummyOutputJson)));
            }
            entries.Add(new DummyResponseEntry()
            {
                ArgumentPrefix = arguments,
                ExitCode = exitCode,
                StandardOutputResponseLines = stdOut.Split(Environment.NewLine),
                StandardErrorResponseLines = stdErr.Split(Environment.NewLine)
            });

            JsonSerializerOptions jsonSerializerOptions = new JsonSerializerOptions()
            {
                WriteIndented = true
            };
            using (StreamWriter writer = new StreamWriter(dummyOutputJson, false)) 
            {
                writer.Write(JsonSerializer.Serialize<List<DummyResponseEntry>>(entries, jsonSerializerOptions));
            }
        }
    }
}
