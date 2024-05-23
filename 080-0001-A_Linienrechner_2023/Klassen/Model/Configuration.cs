using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Linienrechner.Klassen.Model
{
    /// <summary>Konfigurationsklasse, Vorlage für die config.json</summary>
    internal class Configuration
    {
        public string spsIP { get; set; }
        public string CPUType { get; set; }
        public short rack { get; set; }
        public short slot { get; set; }
        public int liveDB { get; set; }
        public int liveByte { get; set; }
        public int liveBit { get; set; }
        public int liveDelay { get; set; }
        public int retryDelay { get; set; }
        public int retryCount { get; set; }
        public string logConfigPath { get; set; }
        public int cleanUpDays { get; set; }
        public ChannelConfig[] configs { get; set; }
    }

    /// <summary>Config für einen einzelnen Kanal</summary>
    internal class ChannelConfig
    {
        public string name { get; set; }
        public string iniPath { get; set; }
        public XmlOptions xmlOptions { get; set; }
        public string sectionName { get; set; }
        public string replaceName { get; set; }
        public int DB { get; set; }
        public int anfDB { get; set; }
        public int anfDelay { get; set; }
        public int anfByte { get; set; }
        public int anfBit { get; set; }
        public int quitBit { get; set; }
    }

    /// <summary>XmlOptionen für die Kanäle</summary>
    internal class XmlOptions
    {
        public string xmlTemplatePath { get; set; }
        public string xmlSavePathIO { get; set; }
        public string xmlSavePathNIO { get; set; }
    }
}
