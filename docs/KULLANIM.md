# EO-Light 5 — Kullanım kılavuzu

EO-Light, aynı yerel ağdaki desteklenen ampulleri bilgisayarından yönetir.
Ampuller arasında seçim yaparken diğerlerinin bağlantısı açık kalır.

Uygulama açıkça klima/termostat türü duyuran bazı ağ cihazlarını da ayrı bir
sayfada gösterebilir. Bu aşamada gerçek klimayı kontrol eden sürücü yoktur;
model için sürücü eklenene kadar sıcaklık, mod ve fan ayarları pasif görünür.
Yalnızca ağda görünmesi cihazın uzaktan kontrol edilebileceği anlamına gelmez.

## Başlangıç

1. **EO-Light.exe** dosyasını aç.
2. Uygulama açılışta ağı bir kez tarar. Birkaç saniye içinde bulunan ampuller
   soldaki **Ampullerim** listesine eklenir ve her birine ayrı bağlantı kurulur.
3. Kontrol etmek istediğin ampule tıkla. Sağdaki panel yalnızca o ampule aittir.

Bilgisayar ve ampuller aynı yerel ağda bulunmalı. Yeelight ampullerinde
**LAN Control / LAN Kontrolü** açık olmalı. Ampul fiziksel olarak elektrik almalıdır.
Uygulamadan ışığı kapatmak bağlantıyı kapatmaz.

## Ampul seçimi ve isimler

- Soldaki her kart, o ampulün açık/kapalı ve bağlı/çevrimdışı durumunu gösterir.
- Seçim yapmak diğer ampulleri kapatmaz veya bağlantılarını kesmez.
- Seçili ampulün başlığındaki **ayar simgesinden** adını değiştirebilirsin:
  örneğin “Çalışma lambası”, “Masa lambası”.
- İsimler EO-Light içinde saklanır; ampulün diğer uygulamalardaki adı değişmez.
- IP adresi ve cihaz kimliği yalnızca bu ayrıntı penceresinde gösterilir.

## Işık kontrolü

- **Güç düğmesi:** seçili ampulü açar/kapatır.
- **Parlaklık:** %1–100 arasında ayarlanır. Kaydırıcı bırakıldığında komut gönderilir.
- **Beyaz tonu:** destekleyen ampullerde Sıcak (2700 K), Doğal (4000 K),
  Soğuk (6500 K) veya sıcaklık kaydırıcısı kullanılır.
- **Renkler:** RGB destekli ampullerde hazır renklerden ya da özel renk seçicisinden seçim yapılır.
- Desteklenmeyen kontroller görünür fakat pasiftir. Sabit beyaz ampule
  yazılımla RGB veya ayarlanabilir beyaz tonu eklenemez.

“Son komut yanıtı” süresi, ampulün ağ üzerinden verdiği yanıtın süresidir;
fiziksel ışık geçişinin ölçümü değildir. Aç/kapat komutunda ek geçiş animasyonu yoktur.

## Dört hızlı ayar

Her ampulün **kendine ait dört parlaklık kaydı** vardır.

1. Parlaklığı istediğin yüzdeye getir.
2. Kaydedeceğin yuvanın sağındaki **disket simgesine** tıkla.
3. Daha sonra yuvadaki yüzdeye tıklayarak bu parlaklığı uygula ve ampulü aç.

Disket düğmesi yalnızca kayıt yapar; ayrıca ampule komut göndermez.
Başka bir ampul seçtiğinde o ampulün kayıtları gösterilir.

## Tarama ve bağlantı

- Otomatik ağ taraması yalnızca uygulama açıldığında yapılır.
- Sonraki taramalar için **Yeniden tara** düğmesine basmalısın.
- Ampul seçimi, parlaklık değişikliği veya pencereyi yeniden göstermek tarama başlatmaz.
- IP değiştiğinde yeniden tara. Ampul, sabit cihaz kimliğiyle tanınır;
  isim ve kayıtları korunarak yeni adresine bağlanılır.
- Bağlantı kesilirse yalnızca kayıtlı adrese yeniden bağlanma denenir.
  Bu işlem ağı yeniden taramaz.
- Ampullerin durum bildirimleri dinlenir; ayrıca 30 saniyede bir bağlı ampullerden
  durum okunur. Bu da cihaz keşfi/tarama değildir.
- Bir ampulün çevrimdışı olması diğer ampullerin komutlarını bekletmez.

**Ağdaki diğer cihazlar** son taramada SSDP/UPnP ve belirli mDNS servisleriyle
bulunan cihazları listeler. Her akıllı cihaz kendini aynı şekilde duyurmaz;
bu liste ağdaki bütün cihazların eksiksiz envanteri değildir.
Şimdilik ışık kontrolü Yeelight LAN protokolüyle yapılır. Diğer markaların
kontrolü için ayrıca o markaya uygun bağlantı desteği gerekir.

## Tema ve sistem tepsisi

- Üstteki **güneş simgesi** koyu ve açık tema arasında geçiş yapar.
- **—** düğmesi uygulamayı saatin yanındaki sistem tepsisine gizler.
  Bağlantılar açık kalır.
- Tepsideki lamba simgesine çift tıklayarak pencereyi yeniden açabilirsin.
  Sağ tık menüsünde **EO-Light’ı aç** ve **Çıkış** bulunur.
- **×** düğmesi uygulamadan çıkar.

## Dosyalar

```text
EO-Light.exe             Uygulama
KULLANIM.md              Bu kılavuz
Ayarlar/                 İlk çalıştırmada oluşur
  eo-light-v5.json       Ampuller, adlar, kayıtlar, seçim ve tema
```

Klasörü yazılabilir bir konumda tut. Taşırken `Ayarlar` klasörünü de taşırsan
tercihlerin korunur. Eski uygulamanın `Ayarlar/ayarlar.json` dosyası yeni
uygulamanın aynı klasöründe veya bir üst uygulama klasöründe varsa,
ilk açılışta eski ampul ve dört parlaklık kaydı içe alınır. Eski dosya değiştirilmez.

## Ampul bulunamıyorsa

1. Ampulün fiziksel elektriğini, Wi-Fi bağlantısını ve LAN kontrolünü kontrol et.
2. Bilgisayarı ve ampulü aynı yerel ağa bağla. Misafir ağı istemcileri ayırabilir.
3. **Yeniden tara** düğmesine bas.
4. Windows güvenlik duvarında EO-Light için özel ağ erişimine izin verildiğini kontrol et.

Uygulama Windows 10/11 x64 ve WebView2 çalışma zamanı kullanır.
Kontroller bulut hesabına ihtiyaç duymaz; iletişim yerel ağ üzerinden yapılır.

## Teşekkür

Çoklu ampul bağlantısı ve keşif yaklaşımında
[EmreOzhan/smart-gadget](https://github.com/emreozhan/smart-gadget) projesinden
yararlanılmıştır. MIT lisans bildirimi kaynak depodaki
`third-party/smart-gadget-LICENSE.txt` dosyasındadır.
