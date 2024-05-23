using System.Text;
using System.Xml;
using System.Xml.Linq;
using Linienrechner.Klassen.Model;
using Linienrechner.Klassen.Pollers;
using log4net;

namespace Linienrechner.Klassen.FileProcessors;

/// <summary>
///     XmlTemplateProcessor Klasse, die die XML Datei erstellt und speichert
/// </summary>
internal class XmlTemplateProcessor
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Poller));
    private readonly ChannelConfig config;
    public bool error;
    private readonly Form1 form1;
    private readonly bool nio;
    private readonly Dictionary<string, object> platzhalter;
    public PollManager pollManager;

    /// <summary>Konstruktor</summary>
    /// <param name="platzhalter"></param>
    /// <param name="config"></param>
    /// <param name="nio"></param>
    /// <param name="form1"></param>
    /// <param name="pollManager"></param>
    public XmlTemplateProcessor(Dictionary<string, object> platzhalter, ChannelConfig config, bool nio, Form1 form1,
        PollManager pollManager)
    {
        this.config = config;
        this.platzhalter = platzhalter;
        this.nio = nio;
        this.form1 = form1;
        this.pollManager = pollManager;
    }

    /// <summary>Reinigt die XML Datei von nicht erlaubten Zeichen</summary>
    /// <param name="inString"></param>
    /// <returns></returns>
    public static string RemoveTroublesomeCharacters(string inString)
    {
        if (inString == null) return null;

        var newString = new StringBuilder();
        char ch;

        for (var i = 0; i < inString.Length; i++)
        {
            ch = inString[i];
            if (XmlConvert.IsXmlChar(ch)) newString.Append(ch);
        }

        return newString.ToString();
    }

    /// <summary>Ersetzt die Platzhalter in der XML Datei</summary>
    public void replace()
    {
        using (var sr = new StreamReader(config.xmlOptions.xmlTemplatePath, Encoding.GetEncoding("ISO-8859-1")))
        {
            var doc = XDocument.Load(sr);
            doc.Declaration = new XDeclaration("1.0", "ISO-8859-1", null);
            foreach (var platz in platzhalter)
            {
                var elementsToUpdate = doc.Descendants().Where(x => x.Value == platz.Key);

                foreach (var element in elementsToUpdate)
                {
                    if (element.Value == "###Platzhalter###_4") continue;
                    ;
                    if (platz.Value != null || !ContainsUnicodeCharacter(platz.Value.ToString()))
                    {
                        var bytes = Encoding.Default.GetBytes(platz.Value.ToString());
                        element.Value = RemoveTroublesomeCharacters(platz.Value.ToString());
                    }
                    else
                    {
                        element.Value = "";
                    }
                }
            }

            var saveString =
                config.xmlOptions.xmlSavePathNIO.Replace("{name}", platzhalter["###Platzhalter###_2"].ToString());
            if (nio == false)
            {
                doc.Descendants().Where(x => x.Name == "NACHARBEIT").Remove();
                saveString =
                    config.xmlOptions.xmlSavePathIO.Replace("{name}", platzhalter["###Platzhalter###_2"].ToString());
            }

            var xmlString = doc.ToString();
            xmlString = xmlString
                .Replace("+###Linienkennung###+", platzhalter["###Linienkennung###"].ToString())
                .Replace("+###Linienkennung###", platzhalter["###Linienkennung###"].ToString())
                .Replace("###Linienkennung###", platzhalter["###Linienkennung###"].ToString());

            if (platzhalter.ContainsKey("###Platzhalter###_4"))
                xmlString = xmlString.Replace("###Platzhalter###_4", platzhalter["###Platzhalter###_4"].ToString());
            var parsedDoc = XDocument.Parse(xmlString);
            parsedDoc.Declaration = new XDeclaration("1.0", "ISO-8859-1", null);

            if (File.Exists(saveString))
            {
                error = true;
                var line = config.name;
                log.Error(config.name + ": " + "Störung.");
                pollManager.CancelPolling();
                form1.Invoke((MethodInvoker)delegate
                {
                    var dialogResult = MessageBox.Show(form1,
                        "Linie: " + line + "\n\nDie Datei " + saveString +
                        " existiert bereits. Bitte Problem beheben. Programm neustarten.", "Datei existiert bereits",
                        MessageBoxButtons.OK);
                    form1.Close();
                });
                return;
            }

            if (!pollManager.isCancelRequested())
            {
                parsedDoc.Save(saveString);
                log.Info(config.name + ": " + "Datei gespeichert.");
                try
                {
                    form1.Invoke((MethodInvoker)delegate
                    {
                        form1.addLatestXML(config.name, platzhalter["###Platzhalter###_2"].ToString());
                    });
                }
                catch (Exception e)
                {
                    log.Error("Fehler beim hinzufügen der XML Datei in die Liste: " + e.Message);
                }
            }
        }
    }

    /// <summary>Prüft ob ein String Unicode Zeichen enthält</summary>
    /// <param name="input"></param>
    /// <returns></returns>
    public bool ContainsUnicodeCharacter(string input)
    {
        const int MaxAnsiCode = 255;

        return input.Any(c => c > MaxAnsiCode);
    }
}