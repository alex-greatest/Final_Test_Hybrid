using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Services.Editor;

public interface ISequenceExcelService
{
    void SaveSequence(string path, List<SequenceRow> rows);
    List<SequenceRow> LoadSequence(string path, int columnCount);
    Task SaveSequenceAsync(string path, List<SequenceRow> rows);
    Task<List<SequenceRow>> LoadSequenceAsync(string path, int columnCount);
}

