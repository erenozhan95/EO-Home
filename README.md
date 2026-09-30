# EO-Home · Yerel akıllı ev kontrolü

EO-Home, evdeki akıllı cihazları tek bir Windows uygulamasında görmeyi ve desteklenenleri yerel ağ üzerinden yönetmeyi amaçlar. Şu anda birden fazla **Yeelight LAN ampulünü aktif olarak kontrol eder**. Klima/termostat türünü açıkça duyuran cihazlar için ayrı bir ekran, sıcaklık–mod–fan arayüzü ve komut doğrulama altyapısı vardır; **gerçek bir klimaya komut gönderen sürücü henüz yoktur**.

Bu depo ilk commit'inde **LightController 4.1** adlı C# sürümünü içeriyordu. Rust/Tauri sürümü önce **EO-Light 5** adıyla geliştirildi; güncel uygulamanın adı **EO-Home**. Eski sürümün kaynakları ve derleme betikleri güncel proje ağacından çıkarılmıştır.

## İndir ve kullan

[Son EO-Home sürümünü](https://github.com/erenozhan95/EO-Home/releases/latest) indir. `EO-Home-Windows.zip` içinde `EO-Home.exe` ve `KULLANIM.md` bulunur; exe ayrıca doğrudan indirilebilir. Windows 10/11 x64 ve WebView2 gerekir. Ampullerle bilgisayar aynı yerel ağda olmalı; Yeelight ampullerinde **LAN Kontrolü** açık olmalı.

[Türkçe kullanım kılavuzu](docs/KULLANIM.md) · [Cihaz sürücüsü tasarımı](docs/DEVICE_DRIVERS.md)

## Cihaz desteği

| Cihaz | Ağda bulma | Uygulamadan kontrol |
| --- | --- | --- |
| Yeelight LAN ampuller | Otomatik keşif ve IP değişince yeniden tarama | Aktif: aç/kapat, parlaklık; cihaz destekliyorsa RGB ve beyaz tonu |
| Klima / termostat | SSDP veya mDNS ile türünü açıkça duyuruyorsa ayrı cihaz sayfası | Sıcaklık, çalışma modu ve fan arayüzü hazır; gerçek cihaz sürücüsü eklenene kadar kontroller pasif |
| Diğer akıllı ağ cihazları | Desteklenen SSDP/mDNS duyuruları listelenir | Markaya ve protokole uygun sürücü gerektiği için şimdilik kontrol yok |

Örneğin birden fazla ampulü aynı anda bağlı tutup soldan seçtiğinin parlaklığını değiştirebilirsin. Bir klima açıkça tanınırsa kendi sayfasında görünür; modeline uygun yerel bağlantı desteği eklenmeden sıcaklık veya fan komutu gönderilmez. Her cihazın ağda bulunması, otomatik olarak kontrol edilebildiği anlamına gelmez.

## Özellikler

- Açılışta bir kez tarama; sonrasında yalnızca **Yeniden tara** düğmesiyle keşif.
- Bulunan ampullerin her biri için bağımsız, kalıcı TCP bağlantısı.
- Ampul seçimi, bağlantı durumunu ve diğer ampullerin ayarlarını değiştirmez.
- Aç/kapat, %1–100 parlaklık, ampul başına dört parlaklık kaydı.
- Donanım destekliyorsa RGB renk ve 1700–6500 K beyaz sıcaklığı.
- Cihaz adı değiştirme, koyu/açık tema ve sistem tepsisine küçültme.
- Cihaz kimliğiyle eşleştirme; IP değiştiğinde isteğe bağlı yeni taramada güncelleme.
- SSDP/UPnP ve belirli mDNS servislerini listeler. Bu cihazlar için genel kontrol sürücüsü bulunmaz.
- Açıkça klima/termostat türü duyuran ağ cihazlarını ayrı sayfada gösterir. Sıcaklık, çalışma modu ve fan ayarları modelin desteklediği özelliklere göre kullanılmak üzere hazırlanmıştır.

## Teknolojiler

| Katman | Teknoloji | Görevi |
| --- | --- | --- |
| Masaüstü | Tauri 2, WebView2 | Windows penceresi, sistem tepsisi ve arayüz köprüsü |
| Arayüz | TypeScript, Vite, CSS, Lucide | Cihaz listesi ve kontroller |
| Yerel servis | Rust, Tokio | Keşif, bağımsız TCP bağlantıları ve komutlar |
| Veri | JSON | Cihaz adları, seçim, tema ve parlaklık kayıtları |

Işık kontrolü için bulut hesabı veya uygulamaya ait bir sunucu gerekmez. Yeni cihaz türlerinin gerçekten kontrol edilebilmesi için ilgili markanın yerel protokolü veya Matter eşleştirmesi, durum okuma ve komut sürücüsü eklenmelidir.

## Kaynak koddan derleme

Windows 10/11 x64 üzerinde Node.js, Rust MSVC toolchain, Microsoft C++ Build Tools, Windows SDK ve WebView2 gerekir. [Tauri önkoşulları](https://v2.tauri.app/start/prerequisites/) kurulduktan sonra depo kökünde:

```powershell
npm ci
npm run tauri build -- --no-bundle
```

Çıktı: `src-tauri/target/release/EO-Home.exe`. Geliştirme için `npm run tauri dev` kullan.

## Testler

```powershell
npm ci
npm run build
cd src-tauri
cargo test --locked --lib --tests
```

Rust testleri sahte TCP ampuller ve örnek ağ adresleri kullanır; evdeki cihazları kontrol etmez. İsteğe bağlı arayüz testi için Vite geliştirme sunucusu çalışırken `node ui-test.mjs` komutunu kullan. Test çıktıları `artifacts/` altında kalır ve depoya eklenmez.

Geliştirme önizlemesindeki `?demo` parametresi örnek ampuller ve örnek klima gösterir; yayımlanan exe'de bu örnek cihazlar etkin değildir.

`scripts/check-public-files.ps1` Git'e eklenen dosyalarda özel ayarları, yerel IP'leri ve kullanıcı klasör yollarını kontrol eder; CI her push'ta çalıştırır.

## Dosya düzeni

```text
src/                 TypeScript arayüzü
src-tauri/src/       Rust uygulaması, keşif ve bağlantılar
src-tauri/tests/     Sahte ampullerle entegrasyon testleri
docs/KULLANIM.md     Kullanım kılavuzu
third-party/         Üçüncü taraf lisans metinleri
```

Kişisel cihaz ayarları, önceki kurulumlarla uyum için dosya adı korunan `Ayarlar/eo-light-v5.json` içinde yalnızca kullanıcının bilgisayarında tutulur; bu dosya Git tarafından yok sayılır. `--diagnose` seçeneğiyle üretilen yerel ağ dökümü de depoya eklenmez.

## Lisans ve teşekkür

Kod [MIT lisanslıdır](LICENSE). [EmreOzhan/smart-gadget](https://github.com/emreozhan/smart-gadget) projesinin keşif ve kalıcı bağlantı yaklaşımından yararlanıldı; ilgili MIT bildirimi [third-party/smart-gadget-LICENSE.txt](third-party/smart-gadget-LICENSE.txt) içindedir. Arayüz simgeleri için Lucide'nin [ISC lisansı](third-party/LUCIDE-LICENSE.txt) korunur.
