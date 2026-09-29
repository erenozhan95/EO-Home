# Mimari ve ağ iletişimi

## Uygulama katmanları

`LightWindow`, WinForms ana penceresidir. `LightController.cs` uygulama akışını,
`Dashboard.cs` ise aynı sınıfın görsel bileşenlerini `partial` sınıf yapısıyla tutar.
Ana pencere tek bir `ILightController` oturumu yönetir.

| Dosya / sınıf | Sorumluluk |
| --- | --- |
| `Device` | IP, port, kimlik, model ve desteklenen komutlar |
| `Preferences` | JSON ayar okuma, doğrulama ve geçici dosyayla kaydetme |
| `LampSession` | TCP bağlantısı, JSON istek kimliği, yanıt ve hata işleme |
| `Discovery` | Yeelight duyurularını ayrıştırma ve uyumlu ampulleri bulma |
| `IDiscoveryProvider` | Farklı keşif yöntemlerinin ortak arayüzü |
| `NetworkDiscovery` | Sağlayıcıları çalıştırma, sonuçları birleştirme ve önbellekleme |
| `DevicePicker` | Son tarama sonuçları, yeniden tarama, elle IPv4 girişi |
| `CardPanel`, `ModernButton`, `ModernSlider`, `LampGlyph` | GDI+ ile çizilen kontroller |

## Keşif ile kontrolün ayrılması

Keşif yalnızca açılışta ve kullanıcının yeniden tarama isteğinde yapılır.
`NetworkDiscovery.LastResult` sonuçları saklar. Cihaz penceresini açmak bu
önbelleği gösterir; yeni tarama başlatmaz.

| Yöntem | İletişim | Amaç |
| --- | --- | --- |
| Yeelight LAN | UDP 1982; `239.255.255.250` multicast ve sınırlı yerel unicast | Ampul konumu, kimliği ve yetenekleri |
| SSDP / UPnP | UDP 1900; `239.255.255.250` | Genel cihaz/hizmet duyuruları |
| mDNS / DNS-SD | UDP 5353; `224.0.0.251` | PTR / SRV / A kayıtlarından hizmet ve IPv4 çözümleme |
| Yeelight port doğrulaması | Gerekirse sınırlı TCP 55443 kontrolü | UDP ile cihaz bulunamadığında salt okunur `get_prop` doğrulaması |
| Işık kontrolü | Cihazın duyurduğu TCP portu; çoğunlukla 55443 | Seçili ampule JSON komutları |

UDP unicast taraması sınırlı bir yerel `/24` aralığı kullanır. Port doğrulaması
bulunamayan ampuller için geri dönüş yoludur; her komutta çalıştırılmaz.
Birden fazla protokolden gelen aynı IPv4 adresinin bilgileri birleştirilir.
Keşfedilen HTTP(S) adresi ancak kullanıcı ilgili düğmeye bastığında tarayıcıda açılır.

## Komut yaşam döngüsü

1. Arayüzden gelen işlem `SemaphoreSlim` ile sıraya alınır.
2. Ağ işi `Task.Run` üzerinden yürütülür.
3. Açık TCP bağlantısı varsa yeniden kullanılır; yoksa bilinen adrese bağlanılır.
4. JSON satırı `\r\n` ile sonlandırılarak gönderilir.
5. Aynı `id` değerini taşıyan yanıt beklenir. Cihaz hatası kullanıcıya bildirilir.
6. Soket/zaman aşımı hatasında bağlantı kapatılır ve bir kez yeniden denenir.

Örnek güç komutu:

```json
{"id":1,"method":"set_power","params":["on","sudden",0]}
```

| İşlem | Yöntem | Parametre örneği |
| --- | --- | --- |
| Durum oku | `get_prop` | `["power","bright","ct","rgb","color_mode"]` |
| Güç | `set_power` | `["off","sudden",0]` |
| Parlaklık | `set_bright` | `[50,"sudden",0]` |
| RGB | `set_rgb` | `[16711680,"smooth",200]` |
| Beyaz sıcaklığı | `set_ct_abx` | `[4000,"smooth",200]` |

Aç/kapat ve parlaklıkta ek geçiş süresi yoktur. RGB/Kelvin değişiminde 200 ms
geçiş kullanılır. Bu değerler toplam ağ/cihaz gecikmesi garantisi değildir.

## Donanım yetenekleri

Keşif yanıtındaki `support` alanı `Device.Commands` içine alınır. `set_rgb`
ve `set_ct_abx` ayrı ayrı kontrol edilir. Hem arayüz hem `LampSession`,
desteklenmeyen renk komutlarını engeller. Donanım desteği bilinmeyen elle bağlantıda
RGB ve beyaz sıcaklığı varsayılmaz.

## Durum ve kalıcılık

WinForms zamanlayıcısı yaklaşık 10 saniyede bir durum okur. Başka bir işlem
sürerken yeni durum sorgusu başlatılmaz. Kullanıcının kaydırıcıdaki henüz
uygulanmamış seçimi, arka plan güncellemesinden korunur.

Ayar dosyası exe'nin yanında `Ayarlar/ayarlar.json` konumundadır.
IP, port, cihaz kimliği, model, cihaz adı ve dört parlaklık kaydı tutulur.
Kayıtlar tüm uygulama için ortaktır. Yazma işleminde önce geçici dosya oluşturulur,
ardından hedef değiştirilir. Eski tek dosyalı yerleşimden ayar taşıma desteği vardır.

## Tasarım sınırları

- Tek `LampSession` vardır; cihaz değiştirmek önceki oturumu sonlandırır.
- Periyodik okuma ile güncellenir; tüm anlık `props` bildirimleri sürekli işlenmez.
- Yeni marka desteği için keşif sağlayıcısı tek başına yeterli değildir;
  uygun `ILightController` uygulaması ve gerekiyorsa eşleştirme akışı gerekir.
- Varsayılan protokol yerel ağ güvenine dayanır; internet üzerinden uzaktan erişim sunulmaz.
