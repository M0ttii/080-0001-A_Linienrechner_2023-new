using System.Collections;
using System.Diagnostics;
using System.Text;
using Linienrechner.Klassen.FileProcessors;
using Linienrechner.Klassen.Model;
using log4net;
using S7;
using S7.Types;
using DateTime = System.DateTime;

namespace Linienrechner.Klassen.Pollers;

internal class Poller
{
    /// <summary>Prozesszustände für Polling</summary>
    public enum ProcessState
    {
        Polling,
        Processing,
        Finished
    }

    private static readonly ILog log = LogManager.GetLogger(typeof(Poller));

    private byte[] buffer;
    private byte[] buffer2;
    private readonly int buffer2StartAdr = 2000;
    private byte[] buffer3;
    private readonly int buffer3StartAdr = 1576;

    public CancellationTokenSource cancellationTokenSource;
    public ChannelConfig config;
    private string detail = "nA";

    public bool error;

    private string fehlerText = "nA";

    //Variablendeklaration
    public Form1 form1;
    private readonly IniReader iniReader;

    private string? listEntry;
    private string[]? listSplit;
    public PollManager pollManager;
    public ProcessState processState = ProcessState.Polling;
    private readonly string station = "EM_L###Linienkennung###_STATION_";

    /// <summary>Konstruktor</summary>
    /// <param name="form1"></param>
    /// <param name="iniReader"></param>
    /// <param name="config"></param>
    /// <param name="pollManager"></param>
    public Poller(Form1 form1, IniReader iniReader, ChannelConfig config, PollManager pollManager)
    {
        this.form1 = form1;
        this.iniReader = iniReader;
        this.config = config;
        cancellationTokenSource = new CancellationTokenSource();

        Utils.Utils.registerInstance(this.form1, this.config.name);
        this.pollManager = pollManager;
    }

    public PLC SPS { get; set; }
    public Task PollingTask { get; private set; }

    public ErrorCode Status { get; private set; }

    public ErrorCode CheckConnection()
    {
        return SPS.IsAvailable
            ? SPS.IsConnected ? ErrorCode.NoError : ErrorCode.ConnectionError
            : ErrorCode.IPAdressNotAvailable;
    }

    /// <summary>Bit aus Byte auslesen</summary>
    /// <param name="b"></param>
    /// <param name="bitNumber"></param>
    /// <returns></returns>
    private bool GetBit(byte b, int bitNumber)
    {
        return (b & (1 << bitNumber)) != 0;
    }

    /// <summary>Bit in einem Byte setzen</summary>
    /// <param name="b"></param>
    /// <param name="value"></param>
    /// <param name="bitPosition"></param>
    /// <returns></returns>
    private byte SetBit(byte b, bool value, int bitPosition)
    {
        switch (value)
        {
            case true:
                return (byte)(b | (1 << bitPosition));
            case false:
                return (byte)(b & ~(1 << bitPosition));
        }
    }

