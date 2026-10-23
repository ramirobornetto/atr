using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
namespace AchievementTimelineReplay {
    internal enum SamCliMode
    {
        Unknown,
        Modern,
        Legacy
    }

    internal sealed class AchievementRecord
    {
        public string ApiId;
        public string DisplayName;
        public string Description;
        public long OriginalUnix;
        public bool Included = true;
        public double? ManualGapSeconds;
        public string SteamState = "—";

        public AchievementRecord Clone()
        {
            AchievementRecord a = new AchievementRecord();
            a.ApiId = ApiId;
            a.DisplayName = DisplayName;
            a.Description = Description;
            a.OriginalUnix = OriginalUnix;
            a.Included = Included;
            a.ManualGapSeconds = ManualGapSeconds;
            a.SteamState = SteamState;
            return a;
        }
    }

    internal sealed class GameEntry
    {
        public uint AppId;
        public uint AccountId;
        public string HistoryId;
        public string SourceLabel = "UserGameStats (.bin)";
        public string DetectedOrigin;
        public bool IsDemo;
        public string StorageKey => String.IsNullOrEmpty(HistoryId) ? AccountId+"_"+AppId : HistoryId+"_"+AppId;
        public string GameName;
        public string AppType;
        public int TotalAchievements;
        public bool IsOwned;
        public bool HasLocalHistory;
        public bool HasSnapshot;
        public string StatsPath;
        public string SchemaPath;
        public List<AchievementRecord> Achievements = new List<AchievementRecord>();

        public override string ToString()
        {
            string history = HasLocalHistory
                ? String.Format("{0} logros históricos", Achievements.Count)
                : "sin historial local";
            return String.Format("{0} [{1}] · {2}", GameName, AppId, history);
        }
    }

    internal sealed class SnapshotData
    {
        public int Version { get; set; }
        public uint AppId { get; set; }
        public uint AccountId { get; set; }
        public string GameName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public List<SnapshotAchievement> Achievements { get; set; }
    }

    internal sealed class SnapshotAchievement
    {
        public string ApiId { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public long OriginalUnix { get; set; }
    }

    internal sealed class ScheduleItem
    {
        public AchievementRecord Achievement;
        public double GapSeconds;
        public double OffsetSeconds;
        public DateTime Due;
    }

    internal sealed class ProcessResult
    {
        public int ExitCode;
        public string StdOut;
        public string StdErr;
        public bool TimedOut;
    }

    internal static class HistoryLoader
    {
        public static GameEntry Load(string statsPath, string schemaPath, uint accountId, uint appId)
        {
            Dictionary<string, object> schemaRoot = BinaryKvReader.ReadFile(schemaPath);
            Dictionary<string, object> statsRoot = BinaryKvReader.ReadFile(statsPath);

            Dictionary<string, object> app = GetDict(schemaRoot, appId.ToString(CultureInfo.InvariantCulture));
            Dictionary<string, object> schemaStats = GetDict(app, "stats");
            Dictionary<string, object> cache = GetDict(statsRoot, "cache");

            GameEntry game = new GameEntry();
            game.AppId = appId;
            game.AccountId = accountId;
            game.StatsPath = statsPath;
            game.SchemaPath = schemaPath;
            game.GameName = GetString(app, "gamename", appId.ToString(CultureInfo.InvariantCulture));
            game.HasLocalHistory = true;

            foreach (KeyValuePair<string, object> groupEntry in schemaStats)
            {
                Dictionary<string, object> group = groupEntry.Value as Dictionary<string, object>;

                if (group == null)
                    continue;

                string type = GetString(group, "type", "");

                if (!String.Equals(type, "ACHIEVEMENTS", StringComparison.OrdinalIgnoreCase))
                    continue;

                Dictionary<string, object> bits = GetDictOrNull(group, "bits");
                Dictionary<string, object> cacheGroup = GetDictOrNull(cache, groupEntry.Key);

                if (bits != null)
                    game.TotalAchievements += bits.Values.Count(value => value is Dictionary<string, object>);

                if (bits == null || cacheGroup == null)
                    continue;

                Dictionary<string, object> times = GetDictOrNull(cacheGroup, "AchievementTimes");

                if (times == null)
                    continue;

                foreach (KeyValuePair<string, object> bitEntry in bits)
                {
                    Dictionary<string, object> bit = bitEntry.Value as Dictionary<string, object>;

                    if (bit == null)
                        continue;

                    long ts = GetLong(times, bitEntry.Key, 0);

                    if (ts <= 0)
                        continue;

                    AchievementRecord a = new AchievementRecord();
                    a.ApiId = GetString(bit, "name", "bit_" + bitEntry.Key);
                    a.DisplayName = GetDisplayText(bit, "name", a.ApiId);
                    a.Description = GetDisplayText(bit, "desc", "");
                    a.OriginalUnix = ts;
                    a.Included = true;

                    game.Achievements.Add(a);
                }
            }

            game.Achievements = game.Achievements
                .OrderBy(delegate(AchievementRecord a) { return a.OriginalUnix; })
                .ToList();

            return game;
        }

