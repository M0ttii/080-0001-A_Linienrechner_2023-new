using Linienrechner.Klassen;
using Linienrechner.Klassen.FileProcessors;
using Linienrechner.Klassen.Pollers;
using Linienrechner.Klassen.Utils;
using S7;

namespace Linienrechner;

public partial class Form1 : Form
{
    private readonly object reconnectLock = new();
    private readonly Dictionary<string, IniReader> iniWorkers = new();
    private LivePoller livePoller;
    private readonly Dictionary<string, Poller> pollers = new();
    private PollManager pollManager;
    private bool reconnectInitiated;

    /// <summary>Konstruktor</summary>
    public Form1()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Eventhandler, wird aufgerufen, wenn das Form geladen wurde
    /// Prüft die Konfiguration und startet die Poller
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Form1_Shown(object sender, EventArgs e)
    {
        checkConfigs();
        loadUI();

        pollManager = new PollManager(iniWorkers, this);
        pollManager.StartPollers();
        pollManager.StartMonitoring();

        label_adress.Text = Program.configuration.spsIP;
    }

    private void loadUI()
    {
        Date_Label.Text = DateTime.Now.ToString("dd.MM.yyyy");
        SetupDataGridView();
    }

    /// <summary>Passt das DataGridView an die größe des UI's und die Anzahl der Kanäle an</summary>
    /// <param name="dgv"></param>
    private void sizeDGV(DataGridView dgv)
    {
        var states = DataGridViewElementStates.None;

        var totalHeight = dgv.Rows.GetRowsHeight(states) + dgv.ColumnHeadersHeight;
        totalHeight += dgv.Rows.Count * 4; // Ihre Korrektur

        var totalWidth = dgv.Columns.GetColumnsWidth(states) + dgv.RowHeadersWidth - 40;

        // Maximale Höhe für 5 Zeilen berechnen
        var maxHeight = dgv.ColumnHeadersHeight;
        for (var i = 0; i < 5; i++) maxHeight += dgv.RowTemplate.Height;


        // Setzen der Größe mit Berücksichtigung der maximalen Höhe
        if (totalHeight > maxHeight)
        {
            dgv.ScrollBars = ScrollBars.Vertical;
            var scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
            dgv.Size = new Size(totalWidth + scrollBarWidth, maxHeight);
        }
        else
        {
            dgv.ScrollBars = ScrollBars.None;
            dgv.Size = new Size(totalWidth, totalHeight);
        }
    }

    /// <summary>Initialisiert das DataGridView</summary>
    private void SetupDataGridView()
    {
        dataGridView1.ColumnCount = 3;

        dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = Color.Navy;
        dataGridView1.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        dataGridView1.ColumnHeadersDefaultCellStyle.Font =
            new Font(dataGridView1.Font, FontStyle.Bold);

        dataGridView1.AutoSizeRowsMode =
            DataGridViewAutoSizeRowsMode.DisplayedCellsExceptHeaders;

        dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        dataGridView1.ScrollBars = ScrollBars.Vertical;

        dataGridView1.ReadOnly = true;

        dataGridView1.ColumnHeadersBorderStyle =
            DataGridViewHeaderBorderStyle.Single;
        dataGridView1.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        dataGridView1.GridColor = Color.Black;
        dataGridView1.RowHeadersVisible = false;

        dataGridView1.Columns[0].Name = "Kanal";
        dataGridView1.Columns[1].Name = "Letzte XML-Datei";
        dataGridView1.Columns[2].Name = "Uhrzeit";

        dataGridView1.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        dataGridView1.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

        dataGridView1.SelectionMode =
            DataGridViewSelectionMode.FullRowSelect;
        dataGridView1.MultiSelect = false;
        sizeDGV(dataGridView1);
    }

    /// <summary>Fügt eine Zeile in das DataGridView hinzu</summary>
    /// <param name="name"></param>
    public void registerRow(string name)
    {
        Invoke((MethodInvoker)delegate
        {
            dataGridView1.Rows.Add(name);
            sizeDGV(dataGridView1);
            dataGridView1.PerformLayout();
        });
    }