    /// <summary>Setzte das anfBit und das quitBit dieses Pollers auf 0</summary>
    public void resetBit()
    {
        var result = SPS.ReadBytes(DataType.DataBlock, config.anfDB, config.anfByte, 1);
        result[0] = SetBit(result[0], false, config.quitBit);
        result[0] = SetBit(result[0], false, config.anfBit);
        var answer = SPS.WriteBytes(DataType.DataBlock, config.anfDB, config.anfByte, result);
        if (answer != ErrorCode.NoError)
        {
            MessageBox.Show("Konnte SPS nicht beschreiben.", "Write Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            log.Error(config.name + ": " + "Konnte SPS nicht beschreiben. Fehler: " + answer);
            Environment.Exit(0);
        }
    }

    /// <summary>
    ///     Startet den Pollingvorgang
    ///     Wartet bis das anfBit auf 1 gesetzt wird
    ///     Ruft this.executeProcess(); auf, um den Auslese- und Kopiervorgang zu starten
    ///     Setzt danach das Quittierbit auf 1, wartet bis die SPS das anfBit auf 0 setzt und setzt das Quittierbit auf 0
    /// </summary>
    /// <param name="delay"></param>
    public void run(int delay)
    {
        resetBit();
        Status = ErrorCode.NoError;
        Utils.Utils.changeTitleState(form1, Utils.Utils.State.Polling);
        var token = cancellationTokenSource.Token;
        PollingTask = Task.Factory.StartNew(() =>
        {
            while (!token.IsCancellationRequested)
            {
                var tEC = CheckConnection();
                if (tEC == ErrorCode.ConnectionError || tEC == ErrorCode.IPAdressNotAvailable) Status = tEC;

                try
                {
                    if (!(Status == ErrorCode.ConnectionError || Status == ErrorCode.IPAdressNotAvailable))
                    {
                        var db = config.DB;
                        var pByte = config.anfByte;
                        var pBit = config.anfBit;
                        byte[] result = null;
                        try
                        {
                            Utils.Utils.intercept = false;
                            result = SPS.ReadBytes(DataType.DataBlock, config.anfDB, pByte, 1);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }

                        var bit = GetBit(result[0], pBit);
                        //Warten auf PollingBit = 1
                        if (bit)
                            if (processState == ProcessState.Polling)
                            {
                                //Auslese- und Kopiervorgang starten
                                if (!token.IsCancellationRequested) executeProcess();
                                if (error) break;
                                //Quittierbit auf 1 setzen
                                if (!token.IsCancellationRequested)
                                {
                                    result[0] = SetBit(result[0], true, config.quitBit);
                                    var answer = SPS.WriteBytes(DataType.DataBlock, config.anfDB, pByte, result);
                                    processState = ProcessState.Polling;
                                    if (answer != ErrorCode.NoError)
                                    {
                                        MessageBox.Show("Konnte SPS nicht beschreiben.", "Write Error",
                                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        log.Error(
                                            config.name + ": " + "Konnte SPS nicht beschreiben. Fehler: " + answer);
                                        break;
                                    }
                                }
                            }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }

                Thread.Sleep(delay);
            }
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>Wandelt ein Byte-Array in ein DateTime-Objekt um</summary>
    /// <param name="byteArray"></param>
    /// <returns></returns>
    public DateTime? getDTFromBA(byte[] byteArray)
    {
        var dtString = string.Format("{0}.{1}.{2} {3}:{4}:{5}",
            byteArray[2].ToString("X"),
            byteArray[1].ToString("X"),
            byteArray[0].ToString("X"),
            byteArray[3].ToString("X"),
            byteArray[4].ToString("X"),
            byteArray[5].ToString("X"));

        var dt = new DateTime();
        if (dtString == "0.0.0 0:0:0") return null;
        try
        {
            dt = DateTime.Parse(dtString);
        }
        catch (Exception ex)
        {
            log.Error("Konnte Datum nicht parsen. " + dtString);
        }

        return dt;
    }

    /// <summary>Liest einen Wert aus der SPS aus</summary>
    /// <param name="valueArray"></param>
    /// <returns></returns>
    public object Read(string[] valueArray)
    {
        var db = config.DB;
        var type = valueArray[0];
        var byteAdress = valueArray[1];
        try
        {
            switch (type.ToLower())
            {
                case "date_and_time":
                    var result = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 6);
                    var dateTime = getDTFromBA(result);
                    if (valueArray.Length == 4)
                        if (dateTime != null)
                        {
                            var realDateTime = (DateTime)dateTime;
                            var timeToAdd = Convert.ToInt32(valueArray[3]);
                            dateTime = realDateTime.AddMilliseconds(timeToAdd);
                            return realDateTime.ToString("dd.MM.yyyy HH:mm:ss");
                        }

                    if (dateTime != null)
                    {
                        var realDateTime = (DateTime)dateTime;
                        return realDateTime.ToString("dd.MM.yyyy HH:mm:ss");
                    }

                    return "0.0.0 0:0:0";

                case "string":
                    var stringLength = SPS.Read(DataType.DataBlock, db, Convert.ToInt32(byteAdress), VarType.Byte, 1);
                    return SPS.Read(DataType.DataBlock, db, Convert.ToInt32(byteAdress), VarType.String,
                            Convert.ToInt32(valueArray[2]))
                        .ToString()
                        .Replace("\0", string.Empty).Trim();

                case "char":
                    var charResult = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 1);
                    if (valueArray.Length == 3)
                    {
                        listEntry = iniReader.getStringLists().ElementAt(Convert.ToInt32(valueArray[2]) - 1).Value
                            .ToString();
                        listSplit = listEntry.Split("|");
                        return listSplit[Convert.ToInt32(Convert.ToInt16(charResult))];
                    }

                    return Encoding.UTF8.GetString(charResult);

                case "bool":
                    var adressSplit = valueArray[1].Split(".");
                    var bytee = adressSplit[0];
                    if (valueArray.Length == 4)
                    {
                        var startBit = Convert.ToInt16(adressSplit[1]);
                        var countBit = Convert.ToInt16(valueArray[2]);
                        var numbers = Enumerable.Range(startBit, countBit).ToArray();
                        var res = new List<bool>();
                        foreach (var number in numbers)
                        {
                            var bitt = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(bytee), 1);
                            res.Add(GetBit(bitt[0], number));
                        }

                        var bitResult = addBits(res);
                        listEntry = iniReader.getStringLists().ElementAt(Convert.ToInt32(valueArray[3]) - 1).Value
                            .ToString();
                        listSplit = listEntry.Split("|");
                        if (bitResult == 3) bitResult = bitResult - 1;
                        return listSplit[bitResult];
                    }

                    if (valueArray.Length == 3)
                    {
                        var byteResult = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(adressSplit[0]), 1);
                        var bit = GetBit(byteResult[0], Convert.ToInt16(adressSplit[1]));
                        listEntry = iniReader.getStringLists().ElementAt(Convert.ToInt32(valueArray[2]) - 1).Value
                            .ToString();
                        listSplit = listEntry.Split("|");
                        return listSplit[Convert.ToInt32(bit)];
                    }

                    return GetBit(SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(bytee), 1)[0],
                        Convert.ToInt16(adressSplit[1]));

                case "int":
                    var returnInt = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 2);
                    return Int.FromByteArray(returnInt);

                case "dbw":
                    var returnWord = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 2);
                    if (valueArray.Length == 3)
                    {
                        listEntry = iniReader.getStringLists().ElementAt(Convert.ToInt32(valueArray[2]) - 1).Value
                            .ToString();
                        listSplit = listEntry.Split("|");
                        return listSplit[Word.FromByteArray(returnWord)];
                    }

                    return Word.FromByteArray(returnWord);

                case "dbb":
                    return SPS.Read(DataType.DataBlock, db, Convert.ToInt32(byteAdress), VarType.Byte, 2);

                //NOCHMAL PRÜFEN OB DAS DIE RICHTIGE KONVERTIERUNG IST
                case "float":
                    var rawReal = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 4);
                    if (BitConverter.IsLittleEndian) Array.Reverse(rawReal);
                    if (valueArray.Length == 3)
                    {
                        var value = BitConverter.ToSingle(rawReal);
                        switch (valueArray[2][0])
                        {
                            case '*':
                                return value * Convert.ToInt32(valueArray[2].Substring(1));
                            case '/':
                                return value / Convert.ToInt32(valueArray[2].Substring(1));
                            default:
                                return value;
                        }
                    }

                    return BitConverter.ToSingle(rawReal);
                case "dbd":
                    var dword = SPS.ReadBytes(DataType.DataBlock, db, Convert.ToInt32(byteAdress), 4);
                    if (valueArray.Length == 3)
                    {
                        listEntry = iniReader.getStringLists().ElementAt(Convert.ToInt32(valueArray[2]) - 1).Value
                            .ToString();
                        listSplit = listEntry.Split("|");
                        return listSplit[Convert.ToInt32(dword)];
                    }

                    return DWord.FromByteArray(dword);
            }

