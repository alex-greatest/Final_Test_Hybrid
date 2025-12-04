using System.Collections.Generic;

namespace Final_Test_Hybrid.Models
{
    public class SequenceRow
    {
        public List<string> Columns { get; set; } = new List<string>();
        public string CssClass { get; set; } = "";

        public SequenceRow() { }

        public SequenceRow(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Columns.Add("");
            }
        }
    }
}

