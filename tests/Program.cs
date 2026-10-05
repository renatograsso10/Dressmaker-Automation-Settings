using DressmakerAutomation.SettingsUI;

void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); }
var root = Path.Combine(Path.GetFullPath("work/ui-tests"), "scratch");
Directory.CreateDirectory(root);
var settings = new Settings { InfiniteMoney = false, GoldFloor = 42, AutoGrainAlignment = false, FindValidPosition = true, AutoMannequinSizing = false };
var existing = "# keep comment\r\nInfiniteGold=true\r\nInfiniteMoney=true\r\nAutoMannequin=true\r\nUnknownOption=keep\r\nGoldFloor=999\r\n";
string rewritten = settings.Rewrite(existing);
Check(rewritten.Contains("# keep comment") && rewritten.Contains("UnknownOption=keep"), "preserve comments and unrecognized settings");
Check(rewritten.Split('\n').Count(line => line.StartsWith("InfiniteMoney=")) == 1 && !rewritten.Contains("InfiniteGold="), "normalize legacy alias and remove duplicates");
Check(rewritten.Contains("AutoMannequinSizing=false") && rewritten.Contains("GoldFloor=42"), "persist values using original core's keys");
Check(rewritten.Contains("FindValidPosition=true") && rewritten.Contains("AutoGrainAlignment=false"), "retain placement preference when grain alignment disabled");
Check(Settings.TryGoldFloor("0", out _) && Settings.TryGoldFloor("2147483647", out _), "accept integer bounds");
Check(new[] { "", "-1", "2147483648", "1.2", "1,000", "abc" }.All(value => !Settings.TryGoldFloor(value, out _)), "reject invalid or overflowing balances");
string path = Path.Combine(root, "config", "DressmakerAutomation.cfg");
settings.Save(path);
Check(File.ReadAllText(path).Contains("GoldFloor=42"), "create config when missing");
File.WriteAllText(path, existing);
settings.Save(path);
Check(File.ReadAllText(path + ".ui-backup") == existing && File.ReadAllText(path) == rewritten, "atomic replacement preserves previous config backup");
Check(!Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Any(), "no temporary file left after save");
string lockedPath = Path.Combine(root, "locked.cfg");
File.WriteAllText(lockedPath, "ORIGINAL");
bool rejected = false;
using (var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
{ try { settings.Save(lockedPath); } catch (IOException) { rejected = true; } }
Check(rejected && File.ReadAllText(lockedPath) == "ORIGINAL", "failed save leaves existing config intact");
