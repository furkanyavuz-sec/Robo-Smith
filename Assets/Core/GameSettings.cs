// GameSettings.cs
// Görev: Sahneler arası taşınan oyun ayarları (statik).
// Ana menüde seçilen zorluk buradan GameManager'a akar.
// MonoBehaviour değil — sahne bağımsız, kablo gerektirmez.

public static class GameSettings
{
    /// <summary>Ana menüde zorluk seçildi mi? (Editörden direkt Play'de false kalır)</summary>
    public static bool DifficultyChosen = false;

    public static Difficulty SelectedDifficulty = Difficulty.Normal;

    public static void SetDifficulty(Difficulty difficulty)
    {
        SelectedDifficulty = difficulty;
        DifficultyChosen   = true;
    }
}
