namespace Final_Test_Hybrid.Components.Engineer.StandDatabase.ResultSettings;

public static class RussianPluralization
{
    public static string GetResultSettingWord(int count)
    {
        if (count % 100 >= 11 && count % 100 <= 19)
        {
            return "настроек";
        }
        return GetWordBySuffix(count % 10);
    }

    private static string GetWordBySuffix(int suffix) =>
        suffix switch
        {
            1 => "настройка",
            2 or 3 or 4 => "настройки",
            _ => "настроек"
        };
}
