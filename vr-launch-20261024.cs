// Rift Rabbit's launcher (`wonderland-launchpad-v1.0-vr-launch-v1.0`): the PC side of the Launch buttons on its VRChat
// setups (Wonderland Launchpad, hub page apps/pet-programs/rift-rabbit-20261018.html). A web page cannot start programs,
// so the page asks Rift Rabbit's program on this PC only (127.0.0.1), and it starts each program in Astra's order, waits
// while she puts the headset on, checks that every device and program the mode needs is on, and starts VRChat when she
// presses Start VRChat.
// Astra, 2026-10-24: "Maybe i need to turn off all of the launch dependencies so this can be the main control hub", her
// answers to the six launcher questions ("All recommended"): the launcher owns VRCFaceTracking, OVR Advanced Settings,
// OVR Dynamic Resolution and Space Calibrator (SteamVR's own auto-start is off for them); it checks, she says go; VRChat
// ignores the trackers her notes list for the Double Hip / Double Chest choice, plus the head tracker ([H]) in the modes
// with one; it sets DoubleHip DoubleChest Tracker's Hip / Chest for her choice; 127.0.0.1 only, with a secret code. Then:
// "Launching is done by Rift Rabbit", and "Install both space sync and space override now": each mode switches SteamVR's
// Space Override and Space Sync add-ons before anything starts, and opens its own overlay.
// Claude Opus 5.5 (claude-opus-5-5), 2026-10-24. C# 5 for the compiler that ships with Windows.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AstrasPetOs.FarmEvents;

namespace AstrasPetOs.RiftRabbit
{
    // What one Launch button asks for: the headset box it is in, its mode and that box's switches.
    sealed class LaunchRequest
    {
        public string Headset, Mode, Streamer, Double, Object;
        // Face tracking is a choice on the Quest Pro (Astra, 2026-10-26: "I also do have the option of using facetracking or
        // not on Quest Pro"); a page that does not say keeps it on. Base Station FOV Viewer and VR Pole Overlay open with the
        // launch when ticked ("I would like to be able to turn these on at any moment").
        public bool Osc, Woojer, FaceTracking = true, Fov, Pole;

        public static readonly string[] Headsets = { "quest-pro", "quest-3" };
        // x.4 QuestLHSync (Astra, 2026-10-25, "all recommended"): a rooted headset's own cameras see the base stations, so
        // no tracker rides on the headset.
        public static readonly string[] Modes = { "native", "space-calibrator", "space-override", "space-sync", "questlhsync" };
        public static readonly string[] Streamers = { "virtual-desktop", "steam-link", "meta-horizon-link" };
        public static readonly string[] Doubles = { "", "hip", "chest", "both" };
        // Object Track VR's objects (Astra, 2026-10-24); the program is not out yet.
        public static readonly string[] Objects = { "", "small-hoop", "large-hoop", "dance-pole" };

        public bool Face { get { return Headset == "quest-pro" && FaceTracking; } }
        public string Device { get { return Headset == "quest-pro" ? "Quest Pro" : "Quest 3"; } }
        // Modes x.1 to x.3 have a tracker mounted on the headset; Native and QuestLHSync have none.
        public bool HeadTracker { get { return Mode != "native" && Mode != "questlhsync"; } }

        // Only the page's own fixed values; anything else is refused, so nothing a page sends reaches a command line.
        public static LaunchRequest From(IDictionary<string, object> body, out string problem)
        {
            problem = null;
            var r = new LaunchRequest
            {
                Headset = FarmEvent.Str(body, "headset"), Mode = FarmEvent.Str(body, "mode"),
                Streamer = FarmEvent.Str(body, "streamer") ?? "virtual-desktop", Double = FarmEvent.Str(body, "double") ?? "", Object = FarmEvent.Str(body, "object") ?? "",
                Osc = Equals(FarmEvent.Get(body, "osc"), true), Woojer = Equals(FarmEvent.Get(body, "woojer"), true),
                FaceTracking = !Equals(FarmEvent.Get(body, "face"), false), Fov = Equals(FarmEvent.Get(body, "fov"), true), Pole = Equals(FarmEvent.Get(body, "pole"), true),
            };
            if (!Headsets.Contains(r.Headset)) problem = "Not one of the headsets.";
            else if (!Modes.Contains(r.Mode)) problem = "Not one of the modes.";
            else if (!Streamers.Contains(r.Streamer)) problem = "Not one of the streamers.";
            else if (!Doubles.Contains(r.Double)) problem = "Not one of the Double Hip / Double Chest choices.";
            else if (!Objects.Contains(r.Object)) problem = "Not one of Object Track VR's objects.";
            return problem == null ? r : null;
        }
    }

    // Where each device is worn: Astra's 27 spots (2026-10-25, her drawing: "27 locations tracking can occur, devices may
    // vary"), set with tracker-strapper on the page. Numbers, codes and names are hers; facing her, her right is on the
    // left. 23 Face holds the headset, 21 and 22 the hands (an Index controller or the headset's hand tracking), every
    // other spot a Vive Tracker 3.0.
    static class Spots
    {
        // code|number|name
        public static readonly string[] Table = {
            "RF|1|Right Foot", "LF|2|Left Foot", "RA|3|Right Ankle", "LA|4|Left Ankle", "RK|5|Right Knee", "LK|6|Left Knee",
            "FW|7|Front Waist", "BW|8|Back Waist", "RS|9|Right Side", "LS|10|Left Side", "RFS|11|Right Front-Side",
            "LFS|12|Left Front-Side", "RBS|13|Right Back-Side", "LBS|14|Left Back-Side", "FS|15|Front Sternum",
            "BS|16|Back Sternum", "FC|17|Front Chest", "BC|18|Back Chest", "RE|19|Right Elbow", "LE|20|Left Elbow",
            "RH|21|Right Hand", "LH|22|Left Hand", "F|23|Face", "H|24|Head", "DP|25|Dance Pole", "BDH|26|Big Dance Hoop",
            "LDH|27|Little Dance Hoop" };
        // Double Hip's pair is any two of the eight waist spots, usually Front and Back Waist (Astra, 2026-10-25: "the
        // double hip can be made from any of the 8 on the waist, but typical is FW, BW"); Double Chest's any two of the four
        // chest spots, usually Front and Back Chest.
        public static readonly string[] Waist = { "FW", "BW", "RS", "LS", "RFS", "LFS", "RBS", "LBS" };
        public static readonly string[] Chest = { "FS", "BS", "FC", "BC" };
        public const string Face = "F", Head = "H", RightHand = "RH", LeftHand = "LH";
        public const string HandTracking = "hand-tracking";
        // Object Track VR's objects and the spot of the tracker on each.
        public static readonly Dictionary<string, string> ObjectSpot = new Dictionary<string, string> { { "dance-pole", "DP" }, { "large-hoop", "BDH" }, { "small-hoop", "LDH" } };

        public static bool Known(string code) { return Table.Any(t => t.Split('|')[0] == code); }
        public static string Name(string code)
        {
            var f = Table.Select(t => t.Split('|')).FirstOrDefault(x => x[0] == code);
            return f == null ? code : f[1] + " " + f[2] + " [" + f[0] + "]";
        }
        public static bool Tracker(string device) { return device != null && device.StartsWith("vive-tracker-") && VrDevices.Table.Any(t => t.Split('|')[0] == device); }

        // Which devices may go on a spot.
        public static bool Fits(string code, string device)
        {
            if (code == Face) return LaunchRequest.Headsets.Contains(device);
            if (code == RightHand) return device == "index-controller-right" || device == HandTracking;
            if (code == LeftHand) return device == "index-controller-left" || device == HandTracking;
            return Known(code) && Tracker(device);
        }

        public static string Serial(string device)
        {
            var f = VrDevices.Table.Select(t => t.Split('|')).FirstOrDefault(x => x[0] == device);
            return f == null ? "" : f[2];
        }
    }

    // This PC's settings, never committed: Documents\Astra's PetOS Data\rift-rabbit\vr-launch-settings-20261024.json.
    // Made with today's spots the first time; she changes them with tracker-strapper on the page opened on this PC.
    sealed class LaunchSettings
    {
        public const string FileName = "vr-launch-settings-20261024.json";
        public int Port = 47621;
        public string Code;
        // Device on each spot (Astra, 2026-10-25: "the body spots guess is right"; her stickers in brackets).
        public Dictionary<string, string> Worn = Defaults();
        // The spots of DoubleHip DoubleChest Tracker's two pairs, A first (her app: Hip [W] + [C], Chest [ ] + [M]).
        public string[] HipSpots = { "FW", "BW" }, ChestSpots = { "FC", "BC" };
        public string DoubleHipTracker = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DoubleHip DoubleChest", "DoubleHipTracker-V1.3.0", "double-hip-tracker.exe");

        public static Dictionary<string, string> Defaults()
        {
            return new Dictionary<string, string> {
                { "F", "quest-pro" }, { "H", "vive-tracker-08" }, { "FC", "vive-tracker-05" }, { "BC", "vive-tracker-03" },
                { "FW", "vive-tracker-09" }, { "BW", "vive-tracker-04" }, { "RK", "vive-tracker-06" }, { "LK", "vive-tracker-02" },
                { "RF", "vive-tracker-01" }, { "LF", "vive-tracker-07" }, { "RH", "index-controller-right" }, { "LH", "index-controller-left" } };
        }

        public string At(string code) { string d; return Worn.TryGetValue(code, out d) ? d : ""; }
        public string SerialAt(string code) { return Spots.Serial(At(code)); }

        // The tracker on 24 Head (modes x.1 to x.3), and DoubleHip DoubleChest Tracker's pairs: both spots need a tracker.
        public string HeadTracker { get { return SerialAt(Spots.Head); } }
        public string[] HipPair { get { return PairOf(HipSpots); } }
        public string[] ChestPair { get { return PairOf(ChestSpots); } }
        string[] PairOf(string[] spots) { var p = spots.Select(SerialAt).ToArray(); return p.All(x => x.Length > 0) ? p : new string[0]; }

        // VRChat's --ignore-trackers list for each Double choice: DoubleHip DoubleChest Tracker joins each pair into one
        // tracker for VRChat, so VRChat ignores both of that pair.
        public Dictionary<string, string[]> Ignore
        {
            get { return new Dictionary<string, string[]> { { "", new string[0] }, { "hip", HipPair }, { "chest", ChestPair }, { "both", HipPair.Concat(ChestPair).ToArray() } }; }
        }

        public static string Folder() { return Rabbit.DocumentsFolder(); }

        public static LaunchSettings Load(string folder)
        {
            var s = new LaunchSettings();
            string file = Path.Combine(folder, FileName);
            bool changed = false;
            try
            {
                var m = File.Exists(file) ? FarmEvent.Map(FarmEvent.Json().DeserializeObject(File.ReadAllText(file))) : null;
                if (m != null)
                {
                    object port = FarmEvent.Get(m, "port");
                    if (port is int && (int)port > 1024 && (int)port < 65536) s.Port = (int)port;
                    s.Code = FarmEvent.Str(m, "code");
                    // A file from before the spots (2026-10-24) gets today's spots once.
                    if (FarmEvent.Get(m, "spots") == null || s.SetSpots(FarmEvent.Map(FarmEvent.Get(m, "spots")), FarmEvent.Get(m, "double_hip_spots"), FarmEvent.Get(m, "double_chest_spots")) != null) changed = true;
                    string dht = FarmEvent.Str(m, "double_hip_tracker");
                    if (!string.IsNullOrEmpty(dht) && Path.IsPathRooted(dht) && dht.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) s.DoubleHipTracker = dht;
                }
                else changed = true;
            }
            catch (Exception) { }
            if (string.IsNullOrEmpty(s.Code) || s.Code.Length < 32) { s.Code = NewCode(); changed = true; }
            if (changed) try { s.Save(folder); } catch (Exception) { }
            return s;
        }

