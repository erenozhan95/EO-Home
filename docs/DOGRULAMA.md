# EO-Home 5 — Doğrulama

- Rust: 5 test geçti (özellik doğrulama, keşif ayrıştırma, bağımsız bağlantılar, adres değişimi, eski ayar aktarımı).
- 900 ms gecikmeli sahte ampul, diğer ampulün komutunu bekletmedi.
- Tekrarlanan cihaz seçimi yeni bağlantı açmadı ve tarama başlatmadı.
- Parlaklık kaydı ampule komut göndermedi; kayıtlar cihaz başına ayrı saklandı.
- TypeScript üretim derlemesi geçti.
- Edge arayüz testi: mono/RGB özellik kapıları, cihaz seçimi, bağımsız kayıtlar, isim, tema, 820 px genişlik; JavaScript hatası yok.
- Paketlenmiş Tauri exe: WebView2 arayüzü yüklendi ve Rust IPC hazır sinyali alındı.
- Aç/kapat ve renk komutları sahte TCP ampullerle doğrulandı.
