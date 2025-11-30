namespace GameOverlay.Models
{
    public class Config
    {
        // Paramètres de la bulle Kikimeter
        public double KikimeterBubbleOpacity { get; set; } = 1.0;
        public double KikimeterBubbleSize { get; set; } = 60;
        public int KikimeterBubbleX { get; set; } = -1;
        public int KikimeterBubbleY { get; set; } = -1;
        
        // Paramètres de la bulle Loot
        public double LootBubbleOpacity { get; set; } = 1.0;
        public double LootBubbleSize { get; set; } = 60;
        public int LootBubbleX { get; set; } = -1;
        public int LootBubbleY { get; set; } = -1;
        
        // Paramètres de fond opaque Kikimeter
        public bool KikimeterWindowBackgroundEnabled { get; set; } = false;
        public string KikimeterWindowBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut
        public double KikimeterWindowBackgroundOpacity { get; set; } = 1.0;
        
        // Orientation du Kikimeter (false = vertical, true = horizontal)
        public bool KikimeterHorizontalMode { get; set; } = false;
        
        // Paramètres de fond opaque Loot
        public bool LootWindowBackgroundEnabled { get; set; } = false;
        public string LootWindowBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut
        public double LootWindowBackgroundOpacity { get; set; } = 1.0;
        
        // Paramètres de fond opaque PlayerWindow (mode individuel)
        public bool PlayerWindowBackgroundEnabled { get; set; } = false;
        public string PlayerWindowBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut
        public double PlayerWindowBackgroundOpacity { get; set; } = 1.0;
        
        // Couleur des rectangles cyan (sections de joueurs/loots)
        public string KikimeterSectionBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut, opaque
        public string LootSectionBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut, opaque
        public string PlayerWindowSectionBackgroundColor { get; set; } = "#FF000000"; // Noir par défaut, opaque
        
        // Opacité forcée des cadres
        public bool KikimeterPlayerFramesOpaque { get; set; } = false;
        public bool LootFramesOpaque { get; set; } = false;
        
        // Chemins des fichiers de log (configurables)
        public string? KikimeterLogPath { get; set; }
        public string? LootChatLogPath { get; set; }
        
        // Couleur d'accent du thème (en hexadécimal, ex: "#FF00BFFF")
        public string? AccentColorHex { get; set; }
        
        // Couleur de fond des bulles overlay (en hexadécimal, ex: "#FF1A1A1A")
        public string BubbleBackgroundColor { get; set; } = "#FF1A1A1A"; // Fond sombre par défaut
        
        // Démarrage automatique au lancement de Windows
        public bool StartWithWindows { get; set; } = false;
        
        // Paramètres de la fenêtre Web
        public string WebWindowUrl { get; set; } = "https://www.google.com";
        public int WebWindowX { get; set; } = -1;
        public int WebWindowY { get; set; } = -1;
        public double WebWindowWidth { get; set; } = 800;
        public double WebWindowHeight { get; set; } = 600;
        
        // Opacité de la fenêtre Web
        public double WebWindowOpacity { get; set; } = 1.0;
        public double WebView2Opacity { get; set; } = 1.0;
        
        // Style de la fenêtre Web
        public bool WebWindowBackgroundEnabled { get; set; } = true;
        public string WebWindowBackgroundColor { get; set; } = "#FF1A1A1A"; // Fond sombre par défaut
        public double WebWindowBackgroundOpacity { get; set; } = 1.0;
        public string WebWindowTitleBarColor { get; set; } = "#FF2A2A2A"; // Barre de titre
    }
}


