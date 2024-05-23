namespace Linienrechner.Klassen.Utils;

internal class Utils
{
    /// <summary>States für die Anzeige</summary>
    public enum State
    {
        Polling,
        Execute,
        Idling,
        Connecting,
        Connected
    }

    /// <summary>Variablendeklaration</summary>
    public static CancellationTokenSource cancellationTokenSourceLabel = new();

    public static CancellationToken tokenLabel = cancellationTokenSourceLabel.Token;

    public static List<string> activeInstances = new();

    public static bool intercept = false;
    public static bool closeInvoked = false;

    /// <summary>Ändert den Status des NotifyIcons</summary>
    /// <param name="form1"></param>
    /// <param name="state"></param>
    public static void changeNotifyState(Form1 form1, State state)
    {
        if (state == State.Polling)
        {
            if (!form1.IsDisposed)
                form1.Invoke(() =>
                {
                    form1.GetNotifyIcon().Text = "Linienrechner - Polling";
                    form1.GetNotifyIcon().Icon = new Icon("iconpolling.ico");
                });
            return;
        }

        if (state == State.Idling)
        {
            if (!form1.IsDisposed)
                form1.Invoke(() =>
                {
                    form1.GetNotifyIcon().Text = "Linienrechner - Idling";
                    form1.GetNotifyIcon().Icon = new Icon("iconidling.ico");
                    ;
                });
        }
    }

    /// <summary>Ändert den Status des Titels</summary>
    /// <param name="form1"></param>
    /// <param name="state"></param>
    public static void changeTitleState(Form1 form1, State state)
    {
        if (intercept) return;
        if (state == State.Polling)
        {
            changeNotifyState(form1, State.Polling);
            if (!form1.IsDisposed)
            {
                form1.Invoke(() =>
                {
                    form1.getConnectLabel().Text = "Polling - Verbindung IO";
                    form1.getConnectLabel().ForeColor = Color.FromArgb(35, 186, 103);
                });
                return;
            }
        }

        if (state == State.Execute)
        {
            changeNotifyState(form1, State.Execute);

            if (!form1.IsDisposed)
                form1.Invoke(() =>
                {
                    form1.getConnectLabel().Text = "Retrieving Data from SPS.";
                    form1.getConnectLabel().ForeColor = Color.FromArgb(35, 186, 103);
                });
            if (state == State.Connecting)
            {
                changeNotifyState(form1, State.Connecting);

                if (!form1.IsDisposed)
                    form1.Invoke(() =>
                    {
                        form1.getConnectLabel().Text = "Connecting.";
                        form1.getConnectLabel().ForeColor = Color.FromArgb(35, 186, 103);
                    });
            }
        }
    }

    /// <summary>Registriert eine Instanz</summary>
    /// <param name="form1"></param>
    /// <param name="name"></param>
    public static void registerInstance(Form1 form1, string name)
    {
        if (!activeInstances.Contains(name))
        {
            activeInstances.Add(name);
            form1.registerRow(name);
        }
    }

    /// <summary>Entfernt eine Instanz</summary>
    /// <param name="form1"></param>
    /// <param name="name"></param>
    public static void unregisterInstance(Form1 form1, string name)
    {
        if (activeInstances.Contains(name)) activeInstances.Remove(name);
    }
}