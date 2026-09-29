# LightController — Kullanım kılavuzu

LightController, ağdaki cihazları keşfeder ve desteklenen ışıkları bilgisayarından kontrol eder. Komutları evindeki yerel ağ üzerinden doğrudan lambaya gönderir; kontrol için internet veya hesap girişi gerekmez.

## Başlatma

**LightController.exe** dosyasına çift tıkla. İlk çalıştırmada Cihazları yönet bölümünden ampulünü seç. Sonraki açılışlarda uygulama kayıtlı lambana bağlanır. Ekranda **Bağlı** yazınca kullanıma hazırdır.

Bilgisayarın ve lamban aynı yerel ağda olmalı. Yeelight telefon uygulamasındaki **LAN Kontrolü** açık, lambanın fiziksel duvar anahtarı da açık kalmalı.

## Işığı açma ve kapatma

- **Işığı aç:** Lambayı açar.
- **Kapat:** Lambanın ışığını söndürür. Uygulama lambayla iletişim kurmaya devam edebilir; tekrar açmak için yeniden bağlantı kurman gerekmez.

Duvar anahtarını kapatırsan lambanın elektriği kesilir ve uygulamadan açamazsın. Elektrik geri geldiğinde uygulama bağlantıyı otomatik yeniden dener.

## Parlaklığı ayarlama

1. Kaydırıcıyla **%1–%100** arasında bir değer seç.
2. **Parlaklığı uygula** düğmesine bas.

Kaydırıcıyı hareket ettirmek tek başına lambayı değiştirmez. Bu düğme kapalı lambaya açma komutu göndermez; ışığı açmak için **Işığı aç** düğmesini kullan.

## Beyaz tonu: sarıdan beyaza

Sağ üstteki **Beyaz tonu** bölümünde üç hazır seçenek bulunur:

- **Sıcak sarı:** 2700 K
- **Doğal beyaz:** 4000 K
- **Soğuk beyaz:** 6500 K

Hazır düğmeler tonu hemen uygular. Ara bir ton için kaydırıcıdan 1700–6500 K arasında bir değer seçip **Beyaz tonunu uygula** düğmesine bas. Renk sıcaklığı, cihazın desteklediği sınırlar dahilinde uygulanabilir.

## RGB renk paleti

Sağ alttaki **Renk paleti** bölümünde bir renk örneği seç veya **Özel renk seç** düğmesini kullan. Ardından **Rengi uygula** düğmesine bas. Seçili renk kodu bölümün sağ üstünde görünür. Renk veya ton değiştirmek kapalı ışığa açma komutu göndermez.

### Ampul desteklemiyorsa

RGB ve beyaz tonu bölümleri her zaman görünür. Kontroller, seçili ampulün bildirdiği desteklenen komutlara göre açılır. Destek yoksa bölüm açıklama göstererek pasif kalır; ampule desteklenmeyen komut gönderilmez. Cihaz bilgisi henüz alınmadıysa destek doğrulanana kadar bekler.

Uygulama sabit renkli bir ampule donanımsal renk özelliği kazandırmaz. Uyumlu bir renkli lamba seçtiğinde ilgili bölümler etkinleşir.

## Dört parlaklık kaydı

Sık kullandığın dört farklı parlaklığı saklayabilirsin.

**Kaydetmek için:**

1. Kaydırıcıdan istediğin yüzdeyi seç.
2. İstediğin yuvanın altındaki **Kaydet** düğmesine bas.
3. Yuvanın üstündeki yüzde güncellenir. Aynı yuvaya tekrar kaydetmek önceki değeri değiştirir.

**Kullanmak için:** Yuvanın üstündeki **yüzde düğmesine** bas. Lamba kayıtlı parlaklığa ayarlanır ve açılır.

Örneğin %15, %35, %65 ve %100 değerlerini dört ayrı yuvaya kaydedebilirsin. Kaydetme işlemi lambanın ışığını değiştirmez. Kayıtların uygulamayı kapatıp açınca korunur.

## Simgeye küçültme ve çıkış

- Pencerenin **—** düğmesi uygulamayı saatin yanındaki bildirim alanına küçültür. Arka planda çalışmaya devam eder.
- Ampul simgesi görünmüyorsa saatin yanındaki **^** düğmesiyle gizli simgelere bak.
- Simgeye **çift tıkla** veya **sağ tık → LightController'ı aç** menüsünü kullanarak pencereyi geri getir.
- **Sağ tık → Çıkış** veya pencerenin **X** düğmesi uygulamayı tamamen kapatır. Uygulamadan çıkmak lambaya kapatma komutu göndermez.

## Ağdaki cihazları bulma

1. **Cihazları yönet** düğmesine bas; açılışta yapılan taramanın sonuçları hemen görünür.
2. Listede bir cihaz seç. Alt bölümde adresi, keşif yöntemi ve kullanılabilir bağlantı seçenekleri görünür.
3. **Işığı kontrol et:** Uyumlu ışığı ana ekrana bağlar; aç/kapat ve parlaklık kontrolleri kullanılabilir.
4. **Cihaz arayüzünü aç:** HTTP/HTTPS hizmeti duyuran cihazın yerel web arayüzünü tarayıcıda açar. Cihaz kendi giriş veya eşleştirme ekranını gösterebilir.

