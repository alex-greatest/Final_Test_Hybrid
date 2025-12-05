using Final_Test_Hybrid.Components.Base;

namespace Final_Test_Hybrid.Components.Overview;

public partial class AiCallCheck : GridInplaceEditorBase<AiCallCheck.AiCallCheckItem>
{
    protected override void OnInitialized()
    {
        Items = Enumerable.Range(0, 11).Select(i => new AiCallCheckItem
        {
            PlcTag = i == 0 ? "BlrSupply" : "Tag_" + i,
            Type = "S[4.20].HW[4.20] mA",
        }).ToList();
    }

    public class AiCallCheckItem
    {
        public string PlcTag { get; set; } = "";
        public string Type { get; set; } = "";
        public double Raw { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public double Multiplier { get; set; }
        public double Offset { get; set; }
        public double Calculated { get; set; }
    }
}
