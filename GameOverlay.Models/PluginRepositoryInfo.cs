namespace GameOverlay.Models
{
    /// <summary>
    /// Informations sur un plugin disponible dans le dépôt (GitHub)
    /// </summary>
    public class PluginRepositoryInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string? Changelog { get; set; }
        public bool IsInstalled { get; set; } = false;
        public string? InstalledVersion { get; set; }
        public bool HasUpdate { get; set; } = false;
    }
}





