using System.Reflection;
using System.Runtime.InteropServices;
using Linienrechner.Klassen.Model;
using Linienrechner.Klassen.Utils;
using log4net;
using log4net.Config;
using Newtonsoft.Json;

namespace Linienrechner.Klassen;

internal static class Program
{
    //Variables
    public static Assembly myAssembly = Assembly.GetExecutingAssembly();
    public static string version = myAssembly.GetName().Version.ToString();

    private static readonly ILog log = LogManager.GetLogger(typeof(Program));
    private static LogFileCleanupTask logFileCleanupTask;
    public static Configuration configuration { get; private set; }
    public static string configPath { get; set; }

    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int dwProcessId);

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            MessageBox.Show(
                "Fehler beim starten des Programms.\nError: Keinen Configparameter angegeben.\nBsp.: --config=D:\\config.json",
                "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var argSplit = args[0].Split("=");
        if (argSplit.Length != 2)
        {
            MessageBox.Show(
                "Fehler beim starten des Programms.\nError: Configparameter hat das falsche Format.\nBsp.: --config=D:\\config.json",
                "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        configPath = argSplit[1];

        if (!configExisits(configPath))
        {
            MessageBox.Show("Fehler beim starten des Programms.\nError: Config existiert nicht am angegebenen Pfad.",
                "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        initConfig(configPath);
        XmlConfigurator.Configure(new FileInfo(configuration.logConfigPath));
        log.Info("Programm gestartet.");

        logFileCleanupTask = new LogFileCleanupTask();
        logFileCleanupTask.CleanUp(configuration.cleanUpDays);

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    /// <summary>Checks if the config file exists</summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool configExisits(string path)
    {
        if (File.Exists(path)) return true;
        return false;
    }

    /// <summary>Loads config in memory</summary>
    /// <param name="path"></param>
    public static void initConfig(string path)
    {
        if (File.Exists(path))
        {
            using (var file = File.OpenText(path))
            {
                try
                {
                    var serializer = new JsonSerializer();
                    configuration = (Configuration)serializer.Deserialize(file, typeof(Configuration));
                }
                catch (Exception e)
                {
                    MessageBox.Show(
                        "Fehler beim starten des Programms.\nError: Config konnte nicht gelesen werden.\n" + e.Message,
                        "Fehler", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
        }
    }
}