**Yeniden tara** ile listeyi yenileyebilirsin. Uygulama SSDP/UPnP ve mDNS/DNS-SD duyurularını marka bağımsız tarar. Bu listede modem, medya cihazı, bilgisayar/telefon hizmeti ve diğer ağ cihazları da bulunabilir. Yalnızca akıllı ev ürünlerinden oluşan bir liste değildir. Kendini duyurmayan, yalnızca bulutla çalışan veya ayrı ağda bulunan cihazlar görünmeyebilir.

### Keşif ve kontrol desteği

Cihazın bulunması, bütün işlevlerinin LightController tarafından kontrol edilebildiği anlamına gelmez. Işık kontrolü ayrı bir bağlantı modülüdür; şu anda Yeelight LAN protokolü desteklenir. Başka protokoller için uygun kontrol modülü ve gerektiğinde cihaz eşleştirmesi gerekir. Desteklenmeyen cihazlara ışık komutu gönderilmez.

Web hizmeti veya ışık kontrolü olmayan cihazlarda bağlantı durumu açıklanır; kontrol düğmeleri etkinleşmez. Ağ taraması ışığı açıp kapatmaz veya ayarlarını değiştirmez.

### IP değişirse

Işığı listeden seçince cihaz kimliği ve adresi kaydedilir. Açılıştaki ilk taramada aynı kimlik eşleştirilir. Uygulama açıkken IP değişirse Cihazları yönet → Yeniden tara düğmesine bas. Başka lambaya otomatik geçilmez. Eski sürümden MAC kimliğiyle kayıtlı bir lambayı yeniden listeden seçmek, cihazın kendi kimliğini kaydetmeyi sağlar.

Yeelight bağlantı modülü önce çoklu yayın, ardından yerel IPv4 bölümüne doğrudan UDP keşif paketleri gönderir. Gerekirse sınırlı bir kontrol portu taraması ve salt okunur durum sorgusu kullanılır. Genel SSDP/mDNS taraması bu lamba modülünden ayrıdır.

En alttaki **Işığa IP ile bağlan**, yalnızca Yeelight LAN için elle bağlantıdır; otomatik IP takibi sağlamaz. Dört parlaklık kaydı uygulama genelinde ortaktır ve lamba değiştirince korunur.

## Bağlantının yenilenmesi

Uygulama yaklaşık her 10 saniyede seçili ışığın durumunu kontrol eder. Bağlantı koparsa yalnızca kayıtlı adrese yeniden bağlanmayı dener; ağ taraması başlatmaz. Telefonla yaptığın değişiklikler de ekrana yansır. Kaydırıcıdaki henüz uygulanmamış yüzde korunur.

## Bir şey çalışmazsa

| Durum | Ne yapabilirsin? |
| --- | --- |
| Bağlantı bekleniyor | Birkaç saniye bekle. Duvar anahtarını, aynı ağda olduğunu ve LAN Kontrolü'nün açık olduğunu kontrol et. |
| Bağlantı hâlâ kurulmuyor | Cihazlar bölümünde Yeniden tara'ya basıp lambanı tekrar seç. VPN veya misafir ağı cihaz keşfini engelleyebilir. |
| Komut doğrulanamadı | Komut uygulanmış olabilir; uygulama yanıt alamamıştır. Lambanın durumunu kontrol edip gerekirse tekrar dene. |
| Parlaklık değişmedi | Kaydırıcıdan sonra Parlaklığı uygula'ya bastığını kontrol et. Lamba kapalıysa aç. |
| Kayıt kaydedilemedi | Uygulama klasörünün yazılabilir olduğundan emin ol. ZIP dosyasının içinden çalıştırıyorsan önce klasörü dışarı çıkar. |

## Klasörde neler var?

```text
LightController.exe     → Uygulama; açmak için çift tıkla
KULLANIM.md      → Bu kılavuz
Ayarlar/        → Kayıtlı parlaklıklar, cihaz kimliği ve lamba adresi
third-party/    → Üçüncü taraf lisans bildirimleri
```

**Ayarlar/ayarlar.json** dosyası kayıtlarını saklar. Uygulamayı başka bir yere taşırken klasörün tamamını taşı; böylece ayarların da yanında gider. Kaynak kod repodaki src klasöründedir; normal kullanımda bu klasöre ihtiyaç yoktur.

## Ağ taraması ne zaman yapılır? (4.1)

- Uygulama açılırken bir kez.
- Sen **Cihazları yönet → Yeniden tara** düğmesine bastığında.

Cihaz listesini açmak, aç/kapat yapmak, parlaklığı değiştirmek, bağlantıyı yeniden kurmak ve arka plan durum sorguları ağ taraması başlatmaz. Aç/kapat komutları doğrudan seçili cihazın bilinen adresine gönderilir. Uygulama açıkken IP değişirse listeyi elle yeniden tara.
