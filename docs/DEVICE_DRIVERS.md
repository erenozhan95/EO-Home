# Cihaz türleri ve kontrol sürücüleri

EO-Light, **keşif** ile **kontrolü** ayrı tutar. Ağ taraması bir servisin adresini ve
duyurduğu türü bulabilir; tek başına komut protokolünü veya cihazın gerçekten
desteklediği özellikleri kanıtlamaz.

## Şimdiki durum

| Tür | Bulma | Kontrol |
| --- | --- | --- |
| Yeelight LAN ampul | UDP duyurusu ve yerel tarama | Güç, parlaklık; donanım destekliyorsa RGB ve beyaz tonu |
| Açıkça klima/termostat türü duyuran SSDP veya mDNS servisi | Tür etiketiyle listelenir | Henüz sürücü yok; ayarlar pasif |
| Diğer SSDP/mDNS/Matter duyuruları | Genel ağ cihazı olarak listelenir | Henüz sürücü yok |

Klima tespiti, servis adındaki tahmine değil, ilan edilen tür bilgisine dayanır.
Bu bilgi yoksa cihaz **bilinmeyen** olarak kalır. Bir klima ağda hiç servis
duyurmuyorsa taramada görünmeyebilir. Matter'ın mDNS duyurusu tek başına cihazın
klima olduğunu veya kontrol yetkisi bulunduğunu göstermez; eşleştirme ve cihaz
veri modelinin okunması gerekir.

## Klima sürücüsü ekleme sözleşmesi

1. Cihazın marka/modelini ve yerel API, Matter veya köprü desteğini doğrula.
2. Gerekli eşleştirme/kimlik doğrulamasını uygula; gizli anahtarları Git'e koyma.
3. Cihazdan gerçek durumu ve desteklenen işlevleri oku: güç, hedef sıcaklık
   aralığı, modlar, fan hızları, isteğe bağlı oda sıcaklığı.
4. `ClimateState` alanlarını gerçek verilerle doldur. Arayüz yalnızca cihazın
   bildirdiği işlevleri açar.
5. `ClimateCommand` doğrulamasından geçen komutları ilgili protokolle gönder;
   yanıtı okuyup ekrandaki durumu güncelle. Ağda görünen fakat desteklenmeyen
   cihazlara komut gönderme.
6. Sahte cihazla sıcaklık sınırı, desteklenmeyen mod, bağlantı kopması ve
   yeniden tarama davranışını test et.

Şu anda `control_climate` gerçek cihaza komut göndermez; sürücü eksikliğini açık
bir hata olarak bildirir. `?demo` yalnızca geliştirme sırasında çalışan örnek
klima sunar ve fiziksel cihazlara bağlanmaz.

Matter klima/termostat veri modeli için [CSA cihaz türleri](https://csa-iot.org/wp-content/uploads/2024/05/matter-1-3-device-library-specification.pdf) ve [Google'ın keşif/eşleştirme açıklaması](https://developers.home.google.com/matter/primer/commissionable-and-operational-discovery) başvuru kaynaklarıdır.
