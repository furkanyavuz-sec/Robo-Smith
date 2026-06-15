// GamePhase.cs
// Görev: Oyun fazları ve zorluk seviyesi enum'ları.
// Tüm sistemlerin ortak dili — tek dosyada tanımlanır.

public enum GamePhase
{
    Lobby,          // Oyun başlamadı
    Preparation,    // 10 dakika — garajda üretim
    Arena,          // 2 dakika  — robotlar savaşır
    Overtime,       // 2 dk bitti, robotlar hâlâ hayatta
    GameOver        // Maç bitti
}

public enum Difficulty
{
    Easy,           // Director AI %50 hızda
    Normal,         // Director AI %100 hızda
    Hard            // Director AI %150 hızda
}