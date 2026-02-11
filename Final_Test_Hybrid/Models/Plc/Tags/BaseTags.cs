namespace Final_Test_Hybrid.Models.Plc.Tags;

public static class BaseTags
{
    public const string PcOn = "ns=3;s=\"PC_ON\"";
    public const string Sb3011 = "ns=3;s=\"30SB11\"";
    public const string PneuValveEv31AirOn = "ns=3;s=\"DB_PneuValve\".\"EV3_1\".\"AirOn\"";
    public const string TestAuto = "ns=3;s=\"DB_Station\".\"Test\".\"Auto\"";
    public const string TestAskAuto = "ns=3;s=\"DB_Station\".\"Test\".\"Ask_Auto\"";

    // Error handling tags
    public const string ErrorRetry = "ns=3;s=\"DB_Station\".\"Test\".\"Req_Repeat\"";
    public const string ErrorSkip = "ns=3;s=\"DB_Station\".\"Test\".\"End\"";
    public const string AskRepeat = "ns=3;s=\"DB_Station\".\"Test\".\"Ask_Repeat\"";

    // Reset handling tags
    public const string ReqReset = "ns=3;s=\"DB_Station\".\"Test\".\"Req_Reset\"";
    public const string Reset = "ns=3;s=\"DB_Station\".\"Test\".\"Reset\"";
    public const string AskEnd = "ns=3;s=\"DB_Station\".\"Test\".\"Ask_End\"";

    // Error reset (HMI)
    public const string ErrorQuitt = "ns=3;s=\"DB_HMI\".\"Button\".\"Mode\".\"xSB_Quitt\"";

    /// <summary>PC сигнализирует об ошибке шага без блока</summary>
    public const string Fault = "ns=3;s=\"DB_Station\".\"Test\".\"Fault\"";

    /// <summary>PLC подтверждает Skip для шага без блока</summary>
    public const string TestEndStep = "ns=3;s=\"DB_Station\".\"Test\".\"EndStep\"";

    /// <summary>
    /// Флаг контроля связи HMI. HMI взводит каждые 2 сек, PLC сбрасывает каждые 5 сек.
    /// Если не взведён вовремя - PLC выдаёт ошибку связи.
    /// </summary>
    public const string HmiHeartbeat = "ns=3;s=\"DB_HMI\".\"PLC_Flag\"";
}