        static string[] Strings(object v)
        {
            var list = v as System.Collections.IEnumerable;
            if (list == null || v is string) return null;
            return list.Cast<object>().Select(o => o as string).ToArray();
        }

        // The spots and pairs from the page (or the file): only her 27 spots, only a device that fits each, each device
        // once (the headset's hand tracking may be on both hands), and each pair two different spots of its own part of
        // the body. Anything else changes nothing and says why.
        public string SetSpots(IDictionary<string, object> spots, object hip, object chest)
        {
            if (spots == null) return "No spots.";
            var worn = new Dictionary<string, string>();
            foreach (var kv in spots)
            {
                string device = kv.Value as string;
                if (!Spots.Known(kv.Key)) return "Not one of the 27 spots: " + kv.Key + ".";
                if (string.IsNullOrEmpty(device)) continue;
                if (!Spots.Fits(kv.Key, device)) return "That device does not go on " + Spots.Name(kv.Key) + ".";
                if (device != Spots.HandTracking && worn.ContainsValue(device)) return "One device is on two spots.";
                worn[kv.Key] = device;
            }
            string[] h = Strings(hip) ?? HipSpots, c = Strings(chest) ?? ChestSpots;
            if (h.Length != 2 || h[0] == h[1] || !h.All(x => Spots.Waist.Contains(x))) return "Double Hip's pair is two different waist spots.";
            if (c.Length != 2 || c[0] == c[1] || !c.All(x => Spots.Chest.Contains(x))) return "Double Chest's pair is two different chest spots.";
            Worn = worn; HipSpots = h; ChestSpots = c;
            return null;
        }

        // What a launch needs that has no device on its spot; null when it has everything.
        public string Missing(LaunchRequest r)
        {
            if (At(Spots.Face).Length > 0 && At(Spots.Face) != r.Headset) return "23 Face [F] has the " + (At(Spots.Face) == "quest-pro" ? "Quest Pro" : "Quest 3") + " on it.";
            if (r.HeadTracker && HeadTracker.Length == 0) return "This mode needs a tracker on " + Spots.Name(Spots.Head) + ".";
            if ((r.Double == "hip" || r.Double == "both") && HipPair.Length == 0) return "Double Hip needs trackers on " + string.Join(" and ", HipSpots.Select(Spots.Name)) + ".";
            if ((r.Double == "chest" || r.Double == "both") && ChestPair.Length == 0) return "Double Chest needs trackers on " + string.Join(" and ", ChestSpots.Select(Spots.Name)) + ".";
            if (!string.IsNullOrEmpty(r.Object) && SerialAt(Spots.ObjectSpot[r.Object]).Length == 0) return "That object needs a tracker on " + Spots.Name(Spots.ObjectSpot[r.Object]) + ".";
            return null;
        }

        static string NewCode()
        {
            var bytes = new byte[24];
            using (var rng = new RNGCryptoServiceProvider()) rng.GetBytes(bytes);
            return FarmEvent.Hex(bytes);
        }

        public void Save(string folder)
        {
            Directory.CreateDirectory(folder);
            var d = new Dictionary<string, object>();
            d["schema"] = "rift-rabbit-vr-launch-settings-v2";
            d["about"] = "Rift Rabbit's Launch buttons. This PC only: never commit or share the code.";
            d["port"] = Port;
            d["code"] = Code;
            d["spots"] = Worn;
            d["double_hip_spots"] = HipSpots;
            d["double_chest_spots"] = ChestSpots;
            d["double_hip_tracker"] = DoubleHipTracker;
            File.WriteAllText(Path.Combine(folder, FileName), FarmEvent.Json().Serialize(d), new UTF8Encoding(false));
        }

        // The trackers VRChat ignores for this launch: the Double choice's, and the head tracker in the modes with one.
        public string[] IgnoreFor(LaunchRequest r)
        {
            var list = new List<string>(Ignore.ContainsKey(r.Double ?? "") ? Ignore[r.Double ?? ""] : new string[0]);
            if (r.HeadTracker && HeadTracker.Length > 0) list.Add(HeadTracker);
            return list.Distinct().ToArray();
        }
    }

    sealed class LaunchStep
    {
        public string Id, Name, Note = "";
        public string State = "waiting";   // waiting | now | you | done | skipped | failed
        public Dictionary<string, object> ToData() { return new Dictionary<string, object> { { "id", Id }, { "name", Name }, { "state", State }, { "note", Note } }; }
    }

    static class VrLaunch
    {
        public const string Tool = "wonderland-launchpad-v1.0-vr-launch-v1.0";
        public const string PageDataName = "vr-launch-page-data.js";
        public const int OpenWaitSeconds = 60;

        // Space Override, Space Sync and QuestLHSync are SteamVR drivers; a mode works only once its driver is registered
        // with SteamVR (Space Override and Space Sync were installed on 2026-10-24; QuestLHSync waits for a rooted headset).
        public static bool Installed(string mode)
        {
            if (mode == "native" || mode == "space-calibrator") return true;
            string want = mode == "space-override" ? "spaceoverride" : mode == "space-sync" ? "spacesync" : "questlhsync";
            foreach (string dir in DriverFolders())
                if (dir.ToLowerInvariant().Replace("-", "").Replace("_", "").Replace(" ", "").Contains(want)) return true;
            return false;
        }

        static IEnumerable<string> DriverFolders()
        {
            var found = new List<string>();
            try
            {
                string paths = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "openvr", "openvrpaths.vrpath");
                var m = File.Exists(paths) ? FarmEvent.Map(FarmEvent.Json().DeserializeObject(File.ReadAllText(paths))) : null;
                var ext = m == null ? null : FarmEvent.Get(m, "external_drivers") as System.Collections.IEnumerable;
                if (ext != null) found.AddRange(ext.Cast<object>().OfType<string>());
                var rt = m == null ? null : FarmEvent.Get(m, "runtime") as System.Collections.IEnumerable;
                if (rt != null)
                    foreach (string r in rt.Cast<object>().OfType<string>())
                        if (Directory.Exists(Path.Combine(r, "drivers"))) found.AddRange(Directory.GetDirectories(Path.Combine(r, "drivers")));
            }
            catch (Exception) { }
            return found;
        }

        // The steps of one launch, in Astra's order (hub #236's launchOrder()).
        public static List<LaunchStep> Plan(LaunchRequest r)
        {
            var steps = new List<LaunchStep>();
            Action<string, string> add = (id, name) => steps.Add(new LaunchStep { Id = id, Name = name });
            string streamer = StreamerProgram(r.Streamer);
            // First, while Steam can still be closed: VRChat's launch options in Steam hold this launch's ignore list.
            add("steam-options", "VRChat's launch options in Steam");
            // Then, before anything can start SteamVR (Virtual Desktop starts it from the headset): only this mode's
            // add-on is on.
            add("addons", "SteamVR add-ons for this mode");
            add("open:" + streamer, VrPrograms.Find(streamer).Name);
            add("headset", "Put on the " + r.Device);
            add("streamer-on-headset", (r.Streamer == "virtual-desktop" ? "Virtual Desktop" : r.Streamer == "steam-link" ? "Steam Link" : "Meta Horizon Link") + " on the " + r.Device);
            add("open:steamvr", "SteamVR");
            if (r.Face) add("open:vrcft", "VRCFaceTracking");
            add("open:ovr-advanced-settings", "OVR Advanced Settings");
            add("open:ovr-dynamic-resolution", "OVR Dynamic Resolution");
            if (r.Mode == "space-calibrator") add("open:space-calibrator", "Space Calibrator");
            else add("close:space-calibrator", "Space Calibrator stays closed");
            if (r.Mode == "space-override") add("open:space-override", "Space Override");
            if (r.Mode == "space-sync") add("open:space-sync", "Space Sync (Follow SLAM HMD mode)");
            if (r.Mode == "questlhsync") add("questlhsync", "QuestLHSync lines up the trackers");
            if (r.Double != "") add("open:double-hip-tracker", "DoubleHip DoubleChest Tracker (" + (r.Double == "hip" ? "Double Hip" : r.Double == "chest" ? "Double Chest" : "Both") + ")");
            if (!string.IsNullOrEmpty(r.Object)) add("object-track", "Object Track VR (" + (r.Object == "small-hoop" ? "Small Hoop" : r.Object == "large-hoop" ? "Large Hoop" : "Dance Pole") + ")");
            add("open:ovr-smooth-tracking", "OVR Smooth Tracking");
            if (r.Fov) add("open:base-station-fov", "Base Station FOV Viewer");
            if (r.Pole) add("open:vr-pole-overlay", "VR Pole Overlay");
            if (r.Osc) add("open:oscgoesbrrr", "OSCGoesBrrr");
            // The Woojer vest (Astra, 2026-10-24: "We also need an option for Woojer vest (similar to OSCGoesBrrr)") has
            // no program of its own: it is a Bluetooth audio device ("Woojer Vest S3") that Voicemeeter plays to.
            if (r.Woojer) add("woojer", "Woojer vest (Voicemeeter)");
            add("wait", "Wait until everything is connected and the trackers look smooth");
            add("vrchat", "VRChat");
            return steps;
        }

        // SteamVR's add-ons (Settings > Startup / Shutdown > Manage Add-ons) for Space Override, Space Sync and QuestLHSync:
        // on only in their own mode. They are off otherwise, so SteamVR started by hand behaves as before they were
        // installed, and no two of them move the Lighthouse devices at once (QuestLHSync: "Two tools correcting the same
        // devices fight each other"). Only their "enable" values in steamvr.vrsettings change; the rest of the file stays
        // as SteamVR wrote it.
        public static readonly string[] AddOns = { "spaceoverride", "spacesync", "questlhsync" };
        public static bool AddOnOn(string driver, string mode) { return (driver == "spaceoverride" && mode == "space-override") || (driver == "spacesync" && mode == "space-sync") || (driver == "questlhsync" && mode == "questlhsync"); }

        public static string SteamVrSettings()
        {
            string steam = Path.GetDirectoryName(SteamExe());
            return Path.Combine(steam, "config", "steamvr.vrsettings");
        }

        // The file's text with each add-on's "enable" set for the mode; an add-on SteamVR has no entry for gets one.
        public static string WithAddOns(string text, string mode)
        {
            foreach (string driver in AddOns)
            {
                string want = AddOnOn(driver, mode) ? "true" : "false";
                var block = new Regex("(\"driver_" + driver + "\"\\s*:\\s*\\{[^{}]*?\"enable\"\\s*:\\s*)(true|false)");
                if (block.IsMatch(text)) text = block.Replace(text, "${1}" + want, 1);
                else
                {
                    int at = text.IndexOf('{');
                    if (at < 0) throw new InvalidDataException("steamvr.vrsettings is not a settings file.");
                    text = text.Substring(0, at + 1) + "\n   \"driver_" + driver + "\" : {\n      \"enable\" : " + want + "\n   }," + text.Substring(at + 1);
                }
            }
            if (FarmEvent.Map(FarmEvent.Json().DeserializeObject(text)) == null) throw new InvalidDataException("steamvr.vrsettings would not read back.");
            return text;
        }

