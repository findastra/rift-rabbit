// Rift Rabbit's program (Wonderland Launchpad, pet program #29), its first native piece: the launcher behind the Launch
// buttons on its VRChat setups. Astra, 2026-10-24: "Launching is done by Rift Rabbit". It runs in the background with no
// tray icon and no window (Astra, 2026-10-21: only Startup Star is in the tray); its menu goes to Startup Star's menu.
// Tools:
//   wonderland-launchpad-v1.0-vr-launch-v1.0   starts a VRChat setup's programs in Astra's order when she presses
//                                              Launch on Rift Rabbit's page opened from this PC (vr-launch-20261024.cs).
// It knows her VR programs (VrPrograms) and reads which VR devices SteamVR has on from SteamVR's own log (VrDevices), the
// same way Benchmark Rock's program-watch and device-watch light the pages: those stay Inference Iguana's, for the
// lights; these are Rift Rabbit's, for launching.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-24. C# 5 for the compiler that ships with Windows (.NET Framework 4).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using AstrasPetOs.Background;
using AstrasPetOs.FarmEvents;

namespace AstrasPetOs.RiftRabbit
{
    static class Rabbit
    {
        public const string Program = "rift-rabbit";
        public const string Pet = "Rift Rabbit";
        public const string Version = "0.1.0";
        public static readonly string[] Tools = { VrLaunch.Tool };

        // The launcher's settings and the page's file: Documents\Astra's PetOS Data\rift-rabbit (a page opened from this
        // PC reads its file there; nothing in it is ever committed).
        public static string DocumentsFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Astra's PetOS Data", Program);
        }

        [STAThread]
        static int Main(string[] args)
        {
            if (Has(args, "--help") || Has(args, "/?")) { Console.WriteLine(Usage); return 0; }
            if (Has(args, "--self-test")) return SelfTest.Run();
            var rabbit = new RabbitState(FarmEvent.DataRoot(args), Value(args, "--data"));
            string tool = Value(args, "--tool");
            if (tool != null)
            {
                string onOff = args.SkipWhile(a => !string.Equals(a, "--tool", StringComparison.OrdinalIgnoreCase)).Skip(2).FirstOrDefault();
                if (!Tools.Contains(tool) || (onOff != "on" && onOff != "off")) { Console.WriteLine("Use --tool <" + string.Join("|", Tools) + "> on|off"); return 1; }
                rabbit.SetTool(tool, onOff == "on");
                if (!Signals.Send(rabbit.MutexName, Signals.Reload)) rabbit.WriteMenu();
                Console.WriteLine(tool + " is " + onOff.ToUpperInvariant() + ".");
                return 0;
            }
            if (Has(args, "--quit")) { Console.WriteLine(Signals.Send(rabbit.MutexName, Signals.Quit) ? "Rift Rabbit's launcher was asked to stop." : "Rift Rabbit's launcher is not running."); return 0; }
            if (Has(args, "--programs"))
            {
                foreach (var st in VrPrograms.Look(VrPrograms.Programs)) Console.WriteLine((st.Running ? "open    " : "closed  ") + st.Program.Name);
                foreach (var d in VrDevices.Look(VrDevices.LogFile())) Console.WriteLine((d.On ? "on      " : "off     ") + d.Name);
                return 0;
            }
            // --startup (sign-in), --watch, or a double-click: keep the launcher listening; a second copy exits quietly.
            Application.EnableVisualStyles();
            bool created;
            using (var mutex = new Mutex(true, rabbit.MutexName, out created))
            {
                if (!created) return 0;
                Application.Run(new Watcher(rabbit));
            }
            return 0;
        }

        const string Usage = "rift-rabbit-20261024.exe          keep Rift Rabbit's launcher listening (no window, no tray icon; its menu\n" +
            "                                  is in Startup Star's menu under Rift Rabbit); --startup and --watch do the same\n" +
            "  --quit                          stop the running launcher\n" +
            "  --tool <label> on|off           switch a tool ON or OFF\n" +
            "  --programs                      which of her VR programs are open and which VR devices SteamVR has on\n" +
            "  --self-test                     run the built-in checks (console program)\n" +
            "  --data <folder>                 use another data folder (tests); default %LOCALAPPDATA%\\astras-pet-os";

