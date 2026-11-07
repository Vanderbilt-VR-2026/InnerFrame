namespace InnerFrame.Entrance
{
    public enum VisitorRole { Boy, LadyInRed }

    public static class EntranceChoice
    {
        public static VisitorRole Role { get; private set; }
        public static string RoomCode { get; private set; }
        public static void SetRole(VisitorRole role) => Role = role;
        public static bool SetRoomCode(string code)
        {
            if (code == null || code.Length != 4) return false;
            foreach (char c in code) if (c < '0' || c > '9') return false;
            RoomCode = code;
            return true;
        }
    }
}
