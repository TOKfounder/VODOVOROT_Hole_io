namespace Vodovorot.RoundExperience
{
    public static class SkinAppearanceText
    {
        private static readonly string[] Russian =
        {
            "Красный корпус и синяя спираль.",
            "Светлый корпус без узора и синяя спираль.",
            "Яркий жёлтый корпус и синяя спираль.",
            "Тёмный полосатый узор и синяя спираль.",
            "Золотистые полосы и синяя спираль."
        };
        private static readonly string[] English =
        {
            "Red body and a blue spiral.",
            "Plain pale body and a blue spiral.",
            "Bright yellow body and a blue spiral.",
            "Dark striped pattern and a blue spiral.",
            "Golden stripes and a blue spiral."
        };
        public static string Description(int index, bool ru) => (ru ? Russian : English)[index];
        public static string CosmeticPromise(bool ru) => ru
            ? "Меняется внешний вид. Скорость и рост одинаковые."
            : "Appearance changes. Speed and growth stay the same.";
    }
}
