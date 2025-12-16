namespace Final_Test_Hybrid.Components.Engineer.StandDatabase;

public static class RussianPluralization
{
    public static string GetRecipeWord(int count)
    {
        if (count % 100 >= 11 && count % 100 <= 19)
        {
            return "рецептов";
        }
        return GetRecipeWordBySuffix(count % 10);
    }

    private static string GetRecipeWordBySuffix(int suffix) =>
        suffix switch
        {
            1 => "рецепт",
            2 or 3 or 4 => "рецепта",
            _ => "рецептов"
        };
}
