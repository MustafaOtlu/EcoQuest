# EcoQuest düşman prototipi

MainScene'deki Enemys nesnesinin EnemySceneSetup bileşeni, Play başladığında eski statik yerleşim modellerini gizler ve iskeletli prefabları aynı noktalara yerleştirir. Asit ve Salya için ek deneme konumları tanımlıdır. Edit modunda eski yerleşim işaretleri korunur; prefabların kendileri Assets/Prefabs/Enemies içindedir.

| Tür | Rol | Saldırı | Can | Drop |
| --- | --- | --- | --- | --- |
| TinMonster | Yavaş, dayanıklı | Metal mermi, 18 hasar | 180 | 6 metal |
| FlameSpirit | Hızlı atıcı | Ateş topu, 10 hasar | 65 | 2 plastik |
| AcidRain | Uzak mesafe | Asit mermisi, 7 hasar ve 2 saniye yavaşlama | 80 | 3 plastik |
| SlimeMonster | Yakın takip | Yakın saldırı, 12 hasar ve 1,5 saniye yavaşlama | 90 | 5 plastik |
| SmokeCreature | Görüş baskısı | Yakında 2,5 saniyelik duman perdesi, doğrudan hasar yok | 55 | 2 plastik |

Düşmanlar çevrelerinde devriye gezer, görüş alanındaki oyuncuyu takip eder, saldırı klibinin yaklaşık %45'inde vuruşu uygular ve bekleme süresinden sonra yeniden saldırır. Oyuncu menzilden kaçarsa veya araya duvar girerse vuruş iptal olur. Uzaktan saldıranlar mesafeyi korur. Oyuncuyu kaybeden veya başlangıç noktasından çok uzaklaşan düşman geri döner. Yere basma, basamaklar ve çarpışmalar CharacterController ile; basit engel ve uçurum kaçınması fizik sorgularıyla yapılır. Bu sistem NavMesh tabanlı tam rota bulma değildir.

Düşman saldırıları sırasında hedefe uzanan çizgi gösterilmez; uzaktan saldıranların fiziksel mermileri ve saldırı animasyonları görünür. Oyuncunun kendi silah akışları bu değişiklikten etkilenmez. Demir top isabeti yaşayan düşmanı kısa mesafe geri iter; CharacterController hareketi duvarla sınırlar. Hasar alan düşmanın can barı kameraya dönük olarak collider'ın üzerinde görünür ve kalan cana göre soldan kısalır.

İtme sürerken takip hareketi kısa süre durur; böylece yaklaşan düşman da gerçekten geriye hareket eder. Bu tepki saldırı animasyonunun durumunu sıfırlamaz. Engel kontrolü, basamak yüksekliğinin üstünden göz hizasına kadar gövdeyi tarar; alçak kutuları algılar, 10 cm yüksekliğindeki ekim yataklarını geçilebilir bırakır.

Animasyonlar her türün kendi iskeletli Idle FBX modeli üzerinde Generic rig ile çalışır. Root motion kapalıdır; hareket koddan gelir. Locomotion Blend Tree Speed parametresini kullanır; Attack tetikleyicisi tek seferlik saldırıya geçer. Duman için yürüyüş dosyası olmadığından hareket sırasında Idle kullanılır. Ölüm klibi bulunmadığından canı biten düşman kaldırılır.

EnemyBrain bileşenindeki hız, menzil, can, hasar, hazırlık ve bekleme süreleri prefab Inspector'ından değiştirilebilir. Mermiler hızlı geçişlerde çarpışmayı kaçırmamak için her karede katedilen mesafeyi SphereCast ile tarar; atıcıyı yok sayar ve ilk duvarda durur.

PlayerVitals, oyuncuya 100 karakter enerjisi, geçici yavaşlama ve duman perdesi sağlar. Enerji biterse oyuncu aynı yerde beş saniyede toparlanır; bu sırada düşmanlar oyuncuyu uygun saldırı hedefi olarak seçmez. Envanter ve silah elektriği korunur. Efektler yenilenebilir ama üst üste çarpılarak sınırsız yavaşlatmaz. Oyuncu silah hasarı EnemyBrain.TakeDamage üzerinden bağlıdır; düşmanlar etkisiz kaldığında fiziksel ganimet bırakır. Ayrıntılar EnergyAndProgression.md içindedir.

## Doğrulama

24 Eylül 2026'da ayrı Unity 6000.0.74f1 test kopyasında uzaktan saldırının hâlâ mermi oluşturduğu ve düşmana ait hiçbir LineRenderer üretmediği doğrulandı. Hasar sonrası can barının genişliği azaldı; Teneke ölümü toplam 6 metalli iki modellenmiş ganimet oluşturdu ve tekrar hasar ganimeti çoğaltmadı. Aynı çalıştırmada silah/duvar etkileşimleri, sulama, hasat ve envanter kaydı kontrolleri de geçti. MainScene ayrıca gerçek prefablarıyla Play Mode'da açıldı; beş düşman ve saha istasyonu doğrulandı. Kullanıcı kontrol sırası PlayerWeapons.md içindedir.

Ek hareket kontrolünde beş gerçek düşman prefabının her biri düz zeminde ve 10 cm basamakta ilerledi, 45 cm kutunun çevresinden fiziksel olarak geçti ve normal takip hızındayken darbe yönünde oyuncudan uzaklaştı. Toplam 35 hareket kontrolü geçti. Bu yerel engel kaçınması, karmaşık kapalı koridorlarda tam rota bulma garantisi vermez.
## Güncel silah bağlantısı

Oyuncu silahları artık EnemyBrain.TakeDamage ile bağlıdır; kullanım ve HUD ayrıntıları PlayerWeapons.md içindedir. Sürekli su/anti-vakum hasarı düşmanın saldırı hazırlığını her karede sıfırlamaz.

Ölen düşmanlar kaynaklarını fiziksel ganimet olarak bırakır. Teneke toplam 6 metali iki adet 3 metalli ezilmiş kutuyla bırakır. Diğer düşmanların plastik ganimeti bir şişeyle gösterilir; miktarlar yukarıdaki tablodadır. Her ganimet pozitif miktar içerir. Modeller hafifçe döner ve yükselip alçalır. Vakumun Toplama modu ham kaynağı alır; 3 ile Geri Dönüşüm seçildikten sonra R ham atığı işler. Geri Dönüşüm silahıyla yerdeki ganimete doğrudan sol tık basılı tutmak da mümkündür. Modellerin kaynak ve lisans bilgileri FreeAssets.md içindedir.

HUD üst bölümünde aktif düşman sayısı gösterilir. Son düşman yenildiğinde alan temizleme adımı biter ve ekim/hasat hedefi görünür. Ganimet toplama, yeniden mühimmat üretmek için kullanılabilir.

Düşman hedefi tamamlandıktan sonra HUD sırasıyla tohum ekme, sulama ve hasat adımlarını yönlendirir. Temiz hasattan sonra temel alan görevi tamamlanır; son bitkinin hasatla kaldırılması tamamlanmış hedefi geri almaz.
