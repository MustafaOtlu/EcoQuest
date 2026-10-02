# Low Poly Water — Unity 6 / URP uyarlaması

İçe aktarılan Low Poly Water 1.0 paketi eski Built-in görüntü sistemi için hazırlanmıştı. Shader'ın kullanılmayan `GrabPass` işlemi `_RefractionTex` hatalarını tekrarlıyordu. URP bu işlemi desteklemiyor: [Unity 6 GrabPass belgesi](https://docs.unity3d.com/6000.0/Documentation/Manual/SL-GrabPass.html).

## Kullanım

- `Assets/LowPolyWater_Pack/_Demo/DemoScene.unity` ile paketin çalışan örneğini aç.
- MainScene'de kullanmak için `Assets/LowPolyWater_Pack/Prefabs/LowPolyWater_URP.prefab` dosyasını sahneye sürükle. Su yüksekliğini ve X/Z ölçeğini gölün boyutuna göre ayarla; kaynak mesh 750 × 750 birimdir.
- `LowPolyWaterMaterial` üzerinde temel renk/parlaklık, bileşen üzerinde dalga yüksekliği/hızı/uzunluğu ayarlanır.
- Derinliğe bağlı kıyı köpüğü isteğe bağlıdır. `Depth shoreline` açılırsa kameranın URP Depth Texture özelliğini de aç. Varsayılan görünüm derinlik dokusu gerektirmez.

## Değişiklikler

- Shader URP Core/Lighting ile çalışır; mevcut GUID, malzeme ve renk özellikleri korunur. Kullanılmayan ekran kopyalama işlemi kaldırılmıştır.
- Demo kameranın eski GUI/Flare Layer ve eksik eski post-processing bileşeni kaldırılmıştır.
- Eski OBJ prefab bağlantısı Unity 6 sahne biçimine taşınmış; demo adasına mevcut renk dokusu bağlanmıştır.
- Dalga animasyonu her su nesnesine ayrı mesh oluşturur. Kaynak mesh değişmez; animasyon sınırları güncellenir, sıfır dalga uzunluğu ve büyük mesh indeksleri desteklenir.
- Su üretme penceresi doğru paket klasörünü ve varsayılan su materyalini kullanır.

## Doğrulama

2 Ekim 2026: `Tools/UnityChecks/LowPolyWaterCheck.cs`, ayrı Unity 6000.0.74f1 projesinde Play Mode ve 1600 × 900 render ile çalıştırıldı. Su/ada görünürlüğü, shader derlemesi, isteğe bağlı kıyı varyantı, sahnenin tekrar yüklenmesi, mesh bağımsızlığı, dalga hareketi ve sınırlar denetlenir. Görüntü: `Documentation/Images/LowPolyWater.png`.

Paketin kaynak dosyaları ve mevcut credits belgeleri korunmuştur. Ana haritanın yerleşimi bu uyarlama sırasında değiştirilmemiştir.
