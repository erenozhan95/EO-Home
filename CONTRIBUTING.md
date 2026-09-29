# Katkıda bulunma

Hata bildirimi, kullanım geri bildirimi, dokümantasyon ve kod katkıları kabul edilir.

## Geliştirme

1. Repoyu fork et ve değişikliğin için bir dal aç.
2. Windows üzerinde `./scripts/build.ps1` çalıştır.
3. Değişiklikten sonra `./scripts/test.ps1` çalıştır.
4. Pull request'te problemi, yeni davranışı ve yaptığın doğrulamayı anlat.

## Proje kuralları

- Mevcut .NET Framework derleyicisiyle uyumlu C# sözdizimini koru.
- Ağ işlemlerini arayüz iş parçacığında bekletme.
- Komut gönderme veya yeniden bağlantı sırasında ağ taraması başlatma.
- Cihazın RGB/Kelvin yeteneklerini varsayma.
- Gerçek ampullerin durumunu değiştiren otomatik test ekleme; loopback sahte cihazları kullan.
- Kişisel ayar dosyalarını, cihaz kimliklerini, MAC adreslerini, anahtarları ve derlenmiş dosyaları commit etme.
- Üçüncü taraf kod kullanıyorsan kaynak ve lisans bildirimlerini koru.

Bir test bu ortamda çalışmadıysa bunu PR açıklamasında belirt; yalnızca
derlemenin geçmesi fiziksel cihaz davranışının doğrulandığı anlamına gelmez.
