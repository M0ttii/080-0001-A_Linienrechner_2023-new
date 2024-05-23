using System.Diagnostics;
using Linienrechner.Klassen.FileProcessors;
using log4net;
using S7;

namespace Linienrechner.Klassen.Pollers;

internal class PollManager
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Poller));
    private readonly Form1 form;
    private readonly Dictionary<string, IniReader> iniWorkers;
    private bool isMonitoring;
    private bool isReconnecting;
    private LivePoller livePoller;
    private Task monitorTask;
    private readonly Dictionary<string, Poller> pollers;

    /// <summary>Konstruktor</summary>
    /// <param name="iniWorkers"></param>
    /// <param name="form"></param>
    public PollManager(Dictionary<string, IniReader> iniWorkers, Form1 form)
    {
        this.iniWorkers = iniWorkers;
        this.form = form;
        pollers = new Dictionary<string, Poller>();
        InitializePollers();
    }

    /// <summary>Stoppt alle Poller</summary>
    public void CancelPolling()
    {
        foreach (var poller in pollers) poller.Value.cancellationTokenSource.Cancel();
    }

    /// <summary>Überprüft ob ein Abbruch eines Tasks innerhalb eines Poller angefordert wurde</summary>
    /// <returns></returns>
    public bool isCancelRequested()
    {
        foreach (var poller in pollers)
            if (poller.Value.cancellationTokenSource.IsCancellationRequested)
                return true;
        return false;
    }

    /// <summary>Initialisiert und "öffnet" alle Poller</summary>
    public void InitializePollers()
    {
        foreach (var config in Program.configuration.configs)
            pollers.Add(config.name, new Poller(form, iniWorkers[config.name], config, this));

        foreach (var poller in pollers)
        {
            poller.Value.SPS = new PLC(CPU_Type.S7300, Program.configuration.spsIP, Program.configuration.rack, Program.configuration.slot);
            try
            {
                var error = poller.Value.SPS.Open();
                if (error != ErrorCode.NoError)
                {
                    MessageBox.Show(
                        "Poller konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.",
                        "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    log.Error("Poller " + poller.Value.config.name +
                              " konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.");
                    form.Invoke((MethodInvoker)delegate { form.Close(); });
                    return;
                }

                log.Info(poller.Value.config.name + ":" + " Verbindung zur SPS hergestellt.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Poller konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                log.Error("Poller " + poller.Value.config.name + " konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.");
                form.Invoke((MethodInvoker)delegate { form.Close(); });
                return;
            }
        }

        try
        {
            livePoller = new LivePoller(
                     new PLC(CPU_Type.S7300, Program.configuration.spsIP, Program.configuration.rack,
                         Program.configuration.slot), form);
            livePoller.SPS.Open();
        }
        catch (Exception ex)
        {
            MessageBox.Show("LivePoller konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            log.Error("LivePoller konnte nicht gestartet werden. Verbindung prüfen. Das Programm schließt sich.");
            form.Invoke((MethodInvoker)delegate { form.Close(); });
        }
    }

    /// <summary>Startet alle Poller</summary>
    public void StartPollers()
    {
        foreach (var poller in pollers)
        {
            var error = poller.Value.CheckConnection();
            if (error != ErrorCode.NoError)
            {
                log.Error("Poller " + poller.Key + " konnte nicht gestartet werden. Fehler: " + error);
                continue;
            }

            poller.Value.cancellationTokenSource = new CancellationTokenSource();
            poller.Value.run(poller.Value.config.anfDelay);
        }

        var lerror = livePoller.CheckConnection();
        if (lerror != ErrorCode.NoError)
        {
            log.Error("LivePoller konnte nicht gestartet werden. Fehler: " + lerror);
        }

        livePoller.cancellationTokenSource = new CancellationTokenSource();
        livePoller.run();
    }

    /// <summary>Stoppt alle Poller</summary>
    /// <returns></returns>
    public async Task StopPollers()
    {
        foreach (var poller in pollers.Values) poller.cancellationTokenSource.Cancel();
        livePoller.cancellationTokenSource.Cancel();

        var tasks = pollers.Values.Select(poller => poller.PollingTask).ToArray();
        tasks.Append(livePoller.PollingTask);
        await Task.WhenAll(tasks);
        log.Info("Alle Poller gestoppt.");
    }

    /// <summary>
    ///     Verbindet alle Poller neu
    ///     Wartet x ms zwischen den Versuchen
    ///     Versucht x mal die Verbindung herzustellen
    /// </summary>
    /// <returns></returns>
    public async Task ReconnectPollersAsync()
    {
        var maxRetries = Program.configuration.retryCount;
        var delay = Program.configuration.retryDelay;

        Utils.Utils.intercept = true;

        await StopPollers();

        for (var i = 1; i < maxRetries + 1; i++)
        {
            var allConnected = true;
            //Programm beenden nach x Fehlversuchen
            //Textbox aktualisieren
            log.Warn("Verbindungsaufbau fehlgeschlagen. Versuche es erneut in " + delay + "ms");
            form.Invoke((MethodInvoker)delegate
            {
                form.getConnectLabel().Text = i + "/" + maxRetries +
                                              " Verbindungsaufbau fehlgeschlagen.\nVersuche es erneut in " + delay +
                                              "ms";
                form.getConnectLabel().ForeColor = Color.Red;
            });
            if (i >= maxRetries)
            {
                Utils.Utils.closeInvoked = true;
                var dialogResult = MessageBox.Show("Die Verbindung konnte nicht hergestellt werden. Das Programm beendet sich.", "Fehler", MessageBoxButtons.OK);
                log.Error("Die Verbindung konnte nicht hergestellt werden. Das Programm beendet sich.");
                form.Invoke((MethodInvoker)delegate { form.Close(); });
                break;
            }


            //LivePoller verbindung herstellen;
            var liveResult = livePoller.SPS.Reconnect();
            if (liveResult != ErrorCode.NoError)
            {
                log.Warn("LivePoller konnte nicht verbunden werden.");
                allConnected = false;
            }

            //Poller verbindungen herstellen
            foreach (var poller in pollers.Values)
            {
                var result = poller.SPS.Reconnect();
                if (result != ErrorCode.NoError)
                {
                    log.Warn("Poller " + poller.config.name + " konnte nicht verbunden werden.");
                    allConnected = false;
                    break;
                }
            }

            if (allConnected)
            {
                Utils.Utils.intercept = false;
                log.Info("Alle Poller erfolgreich neu verbunden.");
                StartPollers();
                break;
            }

            await Task.Delay(delay);
        }
    }

    /// <summary>Startet das Monitoring</summary>
    public void StartMonitoring()
    {
        isMonitoring = true;
        monitorTask = Task.Run(() => MonitorPollers());
    }

    /// <summary>Überwacht die Poller, bei Verbindungsfehler wird neu verbunden</summary>
    private void MonitorPollers()
    {
        while (isMonitoring)
        {
            if (livePoller.Status == ErrorCode.ConnectionError || (livePoller.Status == ErrorCode.IPAdressNotAvailable && !isReconnecting))
            {
                isReconnecting = true;
                ReconnectPollersAsync().ContinueWith(t =>
                {
                    isReconnecting = false;
                });
            }

            foreach (var poller in pollers.Values)
                if (poller.Status == ErrorCode.ConnectionError ||
                    (livePoller.Status == ErrorCode.IPAdressNotAvailable && !isReconnecting))
                {
                    isReconnecting = true;
                    ReconnectPollersAsync().ContinueWith(t =>
                    {
                        isReconnecting = false;
                    });
                    break;
                }

            Thread.Sleep(1000);
        }
    }
}