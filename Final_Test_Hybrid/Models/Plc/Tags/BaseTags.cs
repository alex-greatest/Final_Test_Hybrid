namespace Final_Test_Hybrid.Models.Plc.Tags;

public static class BaseTags
{
    public const string PcOn = "ns=3;s=\"PC_ON\"";
    public const string Sb3011 = "ns=3;s=\"30SB11\"";
    public const string PneuValveEv31AirOn = "ns=3;s=\"DB_PneuValve\".\"EV3_1\".\"AirOn\"";
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
}