    /// <summary>Fügt einen Eintrag in das DataGridView hinzu</summary>
    /// <param name="name"></param>
    /// <param name="path"></param>
    public void addLatestXML(string name, string path)
    {
        Invoke((MethodInvoker)delegate
        {
            // Durchlaufe alle Zeilen im DataGridView
            foreach (DataGridViewRow row in dataGridView1.Rows)
                // Prüfe, ob die aktuelle Zeile den gesuchten Namen hat
                if (row.Cells["Kanal"].Value != null && row.Cells["Kanal"].Value.ToString() == name)
                {
                    // Setze den XML-Pfad in die entsprechende Zelle
                    row.Cells["Letzte XML-Datei"].Value = path;
                    row.Cells["Uhrzeit"].Value = DateTime.Now.ToString(); // Optional: Setze das aktuelle Datum

                    break; // Beende die Schleife, wenn die Zeile gefunden wurde
                }
        });
    }

    /// <summary>Prüft ob alle Notwendigen Konfigurationen vorhanden sind</summary>
    private void checkConfigs()
    {
        foreach (var config in Program.configuration.configs)
        {
            if (!File.Exists(config.iniPath))
            {
                MessageBox.Show("Die INI Datei " + config.iniPath + " existiert nicht.", "Connection Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            if (!File.Exists(config.xmlOptions.xmlTemplatePath))
            {
                MessageBox.Show("Die XML Datei " + config.xmlOptions.xmlTemplatePath + " existiert nicht.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            if (!File.Exists(Program.configuration.logConfigPath))
            {
                MessageBox.Show("Die Config für den Logger " + config.xmlOptions.xmlTemplatePath + " existiert nicht.",
                    "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }

            try
            {
                iniWorkers.Add(config.name, new IniReader(config));
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ein Kanal mit dem Namen " + config.name + " existiert bereits.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
                return;
            }
        }
    }

    /// <summary>Versucht, aller Poller neu zu verbinden</summary>
    public void Reconnect()
    {
        lock (reconnectLock)
        {
            if (reconnectInitiated) return;
            var cancellationTokenSource = new CancellationTokenSource();
            var token = cancellationTokenSource.Token;
            var listener = Task.Factory.StartNew(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    var maxRetries = Program.configuration.retryCount;
                    var delay = Program.configuration.retryDelay;

                    reconnectInitiated = true;
                    Utils.intercept = true;

                    foreach (var poller in pollers) poller.Value.cancellationTokenSource.Cancel();

                    livePoller.cancellationTokenSource.Cancel();

                    for (var i = 1; i < maxRetries + 1; i++)
                    {
                        if (i == maxRetries + 1)
                        {
                            Utils.closeInvoked = true;
                            var dialogResult =
                                MessageBox.Show(
                                    "Die Verbindung konnte nicht hergestellt werden. Das Programm beendet sich.",
                                    "Fehler", MessageBoxButtons.OK);

                            Close();

                            break;
                        }

                        Invoke((MethodInvoker)delegate
                        {
                            getConnectLabel().Text = i + "/" + maxRetries +
                                                     " Verbindungsaufbau fehlgeschlagen.\nVersuche es erneut in " +
                                                     delay + "ms";
                        });
                        var liveResult = livePoller.SPS.Reconnect();
                        if (liveResult == ErrorCode.NoError)
                        {
                            livePoller.cancellationTokenSource = new CancellationTokenSource();
                            livePoller.run();
                        }

                        foreach (var poller in pollers)
                        {
                            var result = poller.Value.SPS.Reconnect();
                            if (result == ErrorCode.NoError)
                            {
                                Utils.intercept = false;
                                poller.Value.cancellationTokenSource = new CancellationTokenSource();
                                poller.Value.run(poller.Value.config.anfDelay);
                                reconnectInitiated = false;
                                break;
                            }
                        }

                        Thread.Sleep(delay);
                    }
                }
            }, token);
        }
    }

    public Label getConnectLabel()
    {
        return connect_label;
    }

    public NotifyIcon GetNotifyIcon()
    {
        return notifyIcon1;
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e)
    {
        if (pollers != null) Utils.cancellationTokenSourceLabel.Cancel();
    }

    private void Form1_Resize(object sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized) Hide();
        else Show();
    }

    private void notifyIcon1_DoubleClick(object sender, EventArgs e)
    {
        Show();
        WindowState = FormWindowState.Normal;
    }

    private void Form1_Load(object sender, EventArgs e)
    {
    }
}