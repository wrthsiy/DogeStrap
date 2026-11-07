using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Bloxstrap
{
    /// <summary>
    /// One editable tag of GlobalBasicSettings_13.xml (a row of the "Advanced" grid)
    /// </summary>
    public class GlobalSettingEntry
    {
        public string Name { get; set; } = "";

        public string Type { get; set; } = "string";

        public string Value { get; set; } = "";

        /// <summary>
        /// True for tags that were read from the file. Their name and type are locked in the grid, only the value can be changed.
        /// </summary>
        public bool IsExisting { get; set; } = false;
    }

    public enum GlobalSettingsSaveResult
    {
        Saved,
        RobloxRunning,
        NotLoaded,
        Failed
    }

    /// <summary>
    /// Reads and writes %LocalAppData%\Roblox\GlobalBasicSettings_13.xml.
    /// Only tags that are explicitly edited are touched, everything else in the file is preserved as it is.
    /// </summary>
    public class GlobalSettingsManager
    {
        public const string FileName = "GlobalBasicSettings_13.xml";

        private const string ClassName = "UserGameSettings";
        private const string RobloxProcessName = "RobloxPlayerBeta";

        /// <summary>
        /// Value types that can be edited as plain text (Vector2, BinaryString etc. are left untouched)
        /// </summary>
        public static readonly string[] SupportedTypes = { "bool", "int", "int64", "float", "double", "string", "token" };

        // edited with the sliders/textbox above the grid
        private static readonly HashSet<string> BasicNames = new() { "GraphicsQualityLevel", "FramerateCap", "PreferredTransparency" };

        // boilerplate of the instance itself, not a setting
        private static readonly HashSet<string> HiddenNames = new()
        {
            "AttributesSerialize", "Capabilities", "DefinesCapabilities", "Name", "SerializedOverrides", "SourceAssetId", "Tags"
        };

        // the exact shape Roblox itself writes, with only the default-valued instance properties,
        // so a missing property keeps behaving exactly like it would if the file didn't exist
        private const string DefaultXml = @"<roblox xmlns:xmime='http://www.w3.org/2005/05/xmlmime' xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xsi:noNamespaceSchemaLocation='http://www.roblox.com/roblox.xsd' version='4'>
	<External>null</External>
	<External>nil</External>
	<Item class='UserGameSettings' referent='{REFERENT}'>
		<Properties>
			<BinaryString name='AttributesSerialize'></BinaryString>
			<SecurityCapabilities name='Capabilities'>0</SecurityCapabilities>
			<bool name='DefinesCapabilities'>false</bool>
			<string name='Name'>GameSettings</string>
			<BinaryString name='SerializedOverrides'></BinaryString>
			<int64 name='SourceAssetId'>-1</int64>
		</Properties>
	</Item>
</roblox>";

        public static string FilePath => Path.Combine(Paths.LocalAppData, "Roblox", FileName);

        public static string BackupPath => FilePath + ".bak";

        private XDocument? _document;

        public bool IsLoaded => _document is not null;

        #region Static helpers

        public static bool IsRobloxRunning()
        {
            var processes = Process.GetProcessesByName(RobloxProcessName);

            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                    process.Dispose();
            }
        }

        public static bool IsReadOnly()
        {
            try
            {
                return File.Exists(FilePath) && File.GetAttributes(FilePath).HasFlag(FileAttributes.ReadOnly);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Creates GlobalBasicSettings_13.xml if it doesn't exist (Roblox does not always create it on its own).
        /// An existing file is never modified. Returns true if the file was created.
        /// </summary>
        public static bool EnsureFileExists()
        {
            const string LOG_IDENT = "GlobalSettingsManager::EnsureFileExists";

            try
            {
                if (File.Exists(FilePath))
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                WriteDocument(CreateDefaultDocument(), FilePath);

                App.Logger.WriteLine(LOG_IDENT, $"Created {FilePath}");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to create {FilePath}");
                App.Logger.WriteException(LOG_IDENT, ex);
                return false;
            }
        }

        private static XDocument CreateDefaultDocument()
        {
            string referent = "RBX" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            return XDocument.Parse(DefaultXml.Replace("{REFERENT}", referent));
        }

        private static void WriteDocument(XDocument document, string path)
        {
            // same formatting as Roblox: no xml declaration, tabs, LF, utf-8 without BOM
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = true,
                IndentChars = "\t",
                NewLineChars = "\n",
                NewLineHandling = NewLineHandling.Replace,
                Encoding = new UTF8Encoding(false)
            };

            using var writer = XmlWriter.Create(path, settings);
            document.Save(writer);
        }

        private static void SetReadOnly(string path, bool readOnly)
        {
            var attributes = File.GetAttributes(path);

            attributes = readOnly ? attributes | FileAttributes.ReadOnly : attributes & ~FileAttributes.ReadOnly;

            if (attributes == 0)
                attributes = FileAttributes.Normal;

            File.SetAttributes(path, attributes);
        }

        #endregion

        #region Loading and reading

        /// <summary>
        /// Loads the file (or an in-memory default if it doesn't exist yet). Returns null on success, otherwise the error message.
        /// </summary>
        public string? Load()
        {
            const string LOG_IDENT = "GlobalSettingsManager::Load";

            try
            {
                if (File.Exists(FilePath))
                {
                    // FileShare.ReadWrite so reading still works if something else has the file open
                    using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    _document = XDocument.Load(stream);
                }
                else
                {
                    _document = CreateDefaultDocument();
                }

                if (GetPropertiesElement() is null)
                    throw new InvalidDataException($"The file has no '{ClassName}' item with properties.");

                return null;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to load {FilePath}");
                App.Logger.WriteException(LOG_IDENT, ex);

                _document = null;
                return ex.Message;
            }
        }

        private XElement? GetPropertiesElement()
        {
            return _document?.Root?
                .Elements("Item")
                .FirstOrDefault(x => (string?)x.Attribute("class") == ClassName)?
                .Element("Properties");
        }

        private XElement? FindProperty(string name)
        {
            return GetPropertiesElement()?.Elements().FirstOrDefault(x => (string?)x.Attribute("name") == name);
        }

        /// <summary>
        /// Returns the text value of a scalar tag, or null if the tag doesn't exist
        /// </summary>
        public string? GetValue(string name)
        {
            var element = FindProperty(name);
            return element is not null && !element.HasElements ? element.Value : null;
        }

        /// <summary>
        /// All editable tags except the ones that have their own controls
        /// </summary>
        public List<GlobalSettingEntry> GetAdvancedEntries()
        {
            var entries = new List<GlobalSettingEntry>();
            var properties = GetPropertiesElement();

            if (properties is null)
                return entries;

            foreach (var element in properties.Elements())
            {
                string name = (string?)element.Attribute("name") ?? "";
                string type = element.Name.LocalName;

                if (name.Length == 0 || element.HasElements || !SupportedTypes.Contains(type))
                    continue;

                if (BasicNames.Contains(name) || HiddenNames.Contains(name))
                    continue;

                entries.Add(new GlobalSettingEntry { Name = name, Type = type, Value = element.Value, IsExisting = true });
            }

            return entries;
        }

        #endregion

        #region Editing

        /// <summary>
        /// Sets the value of a tag in memory, adding the tag if it doesn't exist. Nothing is written to disk until Save().
        /// </summary>
        public void SetValue(string name, string type, string value)
        {
            var properties = GetPropertiesElement() ?? throw new InvalidOperationException("Settings file is not loaded");
            var element = FindProperty(name);

            if (element is null)
                properties.Add(new XElement(type, new XAttribute("name", name), value));
            else
                element.Value = value;
        }

        /// <summary>
        /// Checks the rows of the Advanced grid. Returns null if everything is valid, otherwise a message for the user.
        /// </summary>
        public string? ValidateEntries(IEnumerable<GlobalSettingEntry> entries)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                string name = (entry.Name ?? "").Trim();
                string value = entry.Value ?? "";

                // an untouched blank row at the bottom of the grid
                if (!entry.IsExisting && name.Length == 0 && value.Length == 0)
                    continue;

                if (!Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$"))
                    return $"\"{name}\" is not a valid tag name. Use letters, digits and underscores only.";

                if (BasicNames.Contains(name) || HiddenNames.Contains(name))
                    return $"\"{name}\" can't be edited in the Advanced list.";

                if (!seen.Add(name))
                    return $"\"{name}\" appears more than once.";

                if (!entry.IsExisting && FindProperty(name) is not null)
                    return $"\"{name}\" already exists in the file and can't be added again.";

                string? valueError = ValidateValue(entry.Type, value);

                if (valueError is not null)
                    return $"{name}: {valueError}";
            }

            return null;
        }

        private static string? ValidateValue(string type, string value)
        {
            string trimmed = value.Trim();

            switch (type)
            {
                case "bool":
                    return trimmed.ToLowerInvariant() is "true" or "false" ? null : "expected true or false.";

                case "int":
                case "token":
                    return int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _) ? null : "expected a whole number.";

                case "int64":
                    return long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _) ? null : "expected a whole number.";

                case "float":
                    return float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float f) && float.IsFinite(f) ? null : "expected a number (use . as the decimal separator).";

                case "double":
                    return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) && double.IsFinite(d) ? null : "expected a number (use . as the decimal separator).";

                case "string":
                    return value.Any(c => char.IsControl(c) && c != '\t' && c != '\n' && c != '\r') ? "contains characters that are not allowed in XML." : null;

                default:
                    return $"unsupported type \"{type}\".";
            }
        }

        /// <summary>
        /// Writes validated Advanced grid rows to the in-memory document (call ValidateEntries first)
        /// </summary>
        public void ApplyEntries(IEnumerable<GlobalSettingEntry> entries)
        {
            foreach (var entry in entries)
            {
                string name = (entry.Name ?? "").Trim();
                string value = entry.Value ?? "";

                if (!entry.IsExisting && name.Length == 0 && value.Length == 0)
                    continue;

                if (entry.Type != "string")
                    value = value.Trim();

                if (entry.Type == "bool")
                    value = value.ToLowerInvariant();

                SetValue(name, entry.Type, value);
            }
        }

        #endregion

        #region Saving

        /// <summary>
        /// Writes the document to disk. Refuses to do anything while Roblox is running (it would overwrite the file on exit).
        /// The previous file is always copied to GlobalBasicSettings_13.xml.bak first.
        /// </summary>
        public GlobalSettingsSaveResult Save(bool lockReadOnly, out string? error)
        {
            const string LOG_IDENT = "GlobalSettingsManager::Save";

            error = null;

            if (_document is null)
                return GlobalSettingsSaveResult.NotLoaded;

            if (IsRobloxRunning())
                return GlobalSettingsSaveResult.RobloxRunning;

            string path = FilePath;
            string tempPath = path + ".tmp";

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                if (File.Exists(path))
                {
                    // the file may be read-only (our own lock, or set by the user)
                    SetReadOnly(path, false);

                    // always keep a copy of the previous version; if this fails nothing is written
                    if (File.Exists(BackupPath))
                        SetReadOnly(BackupPath, false);

                    File.Copy(path, BackupPath, true);
                }

                WriteDocument(_document, tempPath);
                File.Move(tempPath, path, true);

                if (lockReadOnly)
                    SetReadOnly(path, true);

                App.Logger.WriteLine(LOG_IDENT, $"Saved {path}");
                return GlobalSettingsSaveResult.Saved;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to save {path}");
                App.Logger.WriteException(LOG_IDENT, ex);

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception)
                {
                    // nothing more to do
                }

                error = ex.Message;
                return GlobalSettingsSaveResult.Failed;
            }
        }

        #endregion
    }
}