        private static string GetDisplayText(Dictionary<string, object> bit, string field, string fallback)
        {
            Dictionary<string, object> display = GetDictOrNull(bit, "display");
            Dictionary<string, object> values = display == null ? null : GetDictOrNull(display, field);

            if (values != null)
            {
                string[] preferred = new string[] { "latam", "spanish", "english" };

                foreach (string lang in preferred)
                {
                    string value = GetString(values, lang, "");
                    if (!String.IsNullOrEmpty(value))
                        return value;
                }

                foreach (KeyValuePair<string, object> kv in values)
                {
                    string s = kv.Value as string;

                    if (!String.IsNullOrEmpty(s) &&
                        !String.Equals(kv.Key, "token", StringComparison.OrdinalIgnoreCase))
                        return s;
                }
            }

            return fallback;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> d, string key)
        {
            Dictionary<string, object> result = GetDictOrNull(d, key);

            if (result == null)
                throw new InvalidDataException("Falta la sección '" + key + "'.");

            return result;
        }

        private static Dictionary<string, object> GetDictOrNull(Dictionary<string, object> d, string key)
        {
            if (d == null || !d.ContainsKey(key))
                return null;

            return d[key] as Dictionary<string, object>;
        }

        private static string GetString(Dictionary<string, object> d, string key, string fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            string s = d[key] as string;
            return s ?? Convert.ToString(d[key], CultureInfo.InvariantCulture);
        }

        private static long GetLong(Dictionary<string, object> d, string key, long fallback)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null)
                return fallback;

            object v = d[key];

            if (v is long)
                return (long)v;

            if (v is ulong)
                return unchecked((long)(ulong)v);

            if (v is int)
                return (int)v;

            if (v is uint)
                return (uint)v;

            long parsed;

            if (Int64.TryParse(
                Convert.ToString(v, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out parsed))
                return parsed;

            return fallback;
        }
    }

    internal sealed class BinaryKvReader
    {
        private BinaryReader reader;

        private BinaryKvReader(Stream stream)
        {
            reader = new BinaryReader(stream, Encoding.UTF8);
        }

        public static Dictionary<string, object> ReadFile(string path)
        {
            using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                BinaryKvReader r = new BinaryKvReader(fs);
                return r.ReadObject();
            }
        }

        private Dictionary<string, object> ReadObject()
        {
            Dictionary<string, object> dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte type = reader.ReadByte();

                if (type == 8)
                    break;

                string key = ReadCString();
                object value;

                switch (type)
                {
                    case 0:
                        value = ReadObject();
                        break;

                    case 1:
                        value = ReadCString();
                        break;

                    case 2:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 3:
                        value = reader.ReadSingle();
                        break;

                    case 4:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 5:
                        value = ReadWideString();
                        break;

                    case 6:
                        value = (long)reader.ReadUInt32();
                        break;

                    case 7:
                        value = reader.ReadUInt64();
                        break;

                    case 9:
                        value = (long)reader.ReadByte();
                        break;

                    case 10:
                        value = 0L;
                        break;

                    case 11:
                        value = 1L;
                        break;

                    default:
                        throw new InvalidDataException(
                            "Tipo Binary KeyValues no soportado: 0x" +
                            type.ToString("X2") +
                            " en '" + key + "'.");
                }

                dict[key] = value;
            }

            return dict;
        }

        private string ReadCString()
        {
            List<byte> bytes = new List<byte>();

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                byte b = reader.ReadByte();

                if (b == 0)
                    break;

                bytes.Add(b);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private string ReadWideString()
        {
            ushort chars = reader.ReadUInt16();

            if (chars == 0)
                return "";

            byte[] bytes = reader.ReadBytes(chars * 2);
            string s = Encoding.Unicode.GetString(bytes);
            return s.TrimEnd('\0');
        }
    }
}