        public static bool Has(string[] args, string flag) { return args.Any(a => string.Equals(a, flag, StringComparison.OrdinalIgnoreCase)); }

        public static string Value(string[] args, string flag)
        {
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }
    }

    // The tools' ON/OFF, the farm event log and the menu and mood files Startup Star reads, in
    // %LOCALAPPDATA%\astras-pet-os\rift-rabbit.
    sealed class RabbitState
    {
        public readonly string DataRoot;
        public readonly FarmEventLog Log;
        public readonly string DataArg;   // set for a test data folder: then nothing is written to Documents
        public readonly string MutexName;
        readonly Dictionary<string, bool> toolOn = new Dictionary<string, bool>();
        readonly string statePath;
        readonly object gate = new object();

        public RabbitState(string dataRoot, string customData)
        {
            DataRoot = dataRoot;
            DataArg = customData == null ? "" : "--data \"" + dataRoot + "\"";
            MutexName = "Local\\astras-pet-os-rift-rabbit-" + FarmEvent.Sha256Text(dataRoot.ToLowerInvariant()).Substring(0, 12);
            Log = new FarmEventLog(dataRoot, Rabbit.Program);
            statePath = Path.Combine(Log.Folder, "state-20261024.json");
            ReloadTools();
        }

        public void ReloadTools()
        {
            lock (gate)
            {
                foreach (string t in Rabbit.Tools) toolOn[t] = true;
                try
                {
                    if (!File.Exists(statePath)) return;
                    var tools = FarmEvent.Map(FarmEvent.Get(FarmEvent.Map(FarmEvent.Json().DeserializeObject(File.ReadAllText(statePath))), "tools"));
                    if (tools != null)
                        foreach (string t in Rabbit.Tools) { object v = FarmEvent.Get(tools, t); if (v is bool) toolOn[t] = (bool)v; }
                }
                catch (Exception) { }
            }
        }

        public bool On(string tool) { lock (gate) return toolOn[tool]; }

        public void SetTool(string tool, bool on)
        {
            lock (gate)
            {
                toolOn[tool] = on;
                var data = new Dictionary<string, object>();
                data["schema"] = "rift-rabbit-state-v1";
                data["tools"] = toolOn.ToDictionary(p => p.Key, p => (object)p.Value);
                data["updated"] = FarmEvent.Now(DateTimeOffset.Now);
                File.WriteAllText(statePath, FarmEvent.Json().Serialize(data), new UTF8Encoding(false));
            }
            Event(tool, "tool-switched", "info", tool + " turned " + (on ? "ON" : "OFF"), null);
        }

        // Rift Rabbit's menu in Startup Star's menu: what the launcher is doing, its tool's ON/OFF, quit.
        public void WriteMenu()
        {
            bool on = On(VrLaunch.Tool);
            var items = new List<PetMenuItem> { PetMenuItem.Note(on ? "Launcher listening on this PC" : "Launcher is OFF"), PetMenuItem.Line() };
            foreach (string t in Rabbit.Tools) items.Add(PetMenuItem.Tool(t, On(t)));
            items.Add(PetMenuItem.Line());
            items.Add(PetMenuItem.Run("Quit Rift Rabbit's launcher", "--quit"));
            try { MenuFile.Write(Log.Folder, Rabbit.Program, Rabbit.Pet, items); } catch (Exception) { }
        }

        public void WriteMood(string mood, string line)
        {
            var m = new Dictionary<string, object>();
            m["mood"] = mood;
            m["line"] = line;
            try { MoodFile.Write(Log.Folder, Rabbit.Program, m); } catch (Exception) { }
        }

        public void Event(string source, string kind, string severity, string summary, IDictionary<string, object> details)
        {
            try { Log.Write(source, kind, severity, summary, null, details, null); } catch (Exception) { }
        }
    }

    // ---------------------------------------------------------------- her VR programs

    sealed class VrProgram
    {
        public string Id, Name, PathHas, Open;
        public string[] Exes;
    }

    sealed class VrProgramState
    {
        public VrProgram Program;
        public readonly List<int> Pids = new List<int>();
        public int Windows;
        public bool Running { get { return Pids.Count > 0; } }
    }

    static class VrPrograms
    {
        // System Map id | name | its programs' names (no .exe) | text its file's path must hold, or nothing | how it opens:
        // its Start menu entry (shell:AppsFolder\...), its Steam link, or its own program file, as her vr-gaming desktop
        // shortcuts open them.
        public static readonly string[] Table = {
            @"virtual-desktop|Virtual Desktop|virtualdesktop.streamer||shell:AppsFolder\{6D809377-6AF0-444B-8957-A3773F02200E}\Virtual Desktop Streamer\VirtualDesktop.Streamer.exe",
            @"steam|Steam|steam||shell:AppsFolder\{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\Steam\Steam.exe",
            @"meta-horizon-link|Meta Horizon Link|client,oculusclient|\meta horizon\|shell:AppsFolder\{6D809377-6AF0-444B-8957-A3773F02200E}\Meta Horizon\Support\oculus-client\Client.exe",
            @"steamvr|SteamVR|vrserver,vrmonitor||steam://rungameid/250820",
            @"vrcft|VRCFaceTracking|vrcfacetracking||steam://rungameid/3329480",
            @"ovr-advanced-settings|OVR Advanced Settings|advancedsettings||steam://rungameid/1009850",
            @"ovr-dynamic-resolution|OVR Dynamic Resolution|ovr-dynamic-resolution||steam://rungameid/3243840",
            @"space-calibrator|Space Calibrator|spacecalibrator||steam://rungameid/3368750",
            // Installed 2026-10-24 from their GitHub releases (Space Override v10, Space Sync 2.5.0): each one's overlay.
            @"space-override|Space Override|openvr-spaceoverride||%ProgramFiles%\OpenVR-SpaceOverride\OpenVR-SpaceOverride.exe",
            @"space-sync|Space Sync|spacesync||%ProgramFiles%\SpaceSync\SpaceSync.exe",
            @"double-hip-tracker|DoubleHip DoubleChest Tracker|double-hip-tracker||%USERPROFILE%\Documents\DoubleHip DoubleChest\DoubleHipTracker-V1.3.0\double-hip-tracker.exe",
            @"ovr-smooth-tracking|OVR Smooth Tracking|openvr-smoothtracking||%ProgramFiles%\OpenVR-SmoothTracking\OpenVR-SmoothTracking.exe",
            @"oscgoesbrrr|OSCGoesBrrr|oscgoesbrrr||%LOCALAPPDATA%\Programs\OscGoesBrrr\OscGoesBrrr.exe",
            // Voicemeeter Potato (Voicemeeter Bug keeps it open): the Woojer vest plays what it sends.
            @"voicemeeter|Voicemeeter|voicemeeter8,voicemeeter8x64,voicemeeterpro,voicemeeterpro_x64,voicemeeter,voicemeeter_x64||shell:AppsFolder\{7C5A40EF-A0FB-4BFC-874A-C0F2E0B9FA8E}\VB\Voicemeeter\voicemeeter8.exe",
            @"vrchat|VRChat|vrchat||steam://rungameid/438100",
        };

        public static readonly List<VrProgram> Programs = Table.Select(line =>
        {
            var f = line.Split('|');
            return new VrProgram { Id = f[0], Name = f[1], Exes = f[2].Split(','), PathHas = f[3], Open = Environment.ExpandEnvironmentVariables(f[4]) };
        }).ToList();

        // How a program opens: its Start menu entry, its Steam link, or its own program file (an .exe).
        public static bool OpensSafely(string open)
        {
            return open.StartsWith(@"shell:AppsFolder\") || open.StartsWith("steam://rungameid/") ||
                (Path.IsPathRooted(open) && open.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !open.Contains("%"));
        }

        public static VrProgram Find(string id) { return Programs.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase)); }

        static bool PathOk(Process p, VrProgram w)
        {
            if (string.IsNullOrEmpty(w.PathHas)) return true;
            try { return p.MainModule.FileName.ToLowerInvariant().Contains(w.PathHas); }
            catch (Exception) { return true; }
        }

        public static List<VrProgramState> Look(IEnumerable<VrProgram> programs)
        {
            var all = Process.GetProcesses();
            try
            {
                return programs.Select(w =>
                {
                    var st = new VrProgramState { Program = w };
                    foreach (var p in all)
                    {
                        if (!w.Exes.Contains(p.ProcessName.ToLowerInvariant()) || !PathOk(p, w)) continue;
                        st.Pids.Add(p.Id);
                        try { if (p.MainWindowHandle != IntPtr.Zero) st.Windows++; } catch (Exception) { }
                    }
                    return st;
                }).ToList();
            }
            finally { foreach (var p in all) p.Dispose(); }
        }

        public static VrProgramState Look(VrProgram w) { return Look(new[] { w })[0]; }

        // Like clicking the X on each of its windows. Never forced.
        public static string Close(RabbitState rabbit, VrProgram w, int waitSeconds)
        {
            var before = Look(w);
            if (!before.Running) return w.Name + " is not open.";
            int asked = 0;
            foreach (int pid in before.Pids)
            {
                try { using (var p = Process.GetProcessById(pid)) if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow()) asked++; }
                catch (Exception) { }
            }
            string said;
            if (asked == 0) said = w.Name + " is running with no window to close (it may be in the system tray). Close it from its own icon. Nothing was forced.";
            else
            {
                var until = DateTime.Now.AddSeconds(waitSeconds);
                VrProgramState now = before;
                while (DateTime.Now < until) { Thread.Sleep(400); now = Look(w); if (!now.Running) break; }
                said = !now.Running ? w.Name + " closed."
                    : now.Windows == 0 ? w.Name + " closed its window but keeps running in the background (its tray icon). Nothing was forced."
                    : w.Name + " is still open after " + waitSeconds + " seconds: it may be asking you something. Nothing was forced.";
            }
            rabbit.Event(VrLaunch.Tool, said.EndsWith(" closed.") ? "program-closed" : "program-close-waiting", "info", said, new Dictionary<string, object> { { "program", w.Id } });
            return said;
        }
    }

    // ---------------------------------------------------------------- her VR devices, from SteamVR's own log

    sealed class VrDevice
    {
        public string Id, Name, Serial;
        public bool On;
    }

    // SteamVR starts a new vrserver.txt each time it starts and writes every device it adds, every wireless device that
    // drops or comes back, and its own shutdown. Read only.
    static class VrDevices
    {
        // System Map id | name | serial. Trackers are named by their stickers (her ignore-trackers notes).
        public static readonly string[] Table = {
            "quest-pro|Meta Quest Pro|1PASH5D1P17365",
            "quest-3|Meta Quest 3|",
            "index-controller-left|Valve Index controller (left) [LH]|LHR-E255FEDA",
            "index-controller-right|Valve Index controller (right) [RH]|LHR-AE0DDD68",
            "vive-tracker-01|Vive Tracker 3.0 [RF]|LHR-F3DF780D",
            "vive-tracker-02|Vive Tracker 3.0 [L]|LHR-B2105741",
            "vive-tracker-03|Vive Tracker 3.0 [M]|LHR-91D63DB2",
            "vive-tracker-04|Vive Tracker 3.0 [C]|LHR-6BA0C57A",
            "vive-tracker-05|Vive Tracker 3.0 [ ]|LHR-28D88802",
            "vive-tracker-06|Vive Tracker 3.0 [R]|LHR-F8590C98",
            "vive-tracker-07|Vive Tracker 3.0 [LF]|LHR-E0ED8ACE",
            "vive-tracker-08|Vive Tracker 3.0 [H]|LHR-6963FCE5",
            "vive-tracker-09|Vive Tracker 3.0 [W]|LHR-467E1FA2",
        };

        static readonly Regex Added = new Regex(@"Driver '([^']+)' finished adding tracked device with serial number '([^']+)'");
        static readonly Regex Back = new Regex(@"lighthouse: ([A-Z]{3}-[0-9A-F]+): Connected to receiver");
        static readonly Regex Gone = new Regex(@"lighthouse: (?:([A-Z]{3}-[0-9A-F]+): Disconnected from receiver|\S+: Wireless controller ([A-Z]{3}-[0-9A-F]+) disconnected)");

        public static string LogFile() { return Path.Combine(Path.GetDirectoryName(VrLaunch.SteamExe()), "logs", "vrserver.txt"); }

        static bool SteamVrRunning()
        {
            var all = Process.GetProcessesByName("vrserver");
            try { return all.Length > 0; } finally { foreach (var p in all) p.Dispose(); }
        }

        public static List<VrDevice> Look(string logFile) { return Read(logFile, SteamVrRunning()); }

        public static List<VrDevice> Read(string logFile, bool steamVrRunning)
        {
            var on = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            string headset = null;
            if (steamVrRunning && File.Exists(logFile))
            {
                using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.Contains("VR server shutting down")) { on.Clear(); headset = null; continue; }
                        var m = Added.Match(line);
                        if (m.Success) { on[m.Groups[2].Value] = true; if (m.Groups[1].Value != "lighthouse") headset = m.Groups[2].Value; continue; }
                        m = Back.Match(line);
                        if (m.Success) { on[m.Groups[1].Value] = true; continue; }
                        m = Gone.Match(line);
                        if (m.Success) on[m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value] = false;
                    }
                }
            }
            return Table.Select(row =>
            {
                var f = row.Split('|');
                var d = new VrDevice { Id = f[0], Name = f[1], Serial = f[2] };
                bool v;
                // The Quest 3's serial is not known yet: any other headset SteamVR added counts as the Quest 3.
                d.On = d.Serial.Length > 0 ? on.TryGetValue(d.Serial, out v) && v
                    : d.Id == "quest-3" && headset != null && !Table.Any(r => r.Split('|')[2] == headset) && on.TryGetValue(headset, out v) && v;
                return d;
            }).ToList();
        }
    }

    // ---------------------------------------------------------------- the background program

    sealed class Watcher : ApplicationContext
    {
        readonly RabbitState rabbit;
        readonly BackgroundHost host;
        LaunchServer launch;

        public Watcher(RabbitState rabbit)
        {
            this.rabbit = rabbit;
            host = new BackgroundHost(rabbit.MutexName, delegate { rabbit.ReloadTools(); Sync(); }, delegate { Quit("stopped from its menu (--quit)"); });
            rabbit.Event(VrLaunch.Tool, "launcher-started", "info", "Rift Rabbit's launcher started", null);
            Sync();
        }

        // The launcher listens while its tool is ON, and tells the page how to reach it (never for a test data folder).
        void Sync()
        {
            bool on = rabbit.On(VrLaunch.Tool);
            try
            {
                if (on && launch == null) launch = new LaunchServer(rabbit, LaunchSettings.Load(LaunchSettings.Folder()));
                else if (!on && launch != null) { launch.Dispose(); launch = null; }
                if (rabbit.DataArg.Length == 0)
                    VrLaunch.WritePageData(VrLaunch.PageDataFile(), launch != null ? launch.Settings : LaunchSettings.Load(LaunchSettings.Folder()), launch != null);
                rabbit.WriteMood("sleep", launch != null ? "Ready to launch VRChat from my page." : "The launcher is OFF.");
            }
            catch (Exception e)
            {
                rabbit.Event(VrLaunch.Tool, "launcher-failed", "warning", "The launcher could not start: " + e.Message, null);
                rabbit.WriteMood("curious", "The launcher could not start: " + e.Message);
            }
            rabbit.WriteMenu();
        }

        void Quit(string why)
        {
            if (launch != null) { launch.Dispose(); launch = null; }
            if (rabbit.DataArg.Length == 0) try { VrLaunch.WritePageData(VrLaunch.PageDataFile(), LaunchSettings.Load(LaunchSettings.Folder()), false); } catch (Exception) { }
            rabbit.Event(VrLaunch.Tool, "launcher-stopped", "info", "Rift Rabbit's launcher " + why, null);
            rabbit.WriteMood("sleep", "The launcher is not running.");
            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { host.Dispose(); if (launch != null) launch.Dispose(); }
            base.Dispose(disposing);
        }
    }

    static class SelfTest
    {
        static int passed, failed;

        static void Check(string name, bool ok)
        {
            if (ok) passed++; else failed++;
            Console.WriteLine((ok ? "ok    " : "FAIL  ") + name);
        }

        public static int Run()
        {
            Check("programs: every row is well formed, ids once each, and each opens safely", VrPrograms.Table.All(l => l.Split('|').Length == 5) &&
                VrPrograms.Programs.Select(w => w.Id).Distinct().Count() == VrPrograms.Programs.Count && VrPrograms.Programs.All(w => VrPrograms.OpensSafely(w.Open)));
            Check("programs: it opens only a Start menu entry, a Steam link or a program file", !VrPrograms.OpensSafely("cmd /c calc") &&
                !VrPrograms.OpensSafely("https://example.com") && !VrPrograms.OpensSafely(@"%ProgramFiles%\x.exe") && VrPrograms.OpensSafely(@"C:\Program Files\x\x.exe"));
            Check("programs: one look sees each of them, open or not", VrPrograms.Look(VrPrograms.Programs).Count == VrPrograms.Programs.Count);
            Check("devices: every row is well formed, ids once each, [H] is the head tracker's sticker",
                VrDevices.Table.All(l => l.Split('|').Length == 3) && VrDevices.Table.Select(l => l.Split('|')[0]).Distinct().Count() == VrDevices.Table.Length &&
                VrDevices.Table.Any(l => l == "vive-tracker-08|Vive Tracker 3.0 [H]|LHR-6963FCE5"));

            string logTmp = Path.Combine(Path.GetTempPath(), "rift-rabbit-vrserver-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                File.WriteAllLines(logTmp, new[] {
                    "Fri Oct 23 2026 18:01:10.459 [Info] - Driver 'oculus' finished adding tracked device with serial number '1PASH5D1P17365'",
                    "Fri Oct 23 2026 18:01:19.191 [Info] - Driver 'lighthouse' finished adding tracked device with serial number 'LHR-6963FCE5'",
                    "Fri Oct 23 2026 18:01:19.200 [Info] - Driver 'lighthouse' finished adding tracked device with serial number 'LHR-28D88802'",
                    "Fri Oct 23 2026 18:31:29.606 [Info] - lighthouse: LHR-28D88802: Disconnected from receiver C7F9C2B167" });
                var dev = VrDevices.Read(logTmp, true).ToDictionary(d => d.Id, d => d.On);
                Check("devices: the headset and the head tracker SteamVR added are on, a tracker that dropped is off", dev["quest-pro"] && dev["vive-tracker-08"] && !dev["vive-tracker-05"] && !dev["quest-3"]);
                Check("devices: with SteamVR not running, everything is off", VrDevices.Read(logTmp, false).All(d => !d.On));
            }
            finally { try { File.Delete(logTmp); } catch (Exception) { } }

            string temp = Path.Combine(Path.GetTempPath(), "rift-rabbit-self-test-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                var rabbit = new RabbitState(temp, temp);
                Check("tools: ON until switched", Rabbit.Tools.All(t => rabbit.On(t)));
                rabbit.SetTool(VrLaunch.Tool, false);
                Check("tools: the switch is kept", !new RabbitState(temp, temp).On(VrLaunch.Tool));
                rabbit.WriteMenu();
                var menu = FarmEvent.Map(FarmEvent.Json().DeserializeObject(File.ReadAllText(MenuFile.PathFor(rabbit.Log.Folder))));
                Check("menu: the shared menu file, for Rift Rabbit, with a way to quit", FarmEvent.Str(menu, "schema") == "astras-pet-os-menu-v1" && FarmEvent.Str(menu, "pet") == Rabbit.Pet &&
                    File.ReadAllText(MenuFile.PathFor(rabbit.Log.Folder)).Contains("--quit"));
                Check("data: a test folder is marked, so Documents is left alone", rabbit.DataArg.Length > 0);
            }
            finally { try { Directory.Delete(temp, true); } catch (Exception) { } }
            Check("data: the launcher's files are in Documents, Astra's PetOS Data, rift-rabbit", Rabbit.DocumentsFolder().EndsWith(Path.Combine("Astra's PetOS Data", "rift-rabbit")) &&
                VrLaunch.PageDataFile().StartsWith(Rabbit.DocumentsFolder()));

            VrLaunchSelfTest.Run(Check);

            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed");
            return failed == 0 ? 0 : 1;
        }
    }
}
