# SolidWorks AI Automation System 🚀

Bu proje, doğal dil komutlarını gerçek zamanlı SolidWorks operasyonlarına dönüştüren, gelişmiş geometrik analiz yeteneklerine sahip AI tabanlı bir otomasyon sistemidir. .NET 8 tabanlı bu sistem, SolidWorks COM API'sini kullanarak karmaşık tasarım süreçlerini otonom hale getirir.

## 🌟 Öne Çıkan Özellikler

*   **Doğal Dil İşleme (NLP) Entegrasyonu**: Teknik talimatları (örn: "Dört köşeye M6 havşa başlı delik aç") JSON tabanlı aksiyon planlarına dönüştürür ve yürütür.
*   **Akıllı Geometrik Analiz**:
    *   **Topolojik Sınıflandırma**: Kenarların dışbükey (convex) veya içbükey (concave) olduğunu algılama.
    *   **Vektörel Seçim**: Yüzey normal vektörlerine (nx, ny, nz) veya kenar yönelimlerine göre otomatik seçim.
    *   **Hassas Ölçüm**: Delik çaplarını ve parça boyutlarını programatik olarak ölçebilme.
*   **Gelişmiş COM Yönetimi**:
    *   **STA Thread İzolasyonu**: Tüm SolidWorks çağrıları güvenli bir STA thread üzerinden yürütülür.
    *   **Retry Mechanism**: SolidWorks meşgul olduğunda (busy state) otomatik yeniden deneme mantığı.
    *   **SafeRelease**: Bellek sızıntılarını önlemek için deterministik COM objesi temizliği.
*   **Dinamik Akış Kontrolü**: Aksiyonlar arasında değişken taşıma (`{{variable}}`), döngüler (`loop_over_entities`) ve mantıksal koşullar (`condition`) desteği.

## 🏗 Mimari Yapı

Proje üç ana katman üzerine kurulmuştur:

1.  **SolidWorksConnector**: Uygulamanın SolidWorks örneğine bağlanmasını, bağlantı durumunu takip etmesini ve STA thread kuyruğunu yönetmesini sağlar.
2.  **ActionRegistry**: 100'den fazla SolidWorks API fonksiyonunu (Sketch, Feature, Drawing, Assembly) AI tarafından tüketilebilir "handler" yapılarına dönüştürür.
3.  **ActionExecutor**: Gelen JSON planlarını ayrıştırır, değişkenleri çözer (resolve) ve aksiyonları mantıksal bir sıra ile yürütür.

## 📋 Desteklenen Bazı Aksiyonlar

*   **Tasarım**: `create_sketch_rectangle`, `feature_extrusion`, `feature_revolve`, `feature_hole_wizard`.
*   **Analiz**: `classify_edge`, `measure_diameter`, `get_mass_properties`.
*   **Teknik Resim**: `auto_generate_drawing`, `insert_bom`, `create_section_view`.
*   **Montaj**: `add_component`, `add_mate`, `animate_explode`.

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
*   SolidWorks 2024 veya üzeri (Sistem SolidWorks 2026 ile test edilmiştir).
*   .NET 8 SDK.

### Başlangıç
1. Projeyi derleyin:
   ```powershell
   dotnet build
   ```
2. Uygulamayı çalıştırın:
   ```powershell
   dotnet run --project src/SolidWorksAI
   ```
3. SolidWorks'ün açık olduğundan emin olun. Sistem otomatik olarak aktif örneğe bağlanacaktır.

## 💡 Örnek Komut Akışı (JSON)

```json
{
  "description": "Otomatik Radyus Uygulama",
  "actions": [
    {
      "action_type": "select_edges_by_direction",
      "parameters": { "dy": 1.0, "output_var": "DikeyKenarlar" }
    },
    {
      "action_type": "create_fillet_on_edges",
      "parameters": { "radius": 0.002, "edges": "{{DikeyKenarlar}}" }
    }
  ]
}
```

## 🛠 Geliştirme Notları
Yeni bir aksiyon eklemek için `src/SolidWorksAI/Services/ActionRegistry.cs` dosyasına yeni bir handler kaydetmeniz ve `src/SolidWorksAI/Schemas/DrawingActionsSchema.json` dosyasını güncellemeniz yeterlidir.

