using System.Diagnostics;
using log4net;
using S7;

namespace Linienrechner.Klassen.Pollers;

/// <summary>
///     Poller welcher das LiveBit setzt und zurücksetzt
/// </summary>
internal class LivePoller
{
    private static readonly ILog log = LogManager.GetLogger(typeof(Poller));
    public CancellationTokenSource cancellationTokenSource = new();
    public Form1 form1;
    public PLC SPS { get; set; }
    public Task PollingTask { get; private set; }
    public ErrorCode Status { get; private set; }

    /// <summary>Konstruktor</summary>
    /// <param name="SPS"></param>
    /// <param name="form1"></param>
    public LivePoller(PLC SPS, Form1 form1)
    {
        this.SPS = SPS;
        this.form1 = form1;
    }


    public ErrorCode CheckConnection()
    {
        return SPS.IsAvailable
            ? SPS.IsConnected ? ErrorCode.NoError : ErrorCode.ConnectionError
            : ErrorCode.IPAdressNotAvailable;
    }

    /// <summary>Startet den LivePoller und setzt das LiveBit bzw. setzt es zurück</summary>
    public void run()
    {
        Status = ErrorCode.NoError;
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
                        var liveByte = Program.configuration.liveByte;
                        var liveBit = Program.configuration.liveBit;

                        byte[] result = null;
                        try
                        {
                            Utils.Utils.intercept = false;
                            result = SPS.ReadBytes(DataType.DataBlock, Program.configuration.liveDB, liveByte, 1);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }

                        var resultBit = GetBit(result[0], liveBit);
                        switch (resultBit)
                        {
                            case true:
                                result[0] = SetBit(result[0], false, liveBit);
                                SPS.WriteBytes(DataType.DataBlock, Program.configuration.liveDB, liveByte, result);
                                break;
                            case false:
                                result[0] = SetBit(result[0], true, liveBit);
                                SPS.WriteBytes(DataType.DataBlock, Program.configuration.liveDB, liveByte, result);
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Status = ErrorCode.ConnectionError;
                    Debug.WriteLine(e.Message);
                }

                Thread.Sleep(Program.configuration.liveDelay);
            }
        }, token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    }

    /// <summary>Liest ein Bit aus einem Byte</summary>
    /// <param name="b"></param>
    /// <param name="bitNumber"></param>
    /// <returns></returns>
    private bool GetBit(byte b, int bitNumber)
    {
        return (b & (1 << bitNumber)) != 0;
    }

    /// <summary>Setzt ein Bit in einem Byte</summary>
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
}