# GameOverlay.Themes

Bibliothèque de gestion de thèmes pour Amaliassistant. Permet de modifier la couleur d'accent (brun/marron par défaut : #FF6E5C2A) partout dans l'application.

## Utilisation

### Changer la couleur d'accent

```csharp
using GameOverlay.Themes;

// Depuis un code hexadécimal
ThemeManager.SetAccentColorFromHex("#FF00FF"); // Magenta
ThemeManager.SetAccentColorFromHex("FF00FF");  // Sans le #

// Depuis les composantes RGB
ThemeManager.SetAccentColor(255, 0, 255); // Magenta

// Depuis une Color WPF
ThemeManager.AccentColor = System.Windows.Media.Color.FromRgb(255, 0, 255);
```

### Obtenir la couleur d'accent

```csharp
// En tant que Color WPF
var color = ThemeManager.AccentColor;

// En tant que SolidColorBrush
var brush = ThemeManager.AccentBrush;

// En tant que System.Drawing.Color
var drawingColor = ThemeManager.AccentDrawingColor;

// En tant que string hexadécimal
var hex = ThemeManager.AccentHex; // "#FFFF00FF"
var hexShort = ThemeManager.AccentHexShort; // "#FF00FF"

// Avec opacité
var brushWithOpacity = ThemeManager.GetAccentBrushWithOpacity(0.5);
```

### Écouter les changements

```csharp
ThemeManager.AccentColorChanged += (sender, e) =>
{
    // La couleur d'accent a changé, mettre à jour l'UI
    UpdateUIWithNewColor(e.NewColor);
};
```

### Réinitialiser au brun/marron par défaut

```csharp
ThemeManager.ResetToDefault(); // Remet RGB(110, 92, 42) = #FF6E5C2A
```

### Couleur de survol

```csharp
// Automatiquement calculée depuis la couleur d'accent
var hoverBrush = ThemeManager.HoverBrush;
var hoverColor = ThemeManager.HoverColor;
```

## Remplacement des couleurs hardcodées

Au lieu d'utiliser :
```csharp
// ❌ Ne pas faire
var accentColor = System.Windows.Media.Color.FromRgb(110, 92, 42);
var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(110, 92, 42));
```

Utiliser :
```csharp
// ✅ Faire
using GameOverlay.Themes;
var color = ThemeManager.AccentColor;
var brush = ThemeManager.AccentBrush;
```

Dans les fichiers XAML, remplacer :
```xml
<!-- ❌ Ne pas faire -->
<Setter Property="BorderBrush" Value="#FF00BFFF"/>

<!-- ✅ Faire -->
<Setter Property="BorderBrush" Value="{x:Static themes:ThemeManager.AccentHex}"/>
```

Note: Pour utiliser dans XAML, il faut définir un namespace dans App.xaml ou le fichier XAML concerné.