        // Whether the add-ons are already as the mode needs them (true, false; null when not set).
        public static bool AddOnsMatch(string text, string mode)
        {
            var m = FarmEvent.Map(FarmEvent.Json().DeserializeObject(text));
            foreach (string driver in AddOns)
            {
                var d = FarmEvent.Map(FarmEvent.Get(m, "driver_" + driver));
                // SteamVR loads an add-on unless told not to; QuestLHSync has nothing to load until it is installed.
                bool on = d == null ? (driver != "questlhsync" || Installed("questlhsync")) : !Equals(FarmEvent.Get(d, "enable"), false);
                if (on != AddOnOn(driver, mode)) return false;
            }
            return true;
        }

        // Steam Link needs only Steam on the PC; the other two have a program of their own.
        public static string StreamerProgram(string streamer) { return streamer == "steam-link" ? "steam" : streamer; }

        // What must be on before Start VRChat lights up: the headset, the Index controllers on her hands (none for hand
        // tracking), every tracker on a body spot (the head tracker only in the modes with one), the tracker on the object
        // chosen, and every program this launch opened.
        public static List<KeyValuePair<string, string>> NeededDevices(LaunchRequest r, LaunchSettings s)
        {
            var need = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(r.Headset, r.Device) };
            foreach (string hand in new[] { Spots.RightHand, Spots.LeftHand })
                if (s.At(hand).StartsWith("index-controller")) need.Add(new KeyValuePair<string, string>(s.At(hand), hand == Spots.RightHand ? "Right Index controller" : "Left Index controller"));
            foreach (string code in Spots.Table.Select(t => t.Split('|')[0]))
            {
                if (!Spots.Tracker(s.At(code))) continue;
                if (code == Spots.Head && !r.HeadTracker) continue;
                if (Spots.ObjectSpot.ContainsValue(code) && (string.IsNullOrEmpty(r.Object) || Spots.ObjectSpot[r.Object] != code)) continue;
                var row = VrDevices.Table.Select(t => t.Split('|')).First(f => f[0] == s.At(code));
                need.Add(new KeyValuePair<string, string>(row[0], row[1] + " on " + Spots.Name(code)));
            }
            return need;
        }

        // VRChat: a shortcut to Steam's VRChat, opened through explorer.exe like every other program (her rule), so nothing
        // runs from inside this program's process tree. Its trackers to ignore are in its launch options in Steam, which
        // Steam adds by itself; passing them here too would give VRChat the list twice.
        public static string VrChatArguments() { return "-applaunch " + VrChatApp; }

        // VRChat's launch options live in Steam (Properties > General > Launch Options), in
        // Steam\userdata\<account>\config\localconfig.vdf. Astra, 2026-10-25: "the codes here go into the steam vrchat
        // launch properties entry here, that is what this process has to change", and "close Steam, write, reopen":
        // Steam writes that file over when it closes, so the launcher closes Steam first, only when the list must change.
        public const string VrChatApp = "438100";
        static readonly string[] VrChatPath = { "UserLocalConfigStore", "Software", "Valve", "Steam", "apps", VrChatApp };

        // The signed-in account's file (Steam's ActiveUser), else the newest one.
        public static string SteamUserConfig()
        {
            string root = Path.Combine(Path.GetDirectoryName(SteamExe()), "userdata");
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                {
                    object id = key == null ? null : key.GetValue("ActiveUser");
                    if (id is int && (int)id != 0)
                    {
                        string f = Path.Combine(root, unchecked((uint)(int)id).ToString(), "config", "localconfig.vdf");
                        if (File.Exists(f)) return f;
                    }
                }
            }
            catch (Exception) { }
            if (!Directory.Exists(root)) return null;
            return Directory.GetDirectories(root).Select(d => Path.Combine(d, "config", "localconfig.vdf")).Where(File.Exists)
                .OrderByDescending(f => File.GetLastWriteTimeUtc(f)).FirstOrDefault();
        }

        // Steam's text settings format: "key" "value" pairs and "key" { ... } blocks, with \" and \\ inside strings.
        sealed class VdfToken { public int Start, End; public char Kind; public string Text; }

        static List<VdfToken> VdfTokens(string t)
        {
            var list = new List<VdfToken>();
            for (int i = 0; i < t.Length; i++)
            {
                char c = t[i];
                if (c == '{' || c == '}') list.Add(new VdfToken { Start = i, End = i + 1, Kind = c });
                else if (c == '/' && i + 1 < t.Length && t[i + 1] == '/') { while (i < t.Length && t[i] != '\n') i++; }
                else if (c == '"')
                {
                    var sb = new StringBuilder();
                    int j = i + 1;
                    for (; j < t.Length && t[j] != '"'; j++)
                    {
                        if (t[j] == '\\' && j + 1 < t.Length) { j++; sb.Append(t[j] == 'n' ? '\n' : t[j] == 't' ? '\t' : t[j]); }
                        else sb.Append(t[j]);
                    }
                    if (j >= t.Length) throw new InvalidDataException("Steam's settings file ends inside a string.");
                    list.Add(new VdfToken { Start = i, End = j + 1, Kind = '"', Text = sb.ToString() });
                    i = j;
                }
            }
            return list;
        }

        // VRChat's block: the token indexes of its { and its }; -1, -1 when Steam has none.
        static int[] AppBlock(List<VdfToken> tk)
        {
            var path = new List<string>();
            var opens = new List<int>();
            int open = -1;
            for (int i = 0; i < tk.Count; i++)
            {
                if (tk[i].Kind == '"')
                {
                    if (i + 1 >= tk.Count || tk[i + 1].Kind == '}') throw new InvalidDataException("Steam's settings file has a name without a value.");
                    i++;
                    if (tk[i].Kind != '{') continue;   // a "key" "value" pair
                    path.Add(tk[i - 1].Text); opens.Add(i);
                    if (open < 0 && path.Count == VrChatPath.Length && path.Select((p, n) => string.Equals(p, VrChatPath[n], StringComparison.OrdinalIgnoreCase)).All(x => x)) open = i;
                }
                else if (tk[i].Kind == '}')
                {
                    if (path.Count == 0) throw new InvalidDataException("Steam's settings file has an extra }.");
                    if (opens[opens.Count - 1] == open) return new[] { open, i };
                    path.RemoveAt(path.Count - 1); opens.RemoveAt(opens.Count - 1);
                }
                else throw new InvalidDataException("Steam's settings file has a { without a name.");
            }
            if (open >= 0) throw new InvalidDataException("Steam's settings file ends inside VRChat's block.");
            return new[] { -1, -1 };
        }

        // The token index of the LaunchOptions value directly in VRChat's block, or -1.
        static int LaunchOptionsValue(List<VdfToken> tk, int[] block)
        {
            int depth = 0;
            for (int i = block[0] + 1; i < block[1]; i++)
            {
                if (tk[i].Kind == '{') depth++;
                else if (tk[i].Kind == '}') depth--;
                else if (depth == 0 && tk[i + 1].Kind == '"')
                {
                    if (string.Equals(tk[i].Text, "LaunchOptions", StringComparison.OrdinalIgnoreCase)) return i + 1;
                    i++;
                }
            }
            return -1;
        }

        // VRChat's launch options as Steam has them: "" when the box is empty, null when Steam has no settings for VRChat.
        public static string LaunchOptions(string vdf)
        {
            var tk = VdfTokens(vdf);
            var b = AppBlock(tk);
            if (b[0] < 0) return null;
            int v = LaunchOptionsValue(tk, b);
            return v < 0 ? "" : tk[v].Text;
        }

        // The file's text with VRChat's launch options set; every other byte stays as Steam wrote it.
        public static string WithLaunchOptions(string vdf, string options)
        {
            var tk = VdfTokens(vdf);
            var b = AppBlock(tk);
            if (b[0] < 0) throw new InvalidDataException("Steam has no settings for VRChat yet: start VRChat once from Steam.");
            string quoted = "\"" + options.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            int v = LaunchOptionsValue(tk, b);
            string text;
            if (v >= 0) text = vdf.Substring(0, tk[v].Start) + quoted + vdf.Substring(tk[v].End);
            else
            {
                // A new line just above VRChat's }, one tab deeper than it.
                int close = tk[b[1]].Start, lineStart = vdf.LastIndexOf('\n', Math.Max(0, close - 1)) + 1;
                string indent = vdf.Substring(lineStart, close - lineStart);
                if (indent.Trim().Length > 0) { lineStart = close; indent = ""; }
                text = vdf.Substring(0, lineStart) + indent + "\t\"LaunchOptions\"\t\t" + quoted + "\n" + vdf.Substring(lineStart);
            }
            if (LaunchOptions(text) != options) throw new InvalidDataException("VRChat's launch options would not read back.");
            return text;
        }

        // Her other launch options stay; only the --ignore-trackers list is the launcher's.
        static readonly Regex IgnoreOption = new Regex(@"(^|\s)--ignore-trackers=\S*");
        public static string WithIgnoreTrackers(string options, string[] ignore)
        {
            string rest = IgnoreOption.Replace(options ?? "", "").Trim();
            string list = ignore.Length > 0 ? "--ignore-trackers=" + string.Join(",", ignore) : "";
            return rest.Length == 0 ? list : list.Length == 0 ? rest : rest + " " + list;
        }

        // "[W] [C] [H]": her stickers for the serials, from the device table.
        public static string Stickers(string[] serials)
        {
            return string.Join(" ", serials.Select(x =>
            {
                var row = VrDevices.Table.Select(t => t.Split('|')).FirstOrDefault(f => f[2] == x);
                var m = row == null ? null : Regex.Match(row[1], @"\[[^\]]*\]$");
                return m != null && m.Success ? m.Value : x;
            }));
        }

        public static string SteamExe()
        {
            string steam = null;
            try { using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")) steam = key == null ? null : key.GetValue("SteamExe") as string; }
            catch (Exception) { }
            if (string.IsNullOrEmpty(steam)) steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam", "steam.exe");
            return steam.Replace('/', '\\');
        }

        public static void MakeShortcut(string lnk, string target, string arguments)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lnk));
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object s = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnk });
                Type st = s.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, s, new object[] { target });
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, s, new object[] { arguments });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, s, new object[] { Path.GetDirectoryName(target) });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, s, null);
                Release(s);
            }
            finally { Release(shell); }
        }

        static void Release(object o) { try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(o); } catch (Exception) { } }

        // DoubleHip DoubleChest Tracker's settings for the choice: Enable Hip / Enable Chest, and the pairs once Astra has said which.
        // Its own settings are kept once, the first time, beside it. Everything else stays as she set it in the app.
        public static string SetDoubleHip(string configFile, string choice, LaunchSettings s)
        {
            if (!File.Exists(configFile)) return "Its settings file was not found, so it opens as it is.";
            string backup = Path.Combine(Path.GetDirectoryName(configFile), "user_config.before-rift-rabbit-20261024.json");
            if (!File.Exists(backup)) File.Copy(configFile, backup);
            var m = FarmEvent.Map(FarmEvent.Json().DeserializeObject(File.ReadAllText(configFile)));
            if (m == null) return "Its settings file could not be read, so it opens as it is.";
            m["useHip"] = choice == "hip" || choice == "both";
            m["useChest"] = choice == "chest" || choice == "both";
            string pairs = "its pairs left as they are";
            if (s.HipPair.Length == 2) { m["useHipAutomatic"] = false; m["extraSerialA"] = s.HipPair[0]; m["extraSerialB"] = s.HipPair[1]; }
            if (s.ChestPair.Length == 2) { m["chestSerialA"] = s.ChestPair[0]; m["chestSerialB"] = s.ChestPair[1]; }
            if (s.HipPair.Length == 2 || s.ChestPair.Length == 2) pairs = "its pairs from the launcher's settings";
            string tmp = configFile + ".tmp";
            File.WriteAllText(tmp, FarmEvent.Json().Serialize(m), new UTF8Encoding(false));
            File.Replace(tmp, configFile, null);
            return "Hip " + ((bool)m["useHip"] ? "on" : "off") + ", Chest " + ((bool)m["useChest"] ? "on" : "off") + ", " + pairs + ".";
        }

        // The overlays the page can switch on and off at any moment.
        public static readonly string[] Overlays = { "base-station-fov", "vr-pole-overlay" };

        public static string OpenNow(VrProgram w)
        {
            if (VrPrograms.Look(w).Running) return w.Name + " is already open.";
            if (!VrPrograms.OpensSafely(w.Open)) return w.Name + " has no safe way to open.";
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + w.Open + "\"") { UseShellExecute = false })) { }
            return w.Name + " is opening.";
        }

        public static string PageDataFile() { return Path.Combine(LaunchSettings.Folder(), PageDataName); }

        // What the page needs to reach the launcher: its address, the code, and which modes can run. In Documents, so only a
        // page opened from this PC can read it.
        public static void WritePageData(string file, LaunchSettings s, bool on)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            var d = new Dictionary<string, object>();
            d["schema"] = "rift-rabbit-vr-launch-v1";
            d["tool"] = Tool;
            d["on"] = on;
            d["url"] = "http://127.0.0.1:" + s.Port + "/";
            d["code"] = on ? s.Code : "";
            d["time"] = FarmEvent.Now(DateTimeOffset.Now);
            d["installed"] = LaunchRequest.Modes.ToDictionary(m => m, m => (object)Installed(m));
            foreach (var kv in LaunchServer.SpotsData(s)) d[kv.Key] = kv.Value;
            string json = FarmEvent.Json().Serialize(d).Replace("<", "\\u003c");
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, "window.riftRabbitLaunch = " + json + ";\n", new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(tmp, file, null); else File.Move(tmp, file);
        }
    }

    // One launch at a time, run on its own thread. The page reads its steps every second.
    sealed class LaunchRun
    {
        readonly RabbitState rabbit;
        readonly LaunchSettings settings;
        public readonly LaunchRequest Request;
        public readonly List<LaunchStep> Steps;
        readonly object gate = new object();
        volatile bool stop, next, go;
        public string State = "running";   // running | ready | done | stopped | failed
        public string Said = "";
        public List<Dictionary<string, object>> Checks = new List<Dictionary<string, object>>();
        readonly List<string> opened = new List<string>();
        public readonly DateTimeOffset Started = DateTimeOffset.Now;

        public LaunchRun(RabbitState rabbit, LaunchSettings settings, LaunchRequest r)
        {
            this.rabbit = rabbit; this.settings = settings; Request = r; Steps = VrLaunch.Plan(r);
        }

        public void Start() { var t = new Thread(Run) { IsBackground = true, Name = "vr-launch" }; t.Start(); }
        public void Stop() { stop = true; }
        public void Next() { next = true; }
        public bool Go()
        {
            lock (gate) { if (State != "ready") return false; go = true; return true; }
        }

        public Dictionary<string, object> ToData()
        {
            lock (gate)
            {
                return new Dictionary<string, object> {
                    { "state", State }, { "said", Said }, { "headset", Request.Headset }, { "mode", Request.Mode },
                    { "started", FarmEvent.Now(Started) },
                    { "steps", Steps.Select(s => (object)s.ToData()).ToList() },
                    { "checks", Checks.Cast<object>().ToList() } };
            }
        }

        void Set(LaunchStep s, string state, string note) { lock (gate) { s.State = state; if (note != null) s.Note = note; } }
        void Say(string state, string said) { lock (gate) { State = state; Said = said; } }

        void Wait(int ms) { for (int i = 0; i < ms / 100 && !stop; i++) Thread.Sleep(100); }

        static bool On(string deviceId) { return VrDevices.Look(VrDevices.LogFile()).Any(d => d.Id == deviceId && d.On); }
        static bool Running(string programId) { return VrPrograms.Look(VrPrograms.Find(programId)).Running; }

        string Open(string id)
        {
            var w = VrPrograms.Find(id);
            if (id == "double-hip-tracker" && File.Exists(settings.DoubleHipTracker)) w = new VrProgram { Id = w.Id, Name = w.Name, Exes = w.Exes, PathHas = w.PathHas, Open = settings.DoubleHipTracker };
            if (!VrPrograms.OpensSafely(w.Open)) return w.Name + " has no safe way to open.";
            opened.Add(id);
            if (VrPrograms.Look(w).Running) return "Already open.";
            using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + w.Open + "\"") { UseShellExecute = false })) { }
            var until = DateTime.Now.AddSeconds(VrLaunch.OpenWaitSeconds);
            while (DateTime.Now < until && !stop)
            {
                Wait(500);
                if (VrPrograms.Look(w).Running) return "Open.";
            }
            return null;
        }

        void Run()
        {
            rabbit.Event(VrLaunch.Tool, "launch-started", "info", "Launch: " + Request.Device + ", " + Request.Mode, new Dictionary<string, object> { { "headset", Request.Headset }, { "mode", Request.Mode }, { "streamer", Request.Streamer }, { "double", Request.Double }, { "osc", Request.Osc } });
            try
            {
                foreach (var s in Steps)
                {
                    if (stop) break;
                    Set(s, "now", null);
                    if (s.Id == "steam-options")
                    {
                        string[] ignore = settings.IgnoreFor(Request);
                        string which = ignore.Length == 0 ? "No trackers ignored" : "Ignoring " + VrLaunch.Stickers(ignore);
                        string file = VrLaunch.SteamUserConfig();
                        string now = file == null ? null : VrLaunch.LaunchOptions(File.ReadAllText(file));
                        if (now == null) { Set(s, "failed", file == null ? "Steam's settings for your account were not found." : "Steam has no settings for VRChat yet: start VRChat once from Steam."); stop = true; continue; }
                        if (now == VrLaunch.WithIgnoreTrackers(now, ignore)) { Set(s, "done", which + " (already set)."); continue; }
                        if (Running("steamvr") || Running("vrchat"))
                        {
                            // Closing Steam would close them too.
                            Set(s, "failed", "Steam has to close to change VRChat's launch options, and SteamVR or VRChat is open. Close them, then press Launch again (" + which + ").");
                            stop = true;
                            continue;
                        }
                        bool wasOpen = Running("steam");
                        if (wasOpen)
                        {
                            Set(s, "now", "Closing Steam to change VRChat's launch options.");
                            using (Process.Start(new ProcessStartInfo("explorer.exe", "steam://exit") { UseShellExecute = false })) { }
                            var until = DateTime.Now.AddSeconds(VrLaunch.OpenWaitSeconds);
                            while (DateTime.Now < until && !stop && Running("steam")) Wait(1000);
                            if (Running("steam")) { Set(s, "failed", "Steam did not close within " + VrLaunch.OpenWaitSeconds + " seconds, so VRChat's launch options were not changed."); stop = true; continue; }
                        }
                        string text = File.ReadAllText(file);   // as Steam left it when it closed
                        string backup = Path.Combine(LaunchSettings.Folder(), "steam-localconfig-before-rift-rabbit-20261025.vdf");
                        if (!File.Exists(backup)) File.Copy(file, backup);
                        string tmp = file + ".rift-rabbit.tmp";
                        File.WriteAllText(tmp, VrLaunch.WithLaunchOptions(text, VrLaunch.WithIgnoreTrackers(VrLaunch.LaunchOptions(text), ignore)), new UTF8Encoding(false));
                        File.Replace(tmp, file, null);
                        string back = wasOpen ? " Steam: " + (Open("steam") ?? "it did not start again within " + VrLaunch.OpenWaitSeconds + " seconds.") : "";
                        Set(s, "done", which + "." + back);
                        continue;
                    }
                    if (s.Id == "addons")
                    {
                        string file = VrLaunch.SteamVrSettings();
                        string text = File.Exists(file) ? File.ReadAllText(file) : null;
                        string which = Request.Mode == "space-override" ? "Space Override on, Space Sync and QuestLHSync off" : Request.Mode == "space-sync" ? "Space Sync on, Space Override and QuestLHSync off" :
                            Request.Mode == "questlhsync" ? "QuestLHSync on, Space Override and Space Sync off" : "Space Override, Space Sync and QuestLHSync off";
                        if (text != null && VrLaunch.AddOnsMatch(text, Request.Mode)) { Set(s, "done", which + "."); continue; }
                        if (Running("steamvr"))
                        {
                            // SteamVR reads its add-ons when it starts, so a running one cannot be changed under it.
                            Set(s, "failed", "SteamVR is already open with the other add-ons. Close SteamVR, then press Launch again (" + which + ").");
                            stop = true;
                            continue;
                        }
                        if (text == null) { Set(s, "failed", "SteamVR's settings file was not found."); stop = true; continue; }
                        string backup = Path.Combine(LaunchSettings.Folder(), "steamvr-before-rift-rabbit-20261024.vrsettings");
                        if (!File.Exists(backup)) File.Copy(file, backup);
                        File.WriteAllText(file, VrLaunch.WithAddOns(text, Request.Mode), new UTF8Encoding(false));
                        Set(s, "done", which + ".");
                        continue;
                    }
                    if (s.Id == "headset" || s.Id == "streamer-on-headset")
                    {
                        // Her side: the headset and its streamer. Done when SteamVR sees the headset, or when she presses
                        // Next because her streamer does not start SteamVR by itself.
                        if (s.Id == "headset") Set(s, "you", "Put it on and wake it up.");
                        else Set(s, "you", "Open it on the headset and connect. When SteamVR has not started by itself, press Next.");
                        while (!stop && !next && !On(Request.Headset)) Wait(1000);
                        if (s.Id == "streamer-on-headset") next = false;
                        if (!stop) Set(s, "done", "");
                        continue;
                    }
                    if (s.Id == "open:steamvr")
                    {
                        if (!Running("steamvr")) Open("steamvr"); else opened.Add("steamvr");
                        Set(s, "now", "Waiting for SteamVR to see the " + Request.Device + ".");
                        while (!stop && !On(Request.Headset)) Wait(1000);
                        if (!stop) Set(s, "done", "It sees the " + Request.Device + ".");
                        continue;
                    }
                    if (s.Id == "close:space-calibrator")
                    {
                        if (!Running("space-calibrator")) { Set(s, "skipped", "Not open."); continue; }
                        string said = VrPrograms.Close(rabbit, VrPrograms.Find("space-calibrator"), 10);
                        Set(s, said.EndsWith(" closed.") ? "done" : "failed", said);
                        continue;
                    }
                    if (s.Id == "open:double-hip-tracker")
                    {
                        string note = "";
                        if (Running("double-hip-tracker")) note = "It was already open, so its settings were left as they are. ";
                        else
                        {
                            try { note = VrLaunch.SetDoubleHip(Path.Combine(Path.GetDirectoryName(settings.DoubleHipTracker), "user_config.json"), Request.Double, settings) + " "; }
                            catch (Exception e) { note = "Its settings could not be set (" + e.Message + "). "; }
                        }
                        string r = Open("double-hip-tracker");
                        Set(s, r == null ? "failed" : "done", note + (r ?? "It did not start within " + VrLaunch.OpenWaitSeconds + " seconds."));
                        continue;
                    }
                    if (s.Id == "questlhsync")
                    {
                        // It runs inside SteamVR; her side is to look at each base station once so the cameras find them.
                        Set(s, "done", "It runs inside SteamVR. Look at each base station until its page in the SteamVR dashboard says it is locked (about half a minute the first time).");
                        continue;
                    }
                    if (s.Id == "object-track")
                    {
                        // Not out yet (Steam lists it as coming soon): nothing to open, so the step says so and the
                        // launch goes on. It gets a row in program-watch once it is installed.
                        Set(s, "skipped", "Not installed yet: Object Track VR is not out. Nothing was opened.");
                        continue;
                    }
                    if (s.Id == "woojer")
                    {
                        // Voicemeeter open is all the PC can do; whether the vest is on and connected only she can see.
                        string r = Open("voicemeeter");
                        Set(s, r == null ? "failed" : "done", r == null ? "Voicemeeter did not start within " + VrLaunch.OpenWaitSeconds + " seconds." :
                            "Voicemeeter: " + r + " Turn the vest on: once Windows has it connected (Woojer Vest S3, Bluetooth), Voicemeeter plays to it.");
                        continue;
                    }
                    if (s.Id.StartsWith("open:"))
                    {
                        string r = Open(s.Id.Substring(5));
                        Set(s, r == null ? "failed" : "done", r ?? "It did not start within " + VrLaunch.OpenWaitSeconds + " seconds.");
                        continue;
                    }
                    if (s.Id == "wait")
                    {
                        while (!stop && !go)
                        {
                            var devices = VrDevices.Look(VrDevices.LogFile());
                            var progs = VrPrograms.Look(opened.Distinct().Select(id => VrPrograms.Find(id)));
                            var checks = VrLaunch.NeededDevices(Request, settings).Select(d => new Dictionary<string, object> { { "id", d.Key }, { "name", d.Value }, { "on", devices.Any(x => x.Id == d.Key && x.On) } })
                                .Concat(progs.Select(p => new Dictionary<string, object> { { "id", p.Program.Id }, { "name", p.Program.Name }, { "on", p.Running } })).ToList();
                            var missing = checks.Where(c => !(bool)c["on"]).Select(c => (string)c["name"]).ToList();
                            lock (gate)
                            {
                                Checks = checks;
                                if (missing.Count == 0) { State = "ready"; s.State = "you"; s.Note = "All green. Press Start VRChat when the trackers look smooth."; }
                                else { State = "running"; s.State = "now"; s.Note = "Waiting for: " + string.Join(", ", missing) + "."; }
                            }
                            Wait(1000);
                        }
                        if (!stop) Set(s, "done", "");
                        continue;
                    }
                    if (s.Id == "vrchat")
                    {
                        Say("running", "");
                        string[] ignore = settings.IgnoreFor(Request);
                        string lnk = Path.Combine(LaunchSettings.Folder(), "vr-launch", "vrchat-20261024.lnk");
                        VrLaunch.MakeShortcut(lnk, VrLaunch.SteamExe(), VrLaunch.VrChatArguments());
                        using (Process.Start(new ProcessStartInfo("explorer.exe", "\"" + lnk + "\"") { UseShellExecute = false })) { }
                        opened.Add("vrchat");
                        var until = DateTime.Now.AddSeconds(90);
                        while (DateTime.Now < until && !stop && !Running("vrchat")) Wait(1000);
                        bool up = Running("vrchat");
                        Set(s, up ? "done" : "failed", (up ? "Open" : "It did not start within 90 seconds") + (ignore.Length > 0 ? ", ignoring " + ignore.Length + " tracker" + (ignore.Length == 1 ? "" : "s") + " (its launch options in Steam)." : "."));
                        continue;
                    }
                }
                if (stop)
                {
                    lock (gate) foreach (var s in Steps) if (s.State == "now" || s.State == "you" || s.State == "waiting") s.State = "skipped";
                    var failed = Steps.FirstOrDefault(x => x.State == "failed");
                    if (failed != null) Say("failed", failed.Note);
                    else Say("stopped", "Stopped. Programs that were open stay open.");
                }
                else Say(Steps.Any(x => x.State == "failed") ? "failed" : "done", Steps.Any(x => x.State == "failed") ? "Done, but a step did not work: see it in the list." : "VRChat is starting. Have fun.");
            }
            catch (Exception e) { Say("failed", "The launch stopped: " + e.Message); }
            rabbit.Event(VrLaunch.Tool, "launch-" + State, State == "failed" ? "warning" : "info", "Launch " + State + ": " + Said, null);
        }
    }

    // Where each dongle is plugged in (Astra, 2026-10-26: "Can you add USBDeview ... and then also make a tool for us to run
    // it and show a branch where each dongle is? There are so many ways to plug them in, we want to optimize it").
    // NirSoft's USBDeview (Documents\USBDeview-3.10) lists what is plugged in; Windows' own device tree gives each one's
    // hub, root hub and USB controller, so the page can draw them as branches. SteamVR's log says which tracker a dongle
    // carries when it has seen them together. Read only: nothing is changed.
    static class UsbMap
    {
        public static string UsbDeview() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "USBDeview-3.10", "USBDeview.exe"); }

        [System.Runtime.InteropServices.DllImport("cfgmgr32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int CM_Locate_DevNodeW(out uint devInst, string deviceId, int flags);
        [System.Runtime.InteropServices.DllImport("cfgmgr32.dll")]
        static extern int CM_Get_Parent(out uint parent, uint devInst, int flags);
        [System.Runtime.InteropServices.DllImport("cfgmgr32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int CM_Get_Device_IDW(uint devInst, StringBuilder buffer, int length, int flags);
        [System.Runtime.InteropServices.DllImport("cfgmgr32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        static extern int CM_Get_DevNode_Registry_PropertyW(uint devInst, int property, out int type, StringBuilder buffer, ref int length, int flags);

        const int DeviceDesc = 0x01, FriendlyName = 0x0D, LocationInformation = 0x0E;

        static string Prop(uint node, int property)
        {
            var sb = new StringBuilder(512); int length = sb.Capacity * 2, type;
            return CM_Get_DevNode_Registry_PropertyW(node, property, out type, sb, ref length, 0) == 0 ? sb.ToString() : "";
        }
        static string Id(uint node) { var sb = new StringBuilder(512); return CM_Get_Device_IDW(node, sb, sb.Capacity, 0) == 0 ? sb.ToString() : ""; }
        static string Name(uint node) { string n = Prop(node, FriendlyName); return n.Length > 0 ? n : Prop(node, DeviceDesc); }

        // What a device is, from its USB ids and name.
        public static string Kind(string id, string name)
        {
            string u = (id ?? "").ToUpperInvariant();
            if (u.Contains("VID_28DE&PID_2101")) return "tracker-dongle";     // Watchman Dongle: one tracker or controller each
            if (u.Contains("VID_28DE&PID_2102")) return "radio";              // Valve VR Radio
            if (u.Contains("VID_28DE")) return "valve";
            if (u.Contains("VID_0BB4")) return "htc";
            if (u.StartsWith("PCI\\")) return "controller";
            if (u.Contains("ROOT_HUB")) return "root-hub";
            if ((name ?? "").IndexOf("hub", StringComparison.OrdinalIgnoreCase) >= 0) return "hub";
            return "device";
        }

        sealed class Node { public string Id, Name, Kind, Location, Serial, Tracker; public List<Node> Children = new List<Node>(); }

        static Dictionary<string, object> Data(Node n)
        {
            var d = new Dictionary<string, object> { { "name", n.Name }, { "kind", n.Kind }, { "location", n.Location } };
            if (!string.IsNullOrEmpty(n.Serial)) d["serial"] = n.Serial;
            if (!string.IsNullOrEmpty(n.Tracker)) d["tracker"] = n.Tracker;
            if (n.Children.Count > 0) d["children"] = n.Children.OrderBy(c => c.Location).ThenBy(c => c.Name).Select(c => (object)Data(c)).ToList();
            return d;
        }

        // Which tracker each dongle carries, when SteamVR's log names a dongle serial and a tracker serial on one line.
        public static Dictionary<string, string> Pairings(IEnumerable<string> lines, IEnumerable<string> dongles)
        {
            var found = new Dictionary<string, string>();
            var serial = new Regex(@"\b(LHR-[0-9A-F]{8})\b");
            foreach (string line in lines)
            {
                var m = serial.Match(line);
                if (!m.Success) continue;
                foreach (string d in dongles) if (line.Contains(d)) found[d] = m.Groups[1].Value;
            }
            return found;
        }

        // The connected devices from USBDeview's XML export: instance id, name, serial.
        public static List<string[]> Devices(string xml)
        {
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);
            var list = new List<string[]>();
            foreach (System.Xml.XmlNode item in doc.SelectNodes("//item"))
            {
                Func<string, string> f = k => { var n = item.SelectSingleNode(k); return n == null ? "" : n.InnerText.Trim(); };
                // Whole devices only: a device's interfaces (MI_xx) and HID collections draw no branch of their own.
                if (f("connected") != "Yes" || f("instance_id").Length == 0 || f("instance_id").Contains("&MI_") || !f("instance_id").StartsWith("USB\\", StringComparison.OrdinalIgnoreCase)) continue;
                list.Add(new[] { f("instance_id"), f("device_name").Length > 0 ? f("device_name") : f("description"), f("serial_number") });
            }
            return list;
        }

        public static Dictionary<string, object> Scan(string exe, string logFolder)
        {
            if (!File.Exists(exe)) return new Dictionary<string, object> { { "ok", false }, { "said", "USBDeview is not in Documents\\USBDeview-3.10." } };
            string xmlFile = Path.Combine(Path.GetTempPath(), "rift-rabbit-usb-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
            try
            {
                using (var p = Process.Start(new ProcessStartInfo(exe, "/DisplayDisconnected 0 /sxml \"" + xmlFile + "\"") { UseShellExecute = false, CreateNoWindow = true }))
                    if (!p.WaitForExit(30000)) return new Dictionary<string, object> { { "ok", false }, { "said", "USBDeview did not finish within 30 seconds." } };
                var devices = Devices(File.ReadAllText(xmlFile));
                var nodes = new Dictionary<string, Node>(StringComparer.OrdinalIgnoreCase);
                var roots = new List<Node>();
                foreach (var dev in devices)
                {
                    uint dn;
                    if (CM_Locate_DevNodeW(out dn, dev[0], 0) != 0) continue;
                    Node child = null;
                    // Walk up from the device to its USB controller, joining branches already seen.
                    for (int depth = 0; depth < 12; depth++)
                    {
                        string id = Id(dn);
                        if (id.Length == 0) break;
                        bool known = nodes.ContainsKey(id);
                        Node n = known ? nodes[id] : new Node { Id = id, Name = Name(dn), Kind = Kind(id, Name(dn)), Location = Prop(dn, LocationInformation) };
                        if (depth == 0 && dev[1].Length > 0 && !dev[1].StartsWith("Port_#")) n.Name = dev[1];
                        if (depth == 0 && dev[2].Length > 0) n.Serial = dev[2];
                        if (child != null && !n.Children.Contains(child)) n.Children.Add(child);
                        if (known) { child = null; break; }
                        nodes[id] = n;
                        if (n.Kind == "controller") { roots.Add(n); child = null; break; }
                        child = n;
                        uint parent;
                        if (CM_Get_Parent(out parent, dn, 0) != 0) break;
                        dn = parent;
                    }
                }
                var dongles = nodes.Values.Where(n => n.Kind == "tracker-dongle" && !string.IsNullOrEmpty(n.Serial)).ToList();
                var lines = new List<string>();
                foreach (string f in new[] { "vrserver.previous.txt", "vrserver.txt" })
                    try { string path = Path.Combine(logFolder, f); if (File.Exists(path)) lines.AddRange(File.ReadAllLines(path)); } catch (Exception) { }
                var paired = Pairings(lines, dongles.Select(n => n.Serial));
                foreach (var n in dongles) { string t; if (paired.TryGetValue(n.Serial, out t)) n.Tracker = t; }
                return new Dictionary<string, object> {
                    { "ok", true }, { "time", FarmEvent.Now(DateTimeOffset.Now) }, { "dongles", dongles.Count },
                    { "controllers", roots.OrderBy(r => r.Name).Select(r => (object)Data(r)).ToList() } };
            }
            catch (Exception e) { return new Dictionary<string, object> { { "ok", false }, { "said", "The scan stopped: " + e.Message } }; }
            finally { try { File.Delete(xmlFile); } catch (Exception) { } }
        }
    }

    // The page's way in: plain HTTP on 127.0.0.1 only (a loopback socket, so nothing off this PC can reach it), and every
    // request carries the code from the launcher's settings, which only a page opened from this PC can read. Requests
    // from a web site (an Origin that is not a file or this PC) are refused before anything else.
    sealed class LaunchServer : IDisposable
    {
        readonly RabbitState rabbit;
        readonly TcpListener listener;
        readonly Thread thread;
        volatile bool closing;
        LaunchRun run;
        public readonly LaunchSettings Settings;
        // Tests: what a launch would do, without starting anything; and what changed spots do, without writing her files.
        public Func<LaunchRequest, LaunchRun> MakeRun;
        public Action Saved;

        public LaunchServer(RabbitState rabbit, LaunchSettings settings)
        {
            this.rabbit = rabbit; Settings = settings;
            MakeRun = r => { var x = new LaunchRun(rabbit, settings, r); x.Start(); return x; };
            Saved = () => { settings.Save(LaunchSettings.Folder()); VrLaunch.WritePageData(VrLaunch.PageDataFile(), settings, true); };
            listener = new TcpListener(IPAddress.Loopback, settings.Port);
            listener.Start();
            thread = new Thread(Accept) { IsBackground = true, Name = "vr-launch-server" };
            thread.Start();
        }

        public int Port { get { return ((IPEndPoint)listener.LocalEndpoint).Port; } }

        public static bool OriginOk(string origin)
        {
            if (string.IsNullOrEmpty(origin) || origin == "null") return true;
            return Regex.IsMatch(origin, @"^http://(127\.0\.0\.1|localhost|\[::1\])(:\d{1,5})?$");
        }

        public static bool SameCode(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        void Accept()
        {
            while (!closing)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); } catch (Exception) { if (closing) return; continue; }
                ThreadPool.QueueUserWorkItem(delegate { try { using (c) Serve(c); } catch (Exception) { } });
            }
        }

        void Serve(TcpClient c)
        {
            c.ReceiveTimeout = 5000; c.SendTimeout = 5000;
            var stream = c.GetStream();
            var head = new StringBuilder();
            var buf = new List<byte>();
            int b;
            // Headers, up to a blank line; 8 KB at most.
            while ((b = stream.ReadByte()) >= 0 && buf.Count < 8192)
            {
                buf.Add((byte)b);
                int n = buf.Count;
                if (n >= 4 && buf[n - 4] == 13 && buf[n - 3] == 10 && buf[n - 2] == 13 && buf[n - 1] == 10) break;
            }
            string[] lines = Encoding.ASCII.GetString(buf.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return;
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string l in lines.Skip(1)) { int i = l.IndexOf(':'); if (i > 0) headers[l.Substring(0, i).Trim()] = l.Substring(i + 1).Trim(); }
            string origin; headers.TryGetValue("Origin", out origin);
            string method = first[0], path = first[1];
            // The body is read before any answer, so a refusal is not cut off by a reset connection.
            string lenText; int len = 0;
            if (headers.TryGetValue("Content-Length", out lenText)) int.TryParse(lenText, out len);
            if (len < 0 || len > 4096) { Reply(stream, 413, null, Error("Too big.")); return; }
            var body = new byte[len];
            int got = 0;
            while (got < len) { int r = stream.Read(body, got, len - got); if (r <= 0) break; got += r; }
            if (!OriginOk(origin)) { Reply(stream, 403, null, Error("Only a page opened from this PC can use the launcher.")); return; }
            if (method == "OPTIONS") { Reply(stream, 204, origin, null); return; }
            if (method != "POST") { Reply(stream, 405, origin, Error("POST only.")); return; }
            IDictionary<string, object> req = null;
            try { req = FarmEvent.Map(FarmEvent.Json().DeserializeObject(Encoding.UTF8.GetString(body, 0, got))); } catch (Exception) { }
            if (req == null || !SameCode(FarmEvent.Str(req, "code"), Settings.Code)) { Reply(stream, 401, origin, Error("The launcher's code does not match. Open the page from this PC.")); return; }
            Reply(stream, 200, origin, Handle(path, req));
        }

        public Dictionary<string, object> Handle(string path, IDictionary<string, object> req)
        {
            var current = run;
            switch (path)
            {
                case "/status":
                    return Status(current, null);
                case "/launch":
                    {
                        if (!rabbit.On(VrLaunch.Tool)) return Status(current, VrLaunch.Tool + " is OFF.");
                        if (current != null && (current.State == "running" || current.State == "ready")) return Status(current, "A launch is already running. Stop it first.");
                        string problem;
                        var r = LaunchRequest.From(req, out problem);
                        if (r == null) return Status(current, problem);
                        problem = Settings.Missing(r);
                        if (problem != null) return Status(current, problem);
                        if (!VrLaunch.Installed(r.Mode)) return Status(current, (r.Mode == "space-override" ? "Space Override" : r.Mode == "space-sync" ? "Space Sync" : "QuestLHSync (it waits for a rooted headset)") + " is not installed.");
                        run = MakeRun(r);
                        return Status(run, null);
                    }
                case "/next":
                    if (current != null) current.Next();
                    return Status(current, null);
                case "/vrchat":
                    return Status(current, current != null && current.Go() ? null : "Not everything is on yet.");
                case "/stop":
                    if (current != null) current.Stop();
                    return Status(current, null);
                case "/overlay":
                    {
                        // The two overlays she switches on or off at any moment, from their ticks on the page: only these two.
                        string id = FarmEvent.Str(req, "id");
                        if (Array.IndexOf(VrLaunch.Overlays, id) < 0) return Error("Not one of the overlays.");
                        var w = VrPrograms.Find(id);
                        bool on = Equals(FarmEvent.Get(req, "on"), true);
                        string said = on ? VrLaunch.OpenNow(w) : VrPrograms.Close(rabbit, w, 10);
                        return new Dictionary<string, object> { { "ok", true }, { "id", id }, { "said", said }, { "running", VrPrograms.Look(w).Running } };
                    }
                case "/usb-map":
                    // tracker-strapper's neighbour on the page: which dongle sits on which hub and USB controller.
                    return UsbMap.Scan(UsbMap.UsbDeview(), Path.GetDirectoryName(VrDevices.LogFile()));
                case "/spots":
                    {
                        // tracker-strapper on the page: which device is on each spot, and the two Double pairs.
                        string problem = current != null && (current.State == "running" || current.State == "ready") ? "A launch is running: change the spots after it." :
                            Settings.SetSpots(FarmEvent.Map(FarmEvent.Get(req, "spots")), FarmEvent.Get(req, "hip"), FarmEvent.Get(req, "chest"));
                        if (problem == null) Saved();
                        var d = SpotsData(Settings);
                        d["ok"] = problem == null;
                        if (problem != null) d["said"] = problem;
                        return d;
                    }
            }
            return Error("Not one of the launcher's requests.");
        }

        static Dictionary<string, object> Status(LaunchRun r, string problem)
        {
            var d = new Dictionary<string, object> { { "schema", "rift-rabbit-vr-launch-status-v1" }, { "ok", problem == null } };
            if (problem != null) d["said"] = problem;
            if (r != null) d["run"] = r.ToData();
            return d;
        }

        // The spots as the page reads them (no code: the page data file carries them too).
        public static Dictionary<string, object> SpotsData(LaunchSettings s)
        {
            return new Dictionary<string, object> { { "spots", s.Worn.ToDictionary(kv => kv.Key, kv => (object)kv.Value) }, { "hip", s.HipSpots }, { "chest", s.ChestSpots } };
        }

        static Dictionary<string, object> Error(string said) { return new Dictionary<string, object> { { "ok", false }, { "said", said } }; }

        static void Reply(NetworkStream stream, int status, string origin, Dictionary<string, object> data)
        {
            byte[] body = data == null ? new byte[0] : Encoding.UTF8.GetBytes(FarmEvent.Json().Serialize(data));
            var h = new StringBuilder();
            h.Append("HTTP/1.1 " + status + " " + (status == 200 ? "OK" : status == 204 ? "No Content" : "Refused") + "\r\n");
            h.Append("Content-Type: application/json; charset=utf-8\r\nCache-Control: no-store\r\nConnection: close\r\n");
            if (origin != null)
            {
                h.Append("Access-Control-Allow-Origin: " + origin + "\r\nVary: Origin\r\n");
                h.Append("Access-Control-Allow-Methods: POST\r\nAccess-Control-Allow-Headers: Content-Type\r\nAccess-Control-Allow-Private-Network: true\r\n");
            }
            h.Append("Content-Length: " + body.Length + "\r\n\r\n");
            byte[] hb = Encoding.ASCII.GetBytes(h.ToString());
            stream.Write(hb, 0, hb.Length);
            stream.Write(body, 0, body.Length);
        }

        public void Dispose()
        {
            closing = true;
            try { listener.Stop(); } catch (Exception) { }
            var r = run;
            if (r != null) r.Stop();
        }
    }

    // The launcher's checks, run by --self-test. Nothing here opens a program: launches go to a stand-in.
    static class VrLaunchSelfTest
    {
        static string Post(int port, string origin, string path, string json)
        {
            using (var c = new TcpClient("127.0.0.1", port))
            {
                var s = c.GetStream();
                byte[] body = Encoding.UTF8.GetBytes(json);
                string head = "POST " + path + " HTTP/1.1\r\nHost: 127.0.0.1\r\n" + (origin != null ? "Origin: " + origin + "\r\n" : "") +
                    "Content-Type: text/plain\r\nContent-Length: " + body.Length + "\r\n\r\n";
                byte[] hb = Encoding.ASCII.GetBytes(head);
                s.Write(hb, 0, hb.Length); s.Write(body, 0, body.Length);
                using (var r = new StreamReader(s, Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        static string Q(string s) { return s.Replace('\'', '"'); }

        public static void Run(Action<string, bool> check)
        {
            string problem;
            Func<string, IDictionary<string, object>> map = j => FarmEvent.Map(FarmEvent.Json().DeserializeObject(Q(j)));
            check("vr-launch: a launch takes only the page's own values",
                LaunchRequest.From(map("{'headset':'quest-pro','mode':'native'}"), out problem) != null &&
                LaunchRequest.From(map("{'headset':'quest-pro','mode':'calc.exe'}"), out problem) == null &&
                LaunchRequest.From(map("{'headset':'quest-pro','mode':'native','double':'--x'}"), out problem) == null);

            var pro = new LaunchRequest { Headset = "quest-pro", Mode = "native", Streamer = "virtual-desktop", Double = "" };
            string ids = string.Join(" ", VrLaunch.Plan(pro).Select(s => s.Id));
            check("vr-launch: Astra's order on the Quest Pro", ids ==
                "steam-options addons open:virtual-desktop headset streamer-on-headset open:steamvr open:vrcft open:ovr-advanced-settings open:ovr-dynamic-resolution close:space-calibrator open:ovr-smooth-tracking wait vrchat");
            var so = VrLaunch.Plan(new LaunchRequest { Headset = "quest-pro", Mode = "space-override", Streamer = "virtual-desktop", Double = "" }).Select(s => s.Id).ToList();
            var ss = VrLaunch.Plan(new LaunchRequest { Headset = "quest-pro", Mode = "space-sync", Streamer = "virtual-desktop", Double = "" }).Select(s => s.Id).ToList();
            check("vr-launch: modes x.2 and x.3 open their own overlay after SteamVR, never the other's",
                so.Contains("open:space-override") && !so.Contains("open:space-sync") && ss.Contains("open:space-sync") && !ss.Contains("open:space-override") &&
                so.IndexOf("open:space-override") > so.IndexOf("open:steamvr"));
            string vdf = "\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t\t\"Valve\"\n\t\t{\n\t\t\t\"Steam\"\n\t\t\t{\n\t\t\t\t\"apps\"\n\t\t\t\t{\n" +
                "\t\t\t\t\t\"250820\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"LaunchOptions\"\t\t\"-keep\"\n\t\t\t\t\t}\n" +
                "\t\t\t\t\t\"438100\"\n\t\t\t\t\t{\n\t\t\t\t\t\t\"cloud\"\n\t\t\t\t\t\t{\n\t\t\t\t\t\t\t\"LaunchOptions\"\t\t\"nested\"\n\t\t\t\t\t\t}\n\t\t\t\t\t\t\"Playtime\"\t\t\"6\"\n\t\t\t\t\t}\n" +
                "\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n\t\"apps\"\n\t{\n\t\t\"438100\"\n\t\t{\n\t\t\t\"LaunchOptions\"\t\t\"wrong place\"\n\t\t}\n\t}\n}\n";
            string added = VrLaunch.WithLaunchOptions(vdf, "--ignore-trackers=LHR-467E1FA2,LHR-6BA0C57A");
            check("vr-launch: an empty launch options box gets a line in VRChat's own block, and nothing else moves",
                VrLaunch.LaunchOptions(vdf) == "" && VrLaunch.LaunchOptions(added) == "--ignore-trackers=LHR-467E1FA2,LHR-6BA0C57A" &&
                added == vdf.Replace("\t\t\t\t\t\t\"Playtime\"\t\t\"6\"\n", "\t\t\t\t\t\t\"Playtime\"\t\t\"6\"\n\t\t\t\t\t\t\"LaunchOptions\"\t\t\"--ignore-trackers=LHR-467E1FA2,LHR-6BA0C57A\"\n"));
            string changed = VrLaunch.WithLaunchOptions(added, "-a \"b\\c\"");
            check("vr-launch: a set launch options box is replaced in place, with Steam's escapes",
                VrLaunch.LaunchOptions(changed) == "-a \"b\\c\"" && changed.Contains("\"LaunchOptions\"\t\t\"-a \\\"b\\\\c\\\"\"") && changed.Contains("\"-keep\"") && changed.Contains("\"nested\"") && changed.Contains("\"wrong place\""));
            check("vr-launch: no settings for VRChat reads as none",
                VrLaunch.LaunchOptions("\"UserLocalConfigStore\"\n{\n\t\"Software\"\n\t{\n\t}\n}\n") == null);
            check("vr-launch: only the --ignore-trackers list is the launcher's; her other launch options stay",
                VrLaunch.WithIgnoreTrackers("--ignore-trackers=LHR-467E1FA2,LHR-6BA0C57A,LHR-28D88802,LHR-91D63DB2", new[] { "LHR-28D88802", "LHR-91D63DB2" }) == "--ignore-trackers=LHR-28D88802,LHR-91D63DB2" &&
                VrLaunch.WithIgnoreTrackers("--fps=144 --ignore-trackers=LHR-467E1FA2 --no-vr", new string[0]) == "--fps=144 --no-vr" &&
                VrLaunch.WithIgnoreTrackers("", new[] { "LHR-6963FCE5" }) == "--ignore-trackers=LHR-6963FCE5" &&
                VrLaunch.Stickers(new[] { "LHR-467E1FA2", "LHR-6963FCE5" }) == "[W] [H]");
            var noFace = LaunchRequest.From(map("{'headset':'quest-pro','mode':'native','face':false,'fov':true,'pole':true}"), out problem);
            var steps = VrLaunch.Plan(noFace).Select(s => s.Id).ToList();
            check("vr-launch: face tracking is a choice on the Quest Pro (on unless the page says off); the two overlays open when ticked",
                LaunchRequest.From(map("{'headset':'quest-pro','mode':'native'}"), out problem).Face && !noFace.Face && !steps.Contains("open:vrcft") &&
                steps.IndexOf("open:base-station-fov") > steps.IndexOf("open:ovr-smooth-tracking") && steps.Contains("open:vr-pole-overlay") &&
                VrLaunch.Overlays.All(o => VrPrograms.Find(o) != null && VrPrograms.OpensSafely(VrPrograms.Find(o).Open)));
            string usbXml = "<?xml version=\"1.0\"?><usb_devices_list><item><device_name>Watchman Dongle</device_name><connected>Yes</connected><serial_number>E2B4C5C9E8</serial_number><instance_id>USB\\VID_28DE&amp;PID_2101\\E2B4C5C9E8</instance_id></item><item><device_name>Old</device_name><connected>No</connected><instance_id>USB\\X</instance_id></item></usb_devices_list>";
            var usb = UsbMap.Devices(usbXml);
            var pair = UsbMap.Pairings(new[] { "lighthouse: E2B4C5C9E8 now carries LHR-467E1FA2", "nothing here" }, new[] { "E2B4C5C9E8", "A745E8B994" });
            check("usb-map: only connected devices; dongles and hubs are told apart; a dongle's tracker comes from SteamVR's log",
                usb.Count == 1 && usb[0][2] == "E2B4C5C9E8" && UsbMap.Kind(usb[0][0], "") == "tracker-dongle" && UsbMap.Kind("USB\\ROOT_HUB30\\5", "") == "root-hub" &&
                UsbMap.Kind("USB\\VID_05E3&PID_0610\\6", "Generic USB Hub") == "hub" && UsbMap.Kind("PCI\\VEN_1022", "") == "controller" &&
                pair.Count == 1 && pair["E2B4C5C9E8"] == "LHR-467E1FA2" && !UsbMap.Scan(@"C:\no\USBDeview.exe", "").ContainsKey("controllers"));
            var lh = VrLaunch.Plan(new LaunchRequest { Headset = "quest-pro", Mode = "questlhsync", Streamer = "virtual-desktop", Double = "" }).Select(s => s.Id).ToList();
            check("vr-launch: x.4 QuestLHSync needs no head tracker, keeps Space Calibrator closed and opens no other overlay",
                lh.Contains("questlhsync") && lh.IndexOf("questlhsync") > lh.IndexOf("open:steamvr") && lh.Contains("close:space-calibrator") && !lh.Contains("open:space-sync") && !lh.Contains("open:space-override") &&
                !new LaunchRequest { Mode = "questlhsync" }.HeadTracker && VrLaunch.AddOnOn("questlhsync", "questlhsync") && !VrLaunch.AddOnOn("questlhsync", "space-sync") && !VrLaunch.AddOnOn("spacesync", "questlhsync"));
            string vrs = "{\n   \"driver_VirtualDesktop\" : {\n      \"disconnect_emulated_trackers\" : true\n   },\n   \"driver_spaceoverride\" : {\n      \"enable\" : false\n   },\n   \"steamvr\" : {\n      \"activateMultipleDrivers\" : true\n   }\n}\n";
            string syncOn = VrLaunch.WithAddOns(vrs, "space-sync");
            check("vr-launch: Space Sync's mode turns only Space Sync on, and leaves the rest of SteamVR's settings as they were",
                VrLaunch.AddOnsMatch(syncOn, "space-sync") && !VrLaunch.AddOnsMatch(syncOn, "native") &&
                syncOn.Contains("\"driver_VirtualDesktop\" : {\n      \"disconnect_emulated_trackers\" : true\n   }") && syncOn.Contains("\"driver_spaceoverride\" : {\n      \"enable\" : false"));
            check("vr-launch: Native turns both add-ons off; an add-on SteamVR has no entry for counts as on",
                VrLaunch.AddOnsMatch(VrLaunch.WithAddOns(syncOn, "native"), "native") && !VrLaunch.AddOnsMatch(vrs, "native"));
            var q3 = new LaunchRequest { Headset = "quest-3", Mode = "space-calibrator", Streamer = "steam-link", Double = "both", Osc = true };
            var q3ids = VrLaunch.Plan(q3).Select(s => s.Id).ToList();
            check("vr-launch: the Quest 3 has no face tracking; Space Calibrator in x.1; Double Hip before OVR Smooth Tracking, OSCGoesBrrr after",
                !q3ids.Contains("open:vrcft") && q3ids.Contains("open:space-calibrator") && q3ids[0] == "steam-options" && q3ids[1] == "addons" && q3ids[2] == "open:steam" &&
                q3ids.IndexOf("open:double-hip-tracker") < q3ids.IndexOf("open:ovr-smooth-tracking") && q3ids.IndexOf("open:oscgoesbrrr") == q3ids.IndexOf("wait") - 1);
            q3.Woojer = true;
            q3ids = VrLaunch.Plan(q3).Select(s => s.Id).ToList();
            check("vr-launch: the Woojer vest comes after OSCGoesBrrr, through Voicemeeter, and only when switched on",
                q3ids.IndexOf("woojer") == q3ids.IndexOf("open:oscgoesbrrr") + 1 && !ids.Contains("woojer") && VrPrograms.Find("voicemeeter") != null &&
                LaunchRequest.From(map("{'headset':'quest-3','mode':'native','woojer':true}"), out problem).Woojer);
            q3.Object = "dance-pole";
            var q3steps = VrLaunch.Plan(q3);
            check("vr-launch: Object Track VR comes after DoubleHip DoubleChest Tracker, names its object, and only its four values pass",
                q3steps.FindIndex(s => s.Id == "object-track") == q3steps.FindIndex(s => s.Id == "open:double-hip-tracker") + 1 &&
                q3steps.First(s => s.Id == "object-track").Name == "Object Track VR (Dance Pole)" && !ids.Contains("object-track") &&
                LaunchRequest.From(map("{'headset':'quest-3','mode':'native','object':'small-hoop'}"), out problem) != null &&
                LaunchRequest.From(map("{'headset':'quest-3','mode':'native','object':'calc'}"), out problem) == null);
            check("vr-launch: every program a plan opens is watched and opens safely", LaunchRequest.Streamers.All(st =>
                VrLaunch.Plan(new LaunchRequest { Headset = "quest-pro", Mode = "space-calibrator", Streamer = st, Double = "hip", Osc = true })
                .Where(s => s.Id.StartsWith("open:") || s.Id.StartsWith("close:")).All(s => { var w = VrPrograms.Find(s.Id.Substring(s.Id.IndexOf(':') + 1)); return w != null && VrPrograms.OpensSafely(w.Open); })));

            string temp = Path.Combine(Path.GetTempPath(), "rift-rabbit-vr-launch-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                var s1 = LaunchSettings.Load(temp);
                check("vr-launch: the settings are made once, with a long code and today's spots", File.Exists(Path.Combine(temp, LaunchSettings.FileName)) &&
                    s1.Code.Length == 48 && LaunchSettings.Load(temp).Code == s1.Code && s1.Worn.Count == 12 && s1.HeadTracker == "LHR-6963FCE5" &&
                    LaunchSettings.Load(temp).HipSpots.SequenceEqual(new[] { "FW", "BW" }));
                check("vr-launch: VRChat ignores both trackers of each pair DoubleHip DoubleChest Tracker joins", string.Join(",", s1.IgnoreFor(new LaunchRequest { Mode = "space-calibrator", Double = "hip" })) == "LHR-467E1FA2,LHR-6BA0C57A,LHR-6963FCE5" &&
                    string.Join(",", s1.IgnoreFor(new LaunchRequest { Mode = "native", Double = "chest" })) == "LHR-28D88802,LHR-91D63DB2" && s1.Ignore["both"].Length == 4 &&
                    s1.IgnoreFor(new LaunchRequest { Mode = "native", Double = "" }).Length == 0);
                File.WriteAllText(Path.Combine(temp, LaunchSettings.FileName), Q("{'code':'" + s1.Code + "','head_tracker':'LHR-6963FCE5','hip_pair':['LHR-467E1FA2','LHR-91D63DB2']}"));
                check("vr-launch: a file from before the spots gets today's spots", LaunchSettings.Load(temp).HipPair.SequenceEqual(new[] { "LHR-467E1FA2", "LHR-6BA0C57A" }) &&
                    File.ReadAllText(Path.Combine(temp, LaunchSettings.FileName)).Contains(Q("'double_hip_spots':['FW','BW']")));
                var s2 = LaunchSettings.Load(temp);
                Func<string, string> set = j => { var m = map(j); return s2.SetSpots(FarmEvent.Map(m["spots"]), FarmEvent.Get(m, "hip"), FarmEvent.Get(m, "chest")); };
                check("vr-launch: only her spots, a device that fits each, each tracker once, pairs from their own part of the body",
                    set("{'spots':{'XX':'vive-tracker-01'}}") != null && set("{'spots':{'F':'vive-tracker-01'}}") != null && set("{'spots':{'RH':'index-controller-left'}}") != null &&
                    set("{'spots':{'RF':'calc.exe'}}") != null && set("{'spots':{'RF':'vive-tracker-01','LF':'vive-tracker-01'}}") != null &&
                    set("{'spots':{},'hip':['FW','FC']}") != null && set("{'spots':{},'chest':['FC','FC']}") != null && s2.Worn.Count == 12);
                check("vr-launch: hand tracking may be on both hands; any two waist spots make Double Hip",
                    set("{'spots':{'F':'quest-3','RH':'hand-tracking','LH':'hand-tracking','RS':'vive-tracker-09','LS':'vive-tracker-04','RF':'vive-tracker-01'},'hip':['RS','LS'],'chest':['FS','BS']}") == null &&
                    s2.HipPair.SequenceEqual(new[] { "LHR-467E1FA2", "LHR-6BA0C57A" }) && s2.ChestPair.Length == 0 && s2.HeadTracker == "");
                check("vr-launch: a launch needs its spots: the headset on Face, a head tracker for x.1 to x.3, both trackers of a pair, the object's tracker",
                    s2.Missing(new LaunchRequest { Headset = "quest-pro", Mode = "native", Double = "" }).Contains("Quest 3") &&
                    s2.Missing(new LaunchRequest { Headset = "quest-3", Mode = "space-sync", Double = "" }).Contains("24 Head [H]") &&
                    s2.Missing(new LaunchRequest { Headset = "quest-3", Mode = "native", Double = "both" }).Contains("15 Front Sternum [FS]") &&
                    s2.Missing(new LaunchRequest { Headset = "quest-3", Mode = "native", Double = "hip", Object = "dance-pole" }).Contains("25 Dance Pole [DP]") &&
                    s2.Missing(new LaunchRequest { Headset = "quest-3", Mode = "native", Double = "hip", Object = "" }) == null);
                check("vr-launch: VRChat's command line carries no list (Steam adds its launch options)", VrLaunch.VrChatArguments() == "-applaunch 438100");
                var need = VrLaunch.NeededDevices(new LaunchRequest { Headset = "quest-pro", Mode = "space-calibrator", Double = "both" }, s1).Select(k => k.Key).ToList();
                check("vr-launch: Start VRChat waits for the headset, the controllers in her hands and every tracker on a spot (" + string.Join(",", need) + ")",
                    need.SequenceEqual(new[] { "quest-pro", "index-controller-right", "index-controller-left", "vive-tracker-01", "vive-tracker-07", "vive-tracker-06", "vive-tracker-02", "vive-tracker-09", "vive-tracker-04", "vive-tracker-05", "vive-tracker-03", "vive-tracker-08" }) &&
                    !VrLaunch.NeededDevices(new LaunchRequest { Headset = "quest-3", Mode = "native", Double = "" }, s2).Any(k => k.Key.StartsWith("index-controller")));

                string cfg = Path.Combine(temp, "user_config.json");
                File.WriteAllText(cfg, Q("{'useHip':true,'useChest':true,'chestSerialA':'LHR-28D88802','chestSerialB':'LHR-91D63DB2','extraSerialA':'LHR-467E1FA2','extraSerialB':'LHR-6BA0C57A','freezeWhenBothLost':false,'locale':'en'}"));
                string said = VrLaunch.SetDoubleHip(cfg, "chest", s1);
                var after = map(File.ReadAllText(cfg));
                check("vr-launch: Double Chest turns Hip off and Chest on, and keeps everything else (" + said + ")",
                    Equals(after["useHip"], false) && Equals(after["useChest"], true) && (string)after["chestSerialA"] == "LHR-28D88802" && (string)after["locale"] == "en" &&
                    File.Exists(Path.Combine(temp, "user_config.before-rift-rabbit-20261024.json")));
                set("{'spots':{'RS':'vive-tracker-04','LS':'vive-tracker-09'},'hip':['RS','LS']}");
                VrLaunch.SetDoubleHip(cfg, "both", s2);
                after = map(File.ReadAllText(cfg));
                check("vr-launch: the pairs from the spots are written; its own settings were kept once", Equals(after["useHip"], true) && (string)after["extraSerialA"] == "LHR-6BA0C57A" && Equals(after["useHipAutomatic"], false) &&
                    (string)after["chestSerialA"] == "LHR-28D88802" && File.ReadAllText(Path.Combine(temp, "user_config.before-rift-rabbit-20261024.json")).Contains(Q("'useChest':true,")));

                string page = Path.Combine(temp, VrLaunch.PageDataName);
                VrLaunch.WritePageData(page, s1, true);
                string pageText = File.ReadAllText(page);
                check("vr-launch: the pages' file says where and how to reach it", pageText.StartsWith("window.riftRabbitLaunch = {") && pageText.Contains(Q("'url':'http://127.0.0.1:47621/'")) && pageText.Contains(s1.Code));
                VrLaunch.WritePageData(page, s1, false);
                check("vr-launch: OFF, the pages' file has no code, and it carries the spots", !File.ReadAllText(page).Contains(s1.Code) && File.ReadAllText(page).Contains(Q("'H':'vive-tracker-08'")));
                check("vr-launch: Native and Space Calibrator need nothing installed", VrLaunch.Installed("native") && VrLaunch.Installed("space-calibrator"));

                string lnk = Path.Combine(temp, "test.lnk");
                VrLaunch.MakeShortcut(lnk, Path.Combine(Environment.SystemDirectory, "notepad.exe"), "-x");
                check("vr-launch: a shortcut can be made", File.Exists(lnk));

                check("vr-launch: only a file page or this PC may ask", LaunchServer.OriginOk(null) && LaunchServer.OriginOk("null") && LaunchServer.OriginOk("http://127.0.0.1:8123") &&
                    !LaunchServer.OriginOk("https://example.com") && !LaunchServer.OriginOk("http://127.0.0.1.example.com") && !LaunchServer.OriginOk("http://localhost:80/x"));
                check("vr-launch: codes compare whole", LaunchServer.SameCode("abc", "abc") && !LaunchServer.SameCode("abc", "abd") && !LaunchServer.SameCode("abc", "ab") && !LaunchServer.SameCode(null, "a"));

                var rabbit = new RabbitState(temp, temp);
                s1.Port = 0;
                LaunchRun made = null;
                using (var server = new LaunchServer(rabbit, s1))
                {
                    server.MakeRun = r => made = new LaunchRun(rabbit, s1, r);
                    int saved = 0;
                    server.Saved = () => saved++;
                    int port = server.Port;
                    string code = Q("'code':'" + s1.Code + "'");
                    string launchBody = "{" + code + Q(",'headset':'quest-pro','mode':'space-calibrator','streamer':'virtual-desktop','double':'hip','osc':true}");
                    check("vr-launch: it listens on 127.0.0.1 only", port > 0);
                    check("vr-launch: a web site is refused", Post(port, "https://example.com", "/launch", launchBody).StartsWith("HTTP/1.1 403") && made == null);
                    string spotsBody = "{" + code + Q(",'spots':{'F':'quest-pro','RH':'index-controller-right','LH':'index-controller-left','FW':'vive-tracker-09','BW':'vive-tracker-04','FC':'vive-tracker-05','BC':'vive-tracker-03','H':'vive-tracker-08'},'hip':['FW','BW'],'chest':['FC','BC']}");
                    check("vr-launch: a web site cannot change the spots", Post(port, "https://example.com", "/spots", spotsBody).StartsWith("HTTP/1.1 403") && saved == 0);
                    string spotsOk = Post(port, "null", "/spots", spotsBody);
                    check("vr-launch: the page's spots are kept and read back", spotsOk.Contains(Q("'ok':true")) && saved == 1 && s1.At("RF") == "" && s1.HeadTracker == "LHR-6963FCE5");
                    check("vr-launch: wrong spots change nothing and say why", Post(port, "null", "/spots", "{" + code + Q(",'spots':{'H':'notepad'}}")).Contains("does not go on") && saved == 1 && s1.HeadTracker == "LHR-6963FCE5");
                    check("vr-launch: a launch its spots cannot do is refused", Post(port, "null", "/launch", "{" + code + Q(",'headset':'quest-3','mode':'native'}")).Contains("Quest Pro on it") && made == null);
                    check("vr-launch: a wrong code is refused", Post(port, "null", "/launch", Q("{'code':'nope','headset':'quest-pro','mode':'native'}")).StartsWith("HTTP/1.1 401") && made == null);
                    string ok = Post(port, "null", "/launch", launchBody);
                    check("vr-launch: the page's launch starts one, and says so", ok.StartsWith("HTTP/1.1 200") && ok.Contains("Access-Control-Allow-Origin: null") && made != null && made.Request.Double == "hip" && ok.Contains("open:double-hip-tracker"));
                    check("vr-launch: one launch at a time", Post(port, "null", "/launch", launchBody).Contains("already running"));
                    check("vr-launch: spots wait while a launch runs", Post(port, "null", "/spots", "{" + code + Q(",'spots':{}}")).Contains("A launch is running") && saved == 1);
                    check("vr-launch: Start VRChat waits until everything is on", Post(port, "null", "/vrchat", "{" + code + "}").Contains("Not everything is on yet"));
                    check("vr-launch: a page can read the steps", Post(port, "http://127.0.0.1:8123", "/status", "{" + code + "}").Contains(Q("'steps':")));
                    try { new TcpClient("::1", port).Close(); check("vr-launch: nothing but 127.0.0.1 answers", false); }
                    catch (Exception) { check("vr-launch: nothing but 127.0.0.1 answers", true); }
                }
            }
            catch (Exception e) { check("vr-launch: the checks ran (" + e.Message + ")", false); }
            finally { try { Directory.Delete(temp, true); } catch (Exception) { } }
        }
    }
}
