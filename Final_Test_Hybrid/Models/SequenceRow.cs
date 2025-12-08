namespace Final_Test_Hybrid.Models
{
    public class SequenceRow
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public List<string> Columns { get; set; } = [];
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

