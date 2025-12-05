using Final_Test_Hybrid.Models;

namespace Final_Test_Hybrid.Services;

public interface ISequenceExcelService
{
    void SaveSequence(string path, List<SequenceRow> rows);
    List<SequenceRow> LoadSequence(string path, int columnCount);
}

