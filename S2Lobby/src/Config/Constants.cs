namespace S2Lobby
{
    public static class Constants
    {
        public const uint Patchlevel = 11757;
        public const uint VersionMaj = 0;
        public const uint VersionMin = 1;
        public static string VERSION = $"v{VersionMaj}.{VersionMin}-alpha 2022 - 2023";

        public static string MOTD = "Welcome to Die Siedler: Aufbruch der Kulturen! \n" +
                                    " --- you are logged in as %name% --- \n\n" +
                                    " S2 online lobby by zocker_160, cocomed and pnxr \n" +
                                    $"({VERSION}) \n\n" +
                                    "Join our Discord: https://discord.gg/UAXH3VS9Qy \0";
        
        public const string ConfigFileNameDefault = "S2Lobby.exe.config";
        //public const string ConfigFileName = "lobby.config";
        public const string ConfigFileName = "config.xml";

        public const string LoggerFileDefault = "lobby_log.txt";
        public const string LoggerFileDefaultDebug = "lobby_net_dump.txt";
        public const string LoggerTimeFormat = @"MM\/dd\/yyyy hh:mm:ss";
    }
}