            return null;
        }
        catch (Exception ex)
        {
            MessageBox.Show("Konnte nicht auslesen.", "Read Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            log.Error("Konnte nicht auslesen. Fehler: " + ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Liest die Werte aus der SPS aus und fügt sie in die Platzhalterliste ein
    ///     Übergibt diese Liste an das XmlTemplateProcessor, welches die Werte in die XML-Datei einfügt
    /// </summary>
    public void executeProcess()
    {
        processState = ProcessState.Processing;
        fillStaticVars();

        foreach (var platzhalter in iniReader.getPlatzhalterRaw())
        {
            var value = platzhalter.Value;
            var valueArray = value.Split(',');

            try
            {
                var result = Read(valueArray);
                iniReader.getPlatzhalterReal().Add(platzhalter.Key, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }

        //Linienkennung
        foreach (var platzhalter in iniReader.getSpecialPlatzhalter())
        {
            var value = platzhalter.Value;
            if (platzhalter.Key == "###Linienkennung###")
            {
                var result = Read(value.Split(','));
                var lastNumber = (ushort)result % 10;
                iniReader.getPlatzhalterReal().Add(platzhalter.Key, lastNumber);
            }

            if (platzhalter.Key == "###Nacharbeit_OK###")
            {
                var result = (bool)Read(value.Split(','));
                iniReader.getPlatzhalterReal().Add(platzhalter.Key, result);
            }

            if (platzhalter.Key == "###Letzte_AST###")
            {
                var result = Read(value.Split(','));
                iniReader.getPlatzhalterReal().Add(platzhalter.Key, result);
            }

            if (value == "ReadClock")
                iniReader.getPlatzhalterReal().Add(platzhalter.Key, DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss"));
        }

        //Special Operations
        var ionio = SPS.ReadBytes(DataType.DataBlock, config.DB, 26, 1);
        if (GetBit(ionio[0], 1))
        {
            log.Info(config.name + ": " + "Daten Anforderung " + iniReader.getPlatzhalterReal()["###Platzhalter###_2"]);
            var copyTool = new XmlTemplateProcessor(iniReader.getPlatzhalterReal(), config, true, form1, pollManager);
            copyTool.replace();
            if (copyTool.error) error = true;
            processState = ProcessState.Finished;
            iniReader.getPlatzhalterReal().Clear();
            return;
        }

        log.Info(config.name + ": " + "Daten Anforderung " + iniReader.getPlatzhalterReal()["###Platzhalter###_2"]);
        var copyTool1 = new XmlTemplateProcessor(iniReader.getPlatzhalterReal(), config, false, form1, pollManager);
        copyTool1.replace();
        if (copyTool1.error) error = true;
        processState = ProcessState.Finished;
        iniReader.getPlatzhalterReal().Clear();
    }

    /// <summary>Füllt die statischen Variablen mit vordefinierten Werten</summary>
    private void fillStaticVars()
    {
        //Fill buffers with byte-ranges
        buffer = SPS.ReadBytes(DataType.DataBlock, config.DB, 0, 150);
        buffer2 = SPS.ReadBytes(DataType.DataBlock, config.DB, buffer2StartAdr, 20);
        buffer3 = SPS.ReadBytes(DataType.DataBlock, config.DB, buffer3StartAdr, 200);
        var buffer3Add = SPS.ReadBytes(DataType.DataBlock, config.DB, buffer3StartAdr + 200, 160);
        buffer3 = buffer3.Concat(buffer3Add).ToArray();


        //Station
        var stationNumber = getStation();
        var errors = getFehler();
        if (errors != null && stationNumber != null)
        {
            fehlerText = errors.FehlerText;
            detail = errors.Detail;

            iniReader.getPlatzhalterReal().Add("###Platzhalter###_502", stationNumber);
            iniReader.getPlatzhalterReal().Add("###Platzhalter###_503", buffer[17]);
            iniReader.getPlatzhalterReal().Add("###Platzhalter###_504", fehlerText);
            iniReader.getPlatzhalterReal().Add("###Platzhalter###_505", detail);
        }
    }

    /// <summary>Addiert die Bits in einem Byte-Array</summary>
    /// <param name="list"></param>
    /// <returns></returns>
    public static int addBits(List<bool> list)
    {
        var bitArray = new BitArray(list.Count);
        for (var i = 0; i < list.Count; i++) bitArray[i] = list[i];
        var bytes = new byte[1];
        bitArray.CopyTo(bytes, 0);
        return bytes[0];
    }

    /// <summary>Wandelt ein Byte-Array in ein Float-Objekt um</summary>
    /// <param name="byteArray"></param>
    /// <returns></returns>
    private float DWordToFloat(byte[] byteArray)
    {
        if (BitConverter.IsLittleEndian) Array.Reverse(byteArray);
        return BitConverter.ToSingle(byteArray, 0);
    }

    /// <summary>Gibt die Station zurück</summary>
    /// <returns></returns>
    private string getStation()
    {
        //Station
        if (GetBit(buffer[42], 1)) return station + "1";
        if (GetBit(buffer[42], 3)) return station + "2";
        if (GetBit(buffer[42], 5)) return station + "3";
        if (GetBit(buffer[42], 7)) return station + "4";
        if (GetBit(buffer[43], 1)) return station + "5";
        if (GetBit(buffer[45], 1)) return station + "13";
        if (GetBit(buffer[45], 3)) return station + "14";
        if (GetBit(buffer[45], 7)) return station + "16";
        if (GetBit(buffer[43], 3)) return station + "6";
        if (GetBit(buffer[43], 5)) return station + "7";
        if (GetBit(buffer[43], 7)) return station + "8";
        return "Unbekannt";
    }

    /// <summary>Gibt einen FehlerObjekt zurück, jenachdem welche Bits gesetzt sind</summary>
    /// <returns></returns>
    private ErrorDetailObject getFehler()
    {
        if (GetBit(buffer[42], 1))
        {
            if (GetBit(buffer[27], 5))
                return new ErrorDetailObject("Handlingfehler WT-Nr: " + buffer2[17], "Unbekannt");
            if (GetBit(buffer[27], 7)) return new ErrorDetailObject("Fehler beim Spannen", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 6)) return new ErrorDetailObject("Versteller nicht entriegelbar", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 7)) return new ErrorDetailObject("Versteller nicht verriegelbar", "Unbekannt");
            var eob = new ErrorDetailObject();
            if (GetBit(buffer3[279], 0))
            {
                eob.FehlerText = "V-Spiel";

                byte[] byteArray = { buffer3[300], buffer3[301], buffer3[302], buffer3[303] };
                var value = DWordToFloat(byteArray);
                value = value / 1000;
                eob.Detail = value + " Grad";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[139 * 2 + 1], 1))
            {
                eob = new ErrorDetailObject();
                eob.FehlerText = "Reibmoment";

                byte[] byteArray = { buffer3[304], buffer3[305], buffer3[306], buffer3[307] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[279], 2))
            {
                eob = new ErrorDetailObject();
                eob.FehlerText = "Federmoment M1";

                byte[] byteArray = { buffer3[288], buffer3[289], buffer3[290], buffer3[291] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[139 * 2 + 1], 3))
            {
                eob.FehlerText = "Federmoment M2";

                byte[] byteArray = { buffer3[292], buffer3[293], buffer3[294], buffer3[295] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }
            }

            if (GetBit(buffer3[139 * 2 + 1], 3))
            {
                eob.FehlerText = "Verstellwinkel";

                byte[] byteArray = { buffer3[296], buffer3[297], buffer3[298], buffer3[299] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Grad";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }
            }

            if (GetBit(buffer3[278 + 1], 5))
            {
                eob.FehlerText = "V-Spiel-Auswertung fehlerhaft";
                eob.Detail = "Unbekannt";
                return eob;
            }

            if (GetBit(buffer3[278 + 1], 6))
            {
                eob.FehlerText = "Verstellwinkel-Auswertung fehlerhaft";
                eob.Detail = "Unbekannt";
                return eob;
            }

            if (GetBit(buffer3[278 + 1], 7))
            {
                eob.FehlerText = "GFM Zeitüberschreitung";
                eob.Detail = "Unbekannt";
                return eob;
            }

            if (GetBit(buffer3[278], 1))
            {
                eob.FehlerText = "Typkennung falsch";
                eob.Detail = "Unbekannt";
                return eob;
            }

            if (GetBit(buffer3[278], 2))
            {
                eob.FehlerText = "Simodrive Zeitüberschreitung";
                eob.Detail = "Unbekannt";
                return eob;
            }
            //return eob;
        }

        //Station 2
        if (GetBit(buffer[42], 3))
        {
            if (GetBit(buffer[27], 5))
                return new ErrorDetailObject("Handlingfehler WT-Nr: " + buffer2[17] + "00", "Unbekannt");
            if (GetBit(buffer[27], 7)) return new ErrorDetailObject("Fehler beim Spannen", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 6)) return new ErrorDetailObject("Versteller nicht entriegelbar", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 7)) return new ErrorDetailObject("Versteller nicht verriegelbar", "Unbekannt");
            var eob = new ErrorDetailObject();
            if (GetBit(buffer3[139 * 2 + 1], 0))
            {
                eob.FehlerText = "V-Spiel";
                byte[] byteArray = { buffer3[300], buffer3[301], buffer3[302], buffer3[303] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Grad";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }
            }

            if (GetBit(buffer3[278 + 1], 1))
            {
                eob.FehlerText = "Reibemoment";
                byte[] byteArray = { buffer3[304], buffer3[305], buffer3[306], buffer3[307] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[278 + 1], 2))
            {
                eob.FehlerText = "Federmoment M1";
                byte[] byteArray = { buffer3[188], buffer3[189], buffer3[190], buffer3[191] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[278 + 1], 3))
            {
                eob.FehlerText = "Federmoment M2";
                byte[] byteArray = { buffer3[292], buffer3[293], buffer3[294], buffer3[295] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Nm";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[278 + 1], 3))
            {
                eob.FehlerText = "Verstellwinkel";
                byte[] byteArray = { buffer3[296], buffer3[297], buffer3[298], buffer3[299] };
                var word = DWordToFloat(byteArray);
                word = (ushort)(word / 1000);
                eob.Detail = word + " Grad";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[139 * 2 + 1], 5))
                return new ErrorDetailObject("V-Spiel-Auswertung fehlerhaft", "Unbekannt");
            if (GetBit(buffer3[139 * 2 + 1], 6))
                return new ErrorDetailObject("Verstellwinkel-Auswertung fehlerhaft", "Unbekannt");
            if (GetBit(buffer3[139 * 2 + 1], 7)) return new ErrorDetailObject("GFM Zeitüberschreitung", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 1)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
            if (GetBit(buffer3[139 * 2], 2)) return new ErrorDetailObject("Simodrive Zeitüberschreitung", "Unbekannt");
        }

        //Station 3
        if (GetBit(buffer[42], 5))
        {
            if (GetBit(buffer[27], 5)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer[27], 7)) return new ErrorDetailObject("Fehler beim Spannen", "Unbekannt");
            if (GetBit(buffer3[158 * 2 + 1], 2))
                return new ErrorDetailObject("Versteller nicht entriegelbar", "Unbekannt");
            if (GetBit(buffer3[158 * 2 + 1], 4) && !GetBit(buffer3[158 * 2 + 1], 5))
                return new ErrorDetailObject("Versteller verklemmt bzw. Schlupf", "Prüfwinkel nicht erreicht");
            if (GetBit(buffer3[158 * 2 + 1], 4) && GetBit(buffer3[158 * 2 + 1], 5))
                return new ErrorDetailObject("Versteller verklemmt bzw. Schlupf", "Nullwinkel nicht erreicht");
            if (GetBit(buffer3[158 * 2 + 1], 3))
                return new ErrorDetailObject("Versteller nicht verriegelbar", "Unbekannt");
            if (GetBit(buffer3[158 * 2 + 1], 4)) return new ErrorDetailObject("Ehrler Zeitüberschreitung", "Unbekannt");
            var eob = new ErrorDetailObject();
            if (GetBit(buffer3[158 * 2], 7))
            {
                eob.FehlerText = "Leckage zu hoch";
                byte[] byteArray = { buffer3[336], buffer3[337], buffer3[338], buffer3[339] };
                var word = DWordToFloat(byteArray);
                eob.Detail = word + " l/min";

                if (GetBit(buffer[26], 6))
                {
                    eob.Detail = eob.Detail + " Nacharbeit mit DMC";
                    return eob;
                }

                if (GetBit(buffer[27], 1))
                {
                    eob.Detail = eob.Detail + " Nacharbeits-Bauteil";
                    return eob;
                }

                if (GetBit(buffer[26], 7))
                {
                    eob.Detail = eob.Detail + " nach Doppelmessung";
                    return eob;
                }

                return eob;
            }

            if (GetBit(buffer3[158 * 2], 5)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
            if (GetBit(buffer3[158 * 2], 6)) return new ErrorDetailObject("Simodrive Zeitüberschreitung", "Unbekannt");
        }

        //Station 4
        if (GetBit(buffer[42], 7))
        {
            if (GetBit(buffer2[5], 0)) return new ErrorDetailObject("Fügefehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[5], 1)) return new ErrorDetailObject("Typkennung falsch", "Unbekannt");
        }

        //Station 5
        if (GetBit(buffer[43], 1))
        {
            if (GetBit(buffer2[5], 4)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[5], 5)) return new ErrorDetailObject("Keine Reibfolie vorhanden", "Unbekannt");
            if (GetBit(buffer2[5], 6)) return new ErrorDetailObject("Reibfolie zu dünn", "Unbekannt");
            if (GetBit(buffer2[5], 7))
                return new ErrorDetailObject("Reibfolie zu dick, verbogen bzw. dopelt", "Unbekannt");
            if (GetBit(buffer2[6], 0)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
        }

        //Station 13
        if (GetBit(buffer[45], 1))
        {
            if (GetBit(buffer2[6], 2))
                return new ErrorDetailObject("Handlingfehler Laser 1", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[6], 3))
                return new ErrorDetailObject("Handlingfehler Laser 2", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[6], 4))
                return new ErrorDetailObject("Handlingfehler Laser 3", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[6], 5)) return new ErrorDetailObject("Fehler beim Spannen Laser 1", "Unbekannt");
            if (GetBit(buffer2[6], 6)) return new ErrorDetailObject("Fehler beim Spannen Laser 2", "Unbekannt");
            if (GetBit(buffer2[6], 7)) return new ErrorDetailObject("Fehler beim Spannen Laser 3", "Unbekannt");
            if (GetBit(buffer2[7], 0)) return new ErrorDetailObject("Laserabbruch Laser 1", "Unbekannt");
            if (GetBit(buffer2[7], 1)) return new ErrorDetailObject("Laserabbruch Laser 2", "Unbekannt");
            if (GetBit(buffer2[7], 2)) return new ErrorDetailObject("Laserabbruch Laser 3", "Unbekannt");
            if (GetBit(buffer2[7], 3)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
        }

        //Station 14
        if (GetBit(buffer[45], 3))
        {
            if (GetBit(buffer2[8], 1))
                return new ErrorDetailObject("Kamera Zeitüberschreitung", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[8], 2)) return new ErrorDetailObject("Strukturierung falsch", "Unbekannt");
        }

        //Station 16
        if (GetBit(buffer[45], 7))
        {
            if (GetBit(buffer2[8], 6)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[8], 7)) return new ErrorDetailObject("Laserabbruch", "Unbekannt");
            if (GetBit(buffer2[9], 0)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
            if (GetBit(buffer2[9], 1)) return new ErrorDetailObject("Simodrive Zeitüberschreitung", "Unbekannt");
        }

        //Station 6
        if (GetBit(buffer[43], 3))
        {
            if (GetBit(buffer2[9], 3)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[9], 4)) return new ErrorDetailObject("Laserabbruch", "Unbekannt");
            if (GetBit(buffer2[9], 5)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
            if (GetBit(buffer2[9], 6)) return new ErrorDetailObject("BT fehlt (Greifer Fehler)", "Unbekannt");
        }

        //Station 7
        if (GetBit(buffer[43], 5))
        {
            if (GetBit(buffer2[10], 0)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[10], 1)) return new ErrorDetailObject("DMC nicht lesbar", "Unbekannt");
            if (GetBit(buffer2[10], 2)) return new ErrorDetailObject("DMC falsch", "Unbekannt");
            if (GetBit(buffer2[10], 3)) return new ErrorDetailObject("Typerkennung falsch", "Unbekannt");
            if (GetBit(buffer2[10], 4)) return new ErrorDetailObject("BT fehlt (Greifer Fehler)", "Unbekannt");
        }

        //Station 8
        if (GetBit(buffer[43], 7))
        {
            if (GetBit(buffer2[10], 5)) return new ErrorDetailObject("Handlingfehler", "WT-Nr: " + buffer2[17] + "00");
            if (GetBit(buffer2[10], 6)) return new ErrorDetailObject("Typkennung falsch", "Unbekannt");
        }

        return new ErrorDetailObject("Kein Fehler", "Unbekannt");
    }
}