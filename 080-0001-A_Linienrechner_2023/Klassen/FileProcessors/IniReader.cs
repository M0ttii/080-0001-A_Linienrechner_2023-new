using IniParser;
using IniParser.Model;
using Linienrechner.Klassen.Model;

namespace Linienrechner.Klassen.FileProcessors;

/// <summary>
///     IniReader Klasse, die die Ini Datei ausliest und die Platzhalter speichert
/// </summary>
internal class IniReader
{
    //Variablendeklaration
    public string filePath;
    private readonly IniData iniData;
    private readonly FileIniDataParser iniFile = new();
    private readonly Dictionary<string, object> lists = new();

    private readonly Dictionary<string, string> platzhalterRaw = new();
    private readonly Dictionary<string, object> platzhalterRealValues = new();
    private readonly Dictionary<string, string> platzhalterSpecial = new();

    /// <summary>Konstruktor, liest die Ini Datei aus und speichert die Platzhalter</summary>
    /// <param name="config"></param>
    public IniReader(ChannelConfig config)
    {
        filePath = config.iniPath;

        iniData = iniFile.ReadFile(filePath);
        foreach (var section in iniData.Sections)
            if (section.SectionName == config.sectionName)
            {
                var replaceName = iniData[config.sectionName][config.replaceName];
                foreach (var key in section.Keys)
                {
                    if (key.KeyName.StartsWith(replaceName))
                    {
                        if (key.Value != "ReadClock")
                        {
                            platzhalterRaw.Add(key.KeyName, key.Value);
                            continue;
                        }

                        platzhalterSpecial.Add(key.KeyName, key.Value);
                    }

                    if (key.KeyName.StartsWith("StringList")) lists.Add(key.KeyName, key.Value);
                    if (key.KeyName == "###Linienkennung###" || key.KeyName == "###Nacharbeit_OK###" ||
                        key.KeyName == "###Letzte_AST###") platzhalterSpecial.Add(key.KeyName, key.Value);
                }
            }
    }

    public Dictionary<string, string> getSpecialPlatzhalter()
    {
        return platzhalterSpecial;
    }

    public Dictionary<string, string> getPlatzhalterRaw()
    {
        return platzhalterRaw;
    }

    public Dictionary<string, object> getPlatzhalterReal()
    {
        return platzhalterRealValues;
    }

    public Dictionary<string, object> getStringLists()
    {
        return lists;
    }
}