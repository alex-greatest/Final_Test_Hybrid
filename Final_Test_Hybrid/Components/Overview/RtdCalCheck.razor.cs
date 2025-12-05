using Final_Test_Hybrid.Components.Base;

namespace Final_Test_Hybrid.Components.Overview;

public partial class RtdCalCheck : GridInplaceEditorBase<RtdCalCheck.RtdCalCheckItem>
{
    protected override void OnInitialized()
    {
        Items = new List<RtdCalCheckItem>
        {
            new() { PlcTag = "TAG", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "CH_TMR", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "CH_TRR", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "DHW_TES", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
            new() { PlcTag = "DHW_TUS", Type = "Pt100", Raw = 21.700, Multiplier = 1.000, Offset = 0.000, Calculated = 21.700 },
        };
    }

    public class RtdCalCheckItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double Raw { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
        public double Calculated { get; set; }
    }
}
