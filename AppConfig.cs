namespace Win11_Extra_Clock
{
    internal static class AppConfig
    {
        private static Settings S => Settings.Default;

        public static Models.Position Position
        {
            get => System.Enum.TryParse(S.Position, out Models.Position p) ? p : Models.Position.BottomCenter;
            set { S.Position = value.ToString(); S.Save(); }
        }

        public static double CustomX
        {
            get => S.CustomX;
            set { S.CustomX = value; S.Save(); }
        }

        public static double CustomY
        {
            get => S.CustomY;
            set { S.CustomY = value; S.Save(); }
        }

        public static string Language
        {
            get => string.IsNullOrWhiteSpace(S.Language) ? "auto" : S.Language;
            set { S.Language = string.IsNullOrWhiteSpace(value) ? "auto" : value; S.Save(); }
        }
    }
}