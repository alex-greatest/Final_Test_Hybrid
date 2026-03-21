namespace Final_Test_Hybrid.Services.Errors;

internal static class PlcErrorValueNormalizer
{
    internal static bool TryNormalizeBooleanValue(
        object? rawValue,
        out bool normalizedValue,
        out string normalizedType,
        out string normalizationNote)
    {
        normalizedValue = false;
        normalizedType = rawValue?.GetType().FullName ?? "null";
        normalizationNote = "UnsupportedType";

        static bool ReturnEmpty(out string note, string nextNote)
        {
            note = nextNote;
            return false;
        }

        switch (rawValue)
        {
            case bool boolValue:
                normalizedValue = boolValue;
                normalizedType = typeof(bool).FullName!;
                normalizationNote = "DirectBool";
                return true;

            case bool[] boolArray when boolArray.Length == 0:
                return ReturnEmpty(out normalizationNote, "BoolArrayEmpty");

            case bool[] boolArray:
                normalizedValue = boolArray[0];
                normalizedType = typeof(bool[]).FullName!;
                normalizationNote = boolArray.Length == 1
                    ? "BoolArrayLength1"
                    : $"BoolArrayLength{boolArray.Length}UseFirst";
                return true;

            case IEnumerable<bool> boolEnumerable:
                using (var enumerator = boolEnumerable.GetEnumerator())
                {
                    if (!enumerator.MoveNext())
                    {
                        return ReturnEmpty(out normalizationNote, "BoolEnumerableEmpty");
                    }

                    normalizedValue = enumerator.Current;
                    var hasSecondValue = enumerator.MoveNext();
                    normalizedType = boolEnumerable.GetType().FullName
                        ?? "System.Collections.Generic.IEnumerable<System.Boolean>";
                    normalizationNote = hasSecondValue
                        ? "BoolEnumerableLength>1UseFirst"
                        : "BoolEnumerableLength1";
                    return true;
                }
        }

        return false;
    }
}
