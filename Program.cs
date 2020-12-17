// ReSharper disable StringLiteralTypo

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Permissions;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WatchSourceFiles
{
    [PermissionSet(SecurityAction.LinkDemand, Name = "FullTrust")]
    [PermissionSet(SecurityAction.InheritanceDemand, Name = "FullTrust")]
    static class Program
    {
        private static readonly CancellationTokenSource Cts = new CancellationTokenSource();
        private static string _outDir = "";

        private static void Main(string[] args)
        {
            try
            {
                const string exitHint = "Tippen Sie 'x', um die Überwachung zu beenden.";

                if (args.Any(arg => arg.Contains("/?")))
                {
                    ShowUsage();

                    Console.Out.WriteLine(exitHint);
                    while (Console.Read() != 'x') { }

                    return;
                }

                bool isElevated;

                using (var identity = WindowsIdentity.GetCurrent())
                {
                    var principal = new WindowsPrincipal(identity);
                    isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
                }

                if (!isElevated)
                {
                    Console.Out.WriteLine("Das Programm muss mit Administratorrechten aufgerufen werden!!!");

                    StartProcess(Assembly.GetExecutingAssembly().Location, 
                        string.Join(" ", args), ProcessWindowStyle.Normal,
                        "runas", true, false, false);

                    return;
                }

                var watchDir = GetDirectoryArg(args, "/watchdir:", 
                        Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), false);
                
                _outDir = GetDirectoryArg(args, "/outdir:", 
                    Path.Combine(Environment.GetFolderPath(
                                    Environment.SpecialFolder.CommonApplicationData), 
                                    "MM", "CopiedSourceFiles"), true);

                Console.Out.WriteLine("Das Verzeichnis");
                Console.Out.WriteLine($"{watchDir}");
                Console.Out.WriteLine("wird inkl. Unterverzeichnisse");
                Console.Out.WriteLine("auf Erstellung und Änderungen von Dateien überwacht.");
                Console.Out.WriteLine("Gefundene Dateien werden nach");
                Console.Out.WriteLine($"{_outDir} kopiert.");
                Console.Out.WriteLine("==============================================================");
                Console.Out.WriteLine();

                var filters = new List<string>(args.ToList().
                    Where(arg => !string.IsNullOrEmpty(arg) && arg.StartsWith("*.")));
                
                if (!filters.Any()) 
                    filters.AddRange(new[] { "*.vb", "*.cs", "*.dll" });

                filters.ForEach(filter => Task.Run(() => 
                    RunWatcher(watchDir, filter, Cts.Token), Cts.Token));

                Console.Out.WriteLine(exitHint);
                Console.Out.WriteLine();
                while (Console.Read() != 'x') {}

                Cts.Cancel();
            }
            catch (Exception e)
            {
                Console.Out.WriteLine(e);
            }
        }

        private static void ShowUsage()
        {
            var fileInfo = new FileInfo(Assembly.GetExecutingAssembly().Location);
            var sb = new StringBuilder();
            sb.AppendLine("Das Programm muss als Administrator ausgeführt werden,");
            sb.AppendLine("um den Zugriff auf das zu überwachende Verzeichnis zu gewährleisten.");
            sb.AppendLine();
            sb.AppendLine("Beispiel Programmaufruf:");
            sb.AppendLine($"{fileInfo.Name} *.vb *.cs /watchdir:\"c:\\temp\" /outdir:\"c:\\files\"");
            sb.AppendLine();
            sb.AppendLine("[Filter] z.B. *.vb *.cs");
            sb.AppendLine("\t\tFilter sind optional und werden Leerzeichen-getrennt und mit * beginnend aufgelistet.");
            sb.AppendLine("\t\tSind keine Filter angegeben, werden die Filter '*.vb *.cs *.dll' verwendet.");
            sb.AppendLine("[/watchdir:<Verzeichnis>]");
            sb.AppendLine("\t\tDas zu überwachende Verzeichnis ist optional.");
            sb.AppendLine("\t\tIst es nicht angegeben, wird das Verzeichnis verwendet, in dem sich das Programm befindet.");
            sb.AppendLine("[/outdir:<Verzeichnis>]");
            sb.AppendLine("\t\tDas Ausgabeverzeichnis, in das die gefundenen Dateien kopiert werden, ist optional.");
            sb.AppendLine("\t\tIst es nicht angegeben, wird das Verzeichnis '%programdata%\\MM\\CopiedSourceFiles' verwendet.");
            Console.Out.WriteLine(sb.ToString());
        }

        private static string GetDirectoryArg(IReadOnlyList<string> args, string argName, string defaultValue, bool createIfNotExists)
        {
            var dir = GetArg(args, argName, defaultValue);

            if (string.IsNullOrWhiteSpace(dir))
            {
                throw new DirectoryNotFoundException(argName);
            }

            if (!Directory.Exists(dir) && createIfNotExists)
            {
                Directory.CreateDirectory(dir);
            }

            if (!Directory.Exists(dir))
            {
                throw new DirectoryNotFoundException(dir);
            }

            return dir;
        }

        private static string GetArg(IReadOnlyList<string> args, string argName, string defaultValue)
        {
            for (var index = 0; index < args.Count; index++)
            {
                var arg = args[index];
                if (string.IsNullOrEmpty(arg))
                    continue;

                if (!arg.StartsWith(argName, StringComparison.InvariantCultureIgnoreCase)) 
                    continue;

                return arg.Equals(argName)
                    ? index < args.Count - 1 ? args[index + 1] : defaultValue
                    : arg.Substring(argName.Length);
            }

            return defaultValue;
        }

        private static void RunWatcher(string path, string extension, CancellationToken ct)
        {
            Console.Out.WriteLine($"Das Verzeichnis wird auf {extension}-Dateien überwacht.");

            using (var watcher = new FileSystemWatcher { Path = path, Filter = extension })
            {
                watcher.Created += WatcherOnCreated;
                watcher.Changed += WatcherOnChanged;
                watcher.Renamed += WatcherOnRenamed;
                watcher.Deleted += WatcherOnDeleted;
                watcher.Error += WatcherOnError;
                watcher.IncludeSubdirectories = true;
                watcher.EnableRaisingEvents = true;
                while (!ct.IsCancellationRequested) {}
            }
        }

        private static void WatcherOnError(object sender, ErrorEventArgs e)
        {
            try
            {
                Console.Out.WriteLine($"Fehler: {e.GetException()}");
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
            }
        }

        private static void WatcherOnDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.Out.WriteLine($"Datei {e.FullPath} wird {GetActionText(e.ChangeType)}");
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
            }
        }

        private static void WatcherOnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                Console.Out.WriteLine($"Datei {e.OldFullPath} wird {GetActionText(e.ChangeType)} zu {e.FullPath}");
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
            }
        }

        private static void WatcherOnChanged(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.Out.WriteLine($"Datei {e.FullPath} wird {GetActionText(e.ChangeType)}");

                var fileInfo = new FileInfo(e.FullPath);
                if (fileInfo.Directory == null) 
                    return;

                var dir = Path.Combine(_outDir, fileInfo.Directory.Name);
                var target = Path.Combine(dir, fileInfo.Name);

                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                Console.Out.WriteLine($"Datei wird von {e.FullPath}\r\nnach {target} kopiert.");

                File.Copy(e.FullPath, target, true);
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
            }
        }

        private static void WatcherOnCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                Console.Out.WriteLine($"Datei {e.FullPath} wird {GetActionText(e.ChangeType)}");
            }
            catch (Exception exception)
            {
                Console.Out.WriteLine(exception);
            }
        }

        private static void StartProcess(string fileName, string arguments, ProcessWindowStyle windowStyle,
            string verb, bool useShellExecute, bool createNoWindow, bool waitForExit)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = useShellExecute,
                    CreateNoWindow = createNoWindow,
                    Verb = verb,
                    WindowStyle = windowStyle
                }
            };

            process.Start();
            if (!waitForExit) return;
            process.WaitForExit();
        }

        private static string GetActionText(WatcherChangeTypes type)
        {
            switch (type)
            {
                case WatcherChangeTypes.Created:
                    return "erstellt";
                case WatcherChangeTypes.Deleted:
                    return "gelöscht";
                case WatcherChangeTypes.Changed:
                    return "geändert";
                case WatcherChangeTypes.Renamed:
                    return "umbenannt";
                case WatcherChangeTypes.All:
                    return "geändert";
                default:
                    return "";
            }
        }
    }
}